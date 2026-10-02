using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace RateLimiterGateway.LoadDriver;

/// <summary>Fires a concurrent burst of requests across several gateway base URLs and tallies the result.</summary>
public static class Load
{
    public sealed class Result
    {
        public int Offered;
        public int Admitted;        // 200 from downstream (passed the limiter)
        public int RateLimited;     // 429
        public int Other;           // anything else (errors)
        public double ElapsedSeconds;
        public readonly ConcurrentDictionary<string, int> AdmittedByInstance = new();
        public readonly ConcurrentDictionary<int, int> StatusHistogram = new();
    }

    public static async Task<Result> RunBurstAsync(
        HttpClient http,
        IReadOnlyList<string> gatewayBaseUrls,
        int totalRequests,
        string path,
        Action<HttpRequestMessage> decorate,
        CancellationToken ct = default)
    {
        var result = new Result { Offered = totalRequests };
        var sw = Stopwatch.StartNew();

        var tasks = Enumerable.Range(0, totalRequests).Select(async i =>
        {
            var baseUrl = gatewayBaseUrls[i % gatewayBaseUrls.Count];
            using var req = new HttpRequestMessage(HttpMethod.Get, $"{baseUrl}{path}");
            decorate(req);
            try
            {
                using var resp = await http.SendAsync(req, HttpCompletionOption.ResponseContentRead, ct);
                var code = (int)resp.StatusCode;
                result.StatusHistogram.AddOrUpdate(code, 1, (_, v) => v + 1);
                if (code == 200)
                {
                    Interlocked.Increment(ref result.Admitted);
                    var inst = resp.Headers.TryGetValues("X-Gateway-Instance", out var vals)
                        ? vals.FirstOrDefault() ?? "?" : "?";
                    result.AdmittedByInstance.AddOrUpdate(inst, 1, (_, v) => v + 1);
                }
                else if (code == 429) Interlocked.Increment(ref result.RateLimited);
                else Interlocked.Increment(ref result.Other);
            }
            catch
            {
                Interlocked.Increment(ref result.Other);
                result.StatusHistogram.AddOrUpdate(0, 1, (_, v) => v + 1);
            }
        });

        await Task.WhenAll(tasks);
        sw.Stop();
        result.ElapsedSeconds = sw.Elapsed.TotalSeconds;
        return result;
    }

    public static HttpClient NewClient() => new(new SocketsHttpHandler
    {
        MaxConnectionsPerServer = 512,
        PooledConnectionLifetime = TimeSpan.FromMinutes(1)
    })
    { Timeout = TimeSpan.FromSeconds(30) };
}

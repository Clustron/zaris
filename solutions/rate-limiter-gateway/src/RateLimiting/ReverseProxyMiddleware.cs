using System;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace RateLimiterGateway.RateLimiting;

public sealed class ReverseProxyOptions
{
    /// <summary>Base address of the downstream service all admitted traffic is forwarded to.</summary>
    public Uri DownstreamBaseAddress { get; set; } = new("http://localhost:5090");
}

/// <summary>
/// Minimal, honest reverse proxy: once a request has been admitted by the rate limiter it is
/// forwarded verbatim (method, path, query, headers, body) to the downstream service, and the
/// downstream response is streamed back. Terminal middleware (never calls next).
/// </summary>
public sealed class ReverseProxyMiddleware
{
    private static readonly HttpMethod[] Bodyless =
        { HttpMethod.Get, HttpMethod.Head, HttpMethod.Delete, HttpMethod.Trace };

    private readonly ReverseProxyOptions _options;
    private readonly IHttpClientFactory _httpFactory;
    private readonly ILogger<ReverseProxyMiddleware> _logger;

    public ReverseProxyMiddleware(
        RequestDelegate _,
        ReverseProxyOptions options,
        IHttpClientFactory httpFactory,
        ILogger<ReverseProxyMiddleware> logger)
    {
        _options = options;
        _httpFactory = httpFactory;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext ctx)
    {
        var client = _httpFactory.CreateClient("downstream");
        var target = new Uri(_options.DownstreamBaseAddress, ctx.Request.Path + ctx.Request.QueryString);
        var method = new HttpMethod(ctx.Request.Method);

        using var upstream = new HttpRequestMessage(method, target);

        var hasBody = Array.IndexOf(Bodyless, method) < 0 && ctx.Request.ContentLength != 0;
        if (hasBody)
            upstream.Content = new StreamContent(ctx.Request.Body);

        foreach (var header in ctx.Request.Headers)
        {
            if (string.Equals(header.Key, "Host", StringComparison.OrdinalIgnoreCase)) continue;
            var values = header.Value.ToArray();
            if (!upstream.Headers.TryAddWithoutValidation(header.Key, values) && upstream.Content != null)
                upstream.Content.Headers.TryAddWithoutValidation(header.Key, values);
        }
        upstream.Headers.TryAddWithoutValidation("X-Forwarded-For", ctx.Connection.RemoteIpAddress?.ToString());
        // The X-Gateway-Instance request header (stamped by the gateway's own middleware) is already
        // copied above, so the downstream sees exactly which instance admitted the request.

        try
        {
            using var response = await client.SendAsync(
                upstream, HttpCompletionOption.ResponseHeadersRead, ctx.RequestAborted);

            ctx.Response.StatusCode = (int)response.StatusCode;
            foreach (var header in response.Headers)
                ctx.Response.Headers[header.Key] = header.Value.ToArray();
            foreach (var header in response.Content.Headers)
                ctx.Response.Headers[header.Key] = header.Value.ToArray();
            ctx.Response.Headers.Remove("transfer-encoding");

            await response.Content.CopyToAsync(ctx.Response.Body, ctx.RequestAborted);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogWarning(ex, "Downstream forward failed for {Path}", ctx.Request.Path);
            ctx.Response.StatusCode = StatusCodes.Status502BadGateway;
            await ctx.Response.WriteAsJsonAsync(new { error = "bad_gateway", detail = ex.Message });
        }
    }
}

// Clustron Zaris – ASP.NET Core Session Sample
//
// Stores a per-session hit counter in Clustron Zaris via the distributed session provider.
// One call — services.AddZarisSession(...) — wires Zaris as the ISession store.
//
//   dotnet run                -> starts a web server; GET / repeatedly to watch the counter grow.
//   dotnet run -- --selftest  -> runs an in-memory two-request session flow and prints PASS/FAIL.
//
// By default the sample uses an embedded (in-process) Zaris store so it needs no external cluster.
// To run against a real cluster, set Zaris:ConnectionString (e.g. the ZARIS__CONNECTIONSTRING env var
// or appsettings.json) to a networked store, e.g. "zaris://host1:7861,host2:7861/sessions".

using Clustron.Zaris.AspNetCore.Session;
using Microsoft.AspNetCore.TestHost;

var selfTest = args.Contains("--selftest");

var builder = WebApplication.CreateBuilder(args);
if (selfTest)
    builder.WebHost.UseTestServer();

var storeName = builder.Configuration["Zaris:StoreName"] ?? "sessions";
var connectionString = builder.Configuration["Zaris:ConnectionString"] ?? $"zaris://inproc/{storeName}";

builder.Services.AddZarisSession(options =>
{
    options.StoreName = storeName;
    options.ConnectionString = connectionString;
    options.KeyPrefix = "zaris:session:";
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.CookieName = ".Zaris.Session";
    options.CookieIsEssential = true;
});

var app = builder.Build();

app.UseSession();

app.MapGet("/", (HttpContext ctx) =>
{
    var count = (ctx.Session.GetInt32("count") ?? 0) + 1;
    ctx.Session.SetInt32("count", count);
    return Results.Text($"count={count}");
});

if (!selfTest)
{
    Console.WriteLine($"Zaris session store: {connectionString}");
    Console.WriteLine("GET http://localhost:5000/ repeatedly to watch the per-session counter grow.");
    app.Run();
    return;
}

// -------------------------------------------------------------------------------------------------
// Self-test: two requests sharing the session cookie must see the counter go 1 -> 2, proving the
// value round-tripped through Zaris between requests. No TCP port is opened.
// -------------------------------------------------------------------------------------------------
await app.StartAsync();
var client = app.GetTestClient();

var r1 = await client.GetAsync("/");
var body1 = await r1.Content.ReadAsStringAsync();
var cookie = ExtractSessionCookie(r1);

var req2 = new HttpRequestMessage(HttpMethod.Get, "/");
if (cookie is not null)
    req2.Headers.Add("Cookie", cookie);
var r2 = await client.SendAsync(req2);
var body2 = await r2.Content.ReadAsStringAsync();

Console.WriteLine("Request 1 (new session):      " + body1);
Console.WriteLine("Request 2 (same cookie):      " + body2);

var ok = body1 == "count=1" && body2 == "count=2";
Console.WriteLine(ok
    ? "SELFTEST PASS: session value persisted in Zaris across requests."
    : "SELFTEST FAIL: expected count=1 then count=2.");

await app.StopAsync();
Environment.Exit(ok ? 0 : 1);

static string? ExtractSessionCookie(HttpResponseMessage response)
{
    if (!response.Headers.TryGetValues("Set-Cookie", out var setCookies))
        return null;
    foreach (var sc in setCookies)
    {
        var semi = sc.IndexOf(';');
        return semi >= 0 ? sc.Substring(0, semi) : sc;
    }
    return null;
}

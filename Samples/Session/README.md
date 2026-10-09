# ASP.NET Core Session over Clustron Zaris

Stores ASP.NET Core **session** state in Clustron Zaris using the
[`Clustron.Zaris.AspNetCore.Session`](https://www.nuget.org/packages/Clustron.Zaris.AspNetCore.Session)
provider. A single `services.AddZarisSession(...)` call wires Zaris as the `ISession` store — with key
prefixing, sliding idle-timeout expiration (via Zaris TTL), and the usual cookie options.

## Run it

```bash
# Embedded (in-process) store — no external cluster needed:
dotnet run

# Then, repeatedly:
curl http://localhost:5000/      # count=1, count=2, count=3, ... for the same session cookie
```

Two-request self-test (no port opened), proving the value round-trips through Zaris:

```bash
dotnet run -- --selftest
# Request 1 (new session):      count=1
# Request 2 (same cookie):      count=2
# SELFTEST PASS: session value persisted in Zaris across requests.
```

## Point it at a real cluster

Set the connection string (and store name) via configuration — `appsettings.json` or environment:

```bash
ZARIS__CONNECTIONSTRING="zaris://host1:7861,host2:7861/sessions" ZARIS__STORENAME="sessions" dotnet run
```

## The wiring

```csharp
builder.Services.AddZarisSession(options =>
{
    options.StoreName        = "sessions";
    options.ConnectionString = "zaris://inproc/sessions"; // or a networked cluster
    options.KeyPrefix        = "zaris:session:";
    options.IdleTimeout      = TimeSpan.FromMinutes(30);  // sliding, enforced by Zaris TTL
    options.CookieName       = ".Zaris.Session";
    options.CookieIsEssential = true;
});

app.UseSession();
```

Because session entries live in Zaris, they are shared across every instance of your web app and
survive instance restarts — the standard reason to use a distributed session store.

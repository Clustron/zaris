# Classic ASP.NET (System.Web) SessionState over Clustron Zaris

For **classic ASP.NET** (Web Forms / classic MVC on .NET Framework 4.7.2+), the
[`Clustron.Zaris.SessionState`](https://www.nuget.org/packages/Clustron.Zaris.SessionState) package is a
drop-in `SessionStateStoreProviderBase` — your `Session["..."]` state lives in Clustron Zaris instead of
in-process memory or SQL Server, surviving app-pool recycles and shared across a web farm.

> This is a **configuration-only** integration (no code changes), so it's documented here as a `web.config`
> recipe rather than a runnable project — the cross-platform samples solution targets .NET 8, while this
> provider targets .NET Framework 4.7.2 / 4.8 and runs under IIS/System.Web.

## Install

```
dotnet add package Clustron.Zaris.SessionState
```

## Configure (`web.config`)

```xml
<configuration>
  <connectionStrings>
    <add name="ZarisSessions"
         connectionString="zaris://zaris-1:7861,zaris-2:7861/sessions" />
  </connectionStrings>

  <system.web>
    <!-- timeout = sliding session timeout, in minutes (enforced by Zaris TTL) -->
    <sessionState mode="Custom" customProvider="Zaris" timeout="20">
      <providers>
        <add name="Zaris"
             type="Clustron.Zaris.SessionState.ZarisSessionStateStoreProvider, Clustron.Zaris.SessionState"
             connectionStringName="ZarisSessions"
             keyPrefix="zaris:session:"
             lockTimeoutSeconds="90" />
      </providers>
    </sessionState>
  </system.web>
</configuration>
```

That's it — use `Session` exactly as always (`Session["user"] = "alice";`). The provider implements correct
exclusive-lock semantics (lock id / age / timeout), sliding expiration via Zaris TTL, and cookieless support.

| Attribute | Required | Default | Meaning |
|---|---|---|---|
| `connectionString` | one of these | — | Zaris connection string, e.g. `zaris://host:7861/sessions` (TLS: `zariss://…`). |
| `connectionStringName` | one of these | — | Name of a `<connectionStrings>` entry holding the Zaris connection string. |
| `keyPrefix` | no | `zaris:session:` | Prefix for every Zaris key the provider writes. |
| `lockTimeoutSeconds` | no | `90` | Safety-net TTL on the lock key so a crashed request can't wedge a session. Set ≥ your longest request. |

For the modern **ASP.NET Core** session store, see the [`Session`](../Session) sample instead.

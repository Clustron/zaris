# Clustron Zaris — Samples

Official, runnable samples for the **Clustron Zaris** .NET client. Each sample is
a self-contained console app that demonstrates one feature area and runs in either
mode without code changes:

- **InProc** — an embedded in-memory store; no server required (just `dotnet run`).
- **Remote** — connects to a running Zaris cluster over the network.

Every sample isolates its keys per run, prints a clear pass/fail line, and cleans
up the data it creates.

---

## Samples

| Project | Demonstrates |
|---|---|
| **Basic** | PUT/GET, metadata (TTL, labels, content-type), a counter, TTL expiry, cleanup |
| **Bulk** | Batch `PutMany`/`GetMany`/`DeleteMany` and `Count` |
| **CAS** | Compare-and-swap: put-if-absent, if-match(version), conflict detection, conditional delete |
| **Counters** | Atomic add, get/set, Min/Max bounds, counter TTL |
| **DistributedJobQueue** | A producer + competing workers claiming/completing jobs |
| **HybridCache** | `HybridCache` with an in-memory L1 over a distributed L2, tags, L1/L2 expiry |
| **IDistributedCache** | The standard ASP.NET `IDistributedCache` (Set/Get/Refresh/Remove) over Zaris |
| **LeaderElection** | Lease-based leader election across simulated nodes, with watch |
| **Lease** | Grant a lease, attach keys, auto-expiry, keep-alive, revoke |
| **RateLimiter** | Fixed-window rate limiting built on counters |
| **Search** | Label / secondary-index scan and query |
| **Transactions** | Multi-key transaction commit and rollback |
| **Watch** | Watch a key and a prefix — initial snapshot plus live change events |

**Shared** is a library the executables reference (console helpers, config binding,
per-run key isolation, the pass/fail run wrapper). It is not a runnable sample.

---

## Running a sample

```bash
cd Basic          # or any sample folder
dotnet run
```

By default samples run **InProc**, so no server is needed. To target a running
cluster, set the store's mode to `Remote` (see below) or set the environment
variable `ZARIS_SAMPLE_MODE=Remote` before running.

---

## Configuration

Each sample reads `appsettings.json`. Stores are configured under
`Zaris:Stores:<storeName>`; the samples use the store name **`teststore`**:

**InProc** (default — embedded, no server):

```json
{
  "Zaris": {
    "Stores": {
      "teststore": { "Mode": "InProc" }
    }
  }
}
```

**Remote** (connect to a running cluster):

```json
{
  "Zaris": {
    "Stores": {
      "teststore": {
        "Mode": "Remote",
        "Seeds": [
          { "Host": "127.0.0.1", "Port": 7990 }
        ]
      }
    }
  }
}
```

- `Mode` — `InProc` or `Remote`.
- `Seeds[]` — one entry per node client endpoint. `Port` is the **client port**
  chosen when the store was created (`New-ZrStore -BaseClientPort`; each instance
  is `BaseClientPort + i`).

Environment overrides use the standard .NET double-underscore syntax, e.g.
`Zaris__Stores__teststore__Mode=Remote` and
`Zaris__Stores__teststore__Seeds__0__Host` / `__Port`.

---

## Suggested order

Start with **Basic**, then **Counters**, **Bulk**, and **CAS** for the core data
plane; **Watch** and **Transactions** for consistency; **Lease**,
**LeaderElection**, and **RateLimiter** for coordination; and
**IDistributedCache** / **HybridCache** for the ASP.NET caching integrations.

---

These samples are the baseline developer experience for Clustron Zaris and
illustrate production-style client patterns.

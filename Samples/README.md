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
| **Batch** | Mixed `ExecuteBatchAsync` — PUT/GET/DELETE in one request, one round-trip per owning node |
| **Bulk** | Batch `PutMany`/`GetMany`/`DeleteMany` and `Count` |
| **CAS** | Compare-and-swap: put-if-absent, if-match(version), conflict detection, conditional delete |
| **Counters** | Atomic add, get/set, Min/Max bounds, counter TTL |
| **DistributedJobQueue** | A producer + competing workers claiming/completing jobs |
| **Expiration** | TTL management — `Expire`/`Persist`/`GetTimeToLive` and watching a key expire |
| **Hashes** | Native hash — set/get/get-all fields, field increment, exists, length, delete |
| **HybridCache** | `HybridCache` with an in-memory L1 over a distributed L2, tags, L1/L2 expiry |
| **IDistributedCache** | The standard ASP.NET `IDistributedCache` (Set/Get/Refresh/Remove) over Zaris |
| **LeaderElection** | Lease-based leader election across simulated nodes, with watch |
| **Lease** | Grant a lease, attach keys, auto-expiry, keep-alive, revoke |
| **Lists** | Native list — push/pop both ends, range, index/set, remove, length |
| **Locks** | Distributed mutex — acquire, contention, renew, release, mutual exclusion |
| **PubSub** | Publish/subscribe and pattern-subscribe with cluster-wide fan-out |
| **RateLimiter** | Fixed-window rate limiting built on counters |
| **Search** | Label / secondary-index scan and query |
| **Sets** | Native set — add/remove, membership, count, random members, pop |
| **SortedSets** | Native sorted set — scored members, rank/score ranges, increments (a leaderboard) |
| **Streams** | Append-only event log with consumer groups — add/read/range, group read + ack |
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

By default samples run **InProc** (an embedded in-memory store), so no server is
needed — just `dotnet run`. To target a running Zaris cluster, point the store's
connection string at your nodes (see below). The exact same sample binary runs
either way; only the connection string changes.

---

## Configuration

Each sample reads `appsettings.json` and connects with
`AddClustronZarisFromConnectionStrings`, using the store name **`teststore`** under
`ConnectionStrings`. In-process vs. remote is chosen by the connection string alone.

**InProc** (default — embedded, no server):

```json
{
  "ConnectionStrings": {
    "teststore": "zaris://inproc/teststore"
  }
}
```

**Remote** (connect to a running cluster):

```json
{
  "ConnectionStrings": {
    "teststore": "zaris://127.0.0.1:7861/teststore"
  }
}
```

- Comma-separate seed nodes for bootstrap failover:
  `zaris://host1:7861,host2:7861/teststore`.
- Use `zariss://` for TLS, and supply a token / CA through the connection string
  (`?token=env:ZARIS_TOKEN`, `?ca=file:/path/ca.pem`).
- The port is the **client port** chosen when the store was created
  (`New-ZrStore -BaseClientPort`; each instance is `BaseClientPort + i`).

To switch a sample to a cluster without editing files, override the connection
string with the standard .NET double-underscore environment syntax:

```bash
ConnectionStrings__teststore="zaris://127.0.0.1:7861/teststore" dotnet run
```

---

## Suggested order

Start with **Basic**, then **Counters**, **Bulk**, **Batch**, **CAS**, and
**Expiration** for the core data plane; **Hashes**, **Lists**, **Sets**,
**SortedSets**, and **Streams** for the native data structures, with **PubSub**
for messaging; **Watch** and **Transactions** for consistency; **Lease**,
**Locks**, **LeaderElection**, and **RateLimiter** for coordination; and
**IDistributedCache** / **HybridCache** for the ASP.NET caching integrations.

---

These samples are the baseline developer experience for Clustron Zaris and
illustrate production-style client patterns.

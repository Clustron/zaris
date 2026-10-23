# 🚀 Clustron Zaris — CDC Cache Invalidation Sample

**What this shows / why you'd use it.** A very common job for a cache in front of a store is to
**stay in sync with it**: when a value changes you want the cache refreshed, and when a value is
deleted you want it evicted — without polling. This sample does that by consuming the **change
feed (CDC)**: every `Put` refreshes the matching entry in a local in-memory cache, and every
`Delete` evicts it. The result is a cache that tracks the store automatically.

It's the same durable, ordered change feed used by the **[Cdc.AuditLog](../Cdc.AuditLog)** sample
(start there for the primary CDC walkthrough and the resume-on-restart demo). Here the feed is put
to a concrete purpose: **keeping a secondary copy of the data coherent**, which is also the shape
of an index rebuild, a materialized view, or replication.

---

# 📌 What This Sample Demonstrates

- Opening a change-feed consumer: `IZarisChangeFeedClient.OpenChangeFeed(...)`
- `Put` → refresh the cached value; `Delete` → evict it
- A **server-side prefix filter** (`KeyPrefix`) and **kind filter** (`Kinds`)
- **Idempotent apply** under at-least-once delivery (dedup by key + revision), so a re-delivered
  event never corrupts the cache

---

# ⚙️ Prerequisites (one necessary difference from the other samples)

Unlike the other samples, this one **cannot run InProc** — the change feed needs the server
op-log and the remote client. So it requires:

- A **running Zaris cluster / store** named `teststore`, reachable at the connection string in
  `appsettings.json` (defaults to `zaris://127.0.0.1:7861/teststore`).
- The **change feed enabled** on it: `changeFeed.enabled = true` in `clustron.json`, or start the
  node(s) with `ZARIS_WAN_SYNC_FEED=1`.
- **Zaris 2.1.0+** (the change feed shipped in 2.1.0).

👉 Server setup: https://clustron.io/docs/clustron/zaris/getting-started/overview/

To point at your own cluster without editing files:

```bash
ConnectionStrings__teststore="zaris://your-host:7861/teststore" dotnet run
```

---

# ▶️ Run it

```bash
dotnet run
```

The sample writes 5 mutations under a per-run prefix (3 puts, 1 update, 1 delete), consumes the
feed, applies each change to the local cache, and prints the final cache:

```
[CACHE] REFRESH sample:cdc-cache:...:product:1 = Keyboard          (size=1)
[CACHE] REFRESH sample:cdc-cache:...:product:2 = Mouse             (size=2)
[CACHE] REFRESH sample:cdc-cache:...:product:3 = Monitor           (size=3)
[CACHE] REFRESH sample:cdc-cache:...:product:2 = Mouse (wireless)  (size=3)
[CACHE] EVICT   sample:cdc-cache:...:product:3                     (size=2)
```

It cleans up the keys it created, so it is safe to run repeatedly.

---

# 🧠 Why CDC (and not Watch) here

If a cache misses an eviction it can serve **stale** data indefinitely — a correctness bug. The
change feed is **durable and resumable**, so a consumer that restarts catches up on the evictions
it missed rather than silently skipping them. (For low-latency *latest-value* reactions where a
brief gap is harmless, the **Watch** sample is the lighter choice.) See
**[../../docs/cdc-vs-watch.md](../../docs/cdc-vs-watch.md)**.

---

# 📦 Summary

| | Value |
|---|---|
| API | `IZarisChangeFeedClient.OpenChangeFeed(ChangeFeedOptions, ct)` |
| Apply rule | `Put` → refresh; `Delete` → evict; idempotent |
| Good for | Near-caches, index/view rebuilds, replication |
| Needs | A running cluster with the change feed enabled (not InProc) |

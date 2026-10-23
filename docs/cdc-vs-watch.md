# CDC (Change Feed) vs Watch — which one?

Clustron Zaris gives you two ways to react to keyspace changes. They look similar — both hand
you events as data changes — but they make **opposite guarantees**, and picking the wrong one is
a common source of subtle bugs. This page is the decision rule.

## The one-line rule

> Use **CDC** (the change feed) when a *missed* event is a **correctness bug**.
> Use **Watch** when you only need the *latest state*, fast, and a missed event is harmless.

## Side by side

| | **CDC — Change Feed** | **Watch** |
|---|---|---|
| API | `IZarisChangeFeedClient.OpenChangeFeed(ChangeFeedOptions, ct)` | `IZaris.Watch.WatchPrefixAsync(...)` / `WatchKeyAsync(...)` |
| Delivery | **Durable, at-least-once** | **Best-effort, live-from-now** |
| On restart / reconnect | **Resumes** from the server-tracked offset (by `ConsumerName`); a fresh name **replays from the start** | Starts fresh — anything emitted while disconnected is **gone** |
| Ordering | Per-key / per-partition | Per-key, best-effort |
| Backfill | A fresh consumer replays existing state; a lagging one re-snapshots | Optional initial snapshot (`IncludeInitialSnapshot`), then live only |
| Duplicates | Possible on reconnect / recovery — **dedup by key + `Revision`** | Rare, treat as best-effort |
| Server requirement | Change feed enabled (`ZARIS_WAN_SYNC_FEED=1` / `changeFeed.enabled=true`) — **2.1.0+** | Always available |
| Cost | Durable offsets + retention on the server | Cheap, in-memory fan-out |

## Choose CDC when a missed event breaks correctness

Reach for the change feed (`Cdc.AuditLog`, `Cdc.CacheInvalidation`) when the consumer is
**recording or mirroring state** and must see **every** change, even across a restart:

- **ETL / outbox** — forward every write to a warehouse, queue, or downstream service.
- **Audit / event sourcing** — an append-only record of what changed, replayable from the start.
- **Cache or index rebuild** — project the keyspace into a secondary store (search index,
  materialized view) that must not drift.
- **Replication / sync** — keep another system eventually consistent with Zaris.

These all tolerate a *duplicate* (idempotent upsert / delete by key) but **not a drop**. The
change feed's durable `ConsumerName` is what makes that safe: the server tracks the committed
offset, so a crashed consumer resumes where it left off rather than losing the gap.

```csharp
var feed = (IZarisChangeFeedClient)client;
var options = new ChangeFeedOptions
{
    KeyPrefix    = "audit/",              // server-side filter
    ConsumerName = "audit-logger",        // durable → resumable; a FRESH name replays from the start
    Kinds        = new[] { FeedEventKind.Put, FeedEventKind.Delete },
};

// Re-opening with the SAME ConsumerName resumes from the last committed offset (no replay),
// so a restart never drops or re-processes changes it already handled.
await foreach (ChangeEvent ev in feed.OpenChangeFeed(options, ct))
{
    log.Record(ev.Key, ev.Kind, ev.Revision, ev.WriteUnixMs);   // idempotent → a re-delivery is harmless
}
```

## Choose Watch when you only need the latest state, fast

Reach for `Watch` (the `Watch` sample) when the consumer only cares about the **current value**
and a brief gap during a disconnect is acceptable — it re-reads the latest state when it comes
back anyway:

- **Live dashboards / UIs** — show the newest value; a skipped intermediate value doesn't matter.
- **Cache freshness** — invalidate or refresh a hot entry when it changes.
- **Presence / heartbeats** — who's online right now.
- **Config / feature-flag reload** — pick up the new config; only the latest wins.

```csharp
var zaris = (IZaris)client;
var sub = await zaris.Watch.WatchPrefixAsync(
    "config/",
    new WatchOptions { IncludeInitialSnapshot = true },   // prime with current state, then live
    ev => cache[ev.Key] = ev.Value);                      // react to the latest value
```

Watch is **live-from-now**: it does not replay history and does not track a durable cursor, so
never use it where a dropped event is a correctness bug — use CDC there.

## Still unsure?

Ask: *"If my consumer is down for a minute and misses a change, is that a bug?"*

- **Yes** → CDC. You need durability and replay.
- **No, I'll just read the latest when I'm back** → Watch.

## See also

- Samples: [`../Samples/Cdc.AuditLog`](../Samples/Cdc.AuditLog) (start here),
  [`../Samples/Cdc.CacheInvalidation`](../Samples/Cdc.CacheInvalidation),
  [`../Samples/Watch`](../Samples/Watch)
- [clustron.io/change-data-capture](https://clustron.io/change-data-capture)

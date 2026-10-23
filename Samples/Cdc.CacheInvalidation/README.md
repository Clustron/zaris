# CDC Sample — Cache Invalidation

Demonstrates Clustron Zaris **Change Data Capture (CDC)**: a console app that consumes the durable,
ordered **change feed** (`IZarisChangeFeedClient.OpenChangeFeed(...)`) to keep a local in-process
cache coherent with the store.

## What it shows

| Step | API |
|------|-----|
| Get the client, as a feed consumer | `(IZarisChangeFeedClient)client` |
| Open the change feed (filtered, resumable) | `feed.OpenChangeFeed(ChangeFeedOptions, ct)` |
| React to a write | `ev.Value` → refresh the cached bytes |
| React to a delete | `ev.IsDelete` → remove the entry |

For every mutation on the feed:

- **Put** (plain value) → refresh the cached bytes;
- **Put** (collection/counter, `ev.Value == null`) → drop the stale entry (re-read lazily);
- **Delete** → remove the entry.

Feed delivery is **at-least-once** and **per-key ordered**, so upsert-on-Put / remove-on-Delete is
naturally idempotent — a re-delivered event does no harm.

## Prerequisites

- A running Zaris **cluster/store** (this sample connects over the network; it does not run InProc).
- The **change feed enabled** on it: `changeFeed.enabled = true` in `clustron.json`, or start the
  node(s) with `ZARIS_WAN_SYNC_FEED=1`.
- The **Zaris 2.1.0+ SDK** — CDC is a 2.1.0 feature.

## Run it

```bash
# all keys
dotnet run -- "zaris://localhost:7861/mystore"

# only keys under a prefix (filtered server-side)
dotnet run -- "zaris://localhost:7861/mystore" "catalog/"
```

The connection string carries seeds, scheme (`zaris://` / `zariss://` for TLS), and an optional
token/CA, exactly as any Zaris client. You can also set `ZARIS_CONNECTION_STRING` instead of passing
arg[0]. Runs until **Ctrl+C**.

In another terminal, write and delete some keys (e.g. with the `Basic` sample or AdminShell) and
watch the invalidator react.

## Key points

- `ConsumerName = "catalog-cache"` makes it a **named consumer**: the server tracks its committed
  offset (for lag/liveness and best-effort resume after a restart). No manual cursor commit is
  needed — resume is automatic from the consumer name.
- `KeyPrefix` is pushed **server-side**, so filtered-out keys never cross the wire.
- If the consumer falls behind the retention window it transparently re-snapshots
  (`OutOfRange → snapshot-then-tail`) — current state is re-emitted as Puts.

See the [Change Data Capture](https://clustron.io/change-data-capture) overview for the concepts.

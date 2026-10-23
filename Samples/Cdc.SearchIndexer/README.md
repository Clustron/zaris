# CDC Sample — Secondary Index / Search Sync

Demonstrates Clustron Zaris **Change Data Capture (CDC)**: a console app that consumes the durable,
ordered **change feed** and projects every change into an in-memory inverted index — a stand-in for
keeping a real search engine or secondary lookup table in sync with the store.

## What it shows

| Step | API |
|------|-----|
| Get the client, as a feed consumer | `(IZarisChangeFeedClient)client` |
| Open the feed, filtered by prefix + op kind | `feed.OpenChangeFeed(ChangeFeedOptions, ct)` |
| Index a write | `ev.Value` → tokenize + index under `ev.Key` |
| Remove a document | `ev.IsDelete` → drop from the index |

- **Put** (plain string value) → tokenize the value and (re)index the document under its key;
- **Delete** → remove the document from the index.

The index is keyed by document key, and an upsert **removes old postings first**, so re-delivery of
an event (feed delivery is at-least-once) is an idempotent net no-op.

## Prerequisites

- A running Zaris **cluster/store** (this sample connects over the network; it does not run InProc).
- The **change feed enabled** (`changeFeed.enabled = true` or `ZARIS_WAN_SYNC_FEED=1`).
- The **Zaris 2.1.0+ SDK** — CDC is a 2.1.0 feature.

## Run it

```bash
dotnet run -- "zaris://localhost:7861/mystore" "docs/"
```

Write some string values under the `docs/` prefix in another terminal and watch the index grow;
delete them and watch it shrink. Runs until **Ctrl+C**.

## Key points

- `Kinds = { Put, Delete }` and `KeyPrefix` are pushed **server-side** — only matching ops cross the
  wire, and the cursor still advances past filtered ops so the tail never stalls.
- Only plain-string values are indexed here; collection/counter changes are skipped (inspect
  `ev.Entry` for those in a real indexer).
- A named consumer (`ConsumerName = "search-indexer"`) gets server-tracked lag and best-effort
  resume after a restart — no manual cursor commit needed.

See the [Change Data Capture](https://clustron.io/change-data-capture) overview for the concepts.

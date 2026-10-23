# 🚀 Clustron Zaris — CDC Audit Log Sample

**What this shows / why you'd use it.** This is the simplest, most useful form of **Change Data
Capture (CDC)**: a consumer that writes down **every change made to the store** — each `Put` and
`Delete`, with its key, kind, version (revision) and timestamp — as it happens. That running
record is exactly what you need for an **audit/change log, ETL/outbox, event sourcing, or feeding
a downstream system**, where *missing a change would be a bug*.

The thing that makes this CDC and not just "watching for changes" is that the consumer is
**durable and resumable**: it has a name, and the server remembers how far that name has read. So
if your process crashes or restarts, it **picks up exactly where it left off** instead of starting
over or dropping the changes it missed while it was down. This sample proves that on screen.

---

# 📌 What This Sample Demonstrates

- Opening a durable change-feed consumer: `IZarisChangeFeedClient.OpenChangeFeed(...)`
- Logging every mutation across a keyspace (`Put` / `Delete`, key, `Revision`, timestamp)
- A **server-side prefix filter** (`KeyPrefix`) and **kind filter** (`Kinds`)
- **Resume-on-restart**: a fixed `ConsumerName` continues from its last committed offset
- The contrast: a **fresh** consumer name **replays from the beginning**
- **At-least-once** delivery and idempotent handling (dedup by key + revision)

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

# ▶️ Run it — and watch it resume

Each run writes 6 changes under `audit/<runId>/…` (5 puts + 1 delete) and then logs what the feed
delivers to the durable consumer `audit-logger`.

### 1. First run

```bash
dotnet run
```

You'll see the 6 changes logged, each tagged `(this run)`, and:

```
RESUMED: the durable consumer 'audit-logger' continued from its last committed offset ...
```

### 2. Run it again — it resumes, it does NOT replay

```bash
dotnet run
```

It writes 6 **new** changes and logs **only those 6** — the 6 from the first run are **not**
replayed, because the consumer `audit-logger` committed its offset last time and resumes past it.
`from EARLIER runs : 0` is the proof. (You can repeat this as many times as you like; it always
continues from where it stopped.)

> Prefer a manual demo? Leave the first run tailing, press **Ctrl+C** to stop it, write some keys
> under `audit/…` from another terminal (e.g. with the **Basic** sample or AdminShell), then run
> this sample again — it logs only what was written while it was away.

### 3. Compare with a fresh consumer — it replays everything

```bash
dotnet run -- --fresh true
```

A brand-new consumer name reads the **whole retained feed from the start**, so now you'll see the
changes from **all** previous runs too (`from EARLIER runs : > 0`), ending with:

```
REPLAYED: consumer 'audit-adhoc-...' re-read N change(s) from earlier runs, from the start ...
```

That is the difference between **resuming** (fixed, durable name) and **replaying from scratch**
(fresh name) — and why a durable `ConsumerName` is what makes CDC safe across restarts.

---

# 🧠 How Resume Works

- `ConsumerName` is a **durable identity** on the server. As the consumer reads, the server commits
  its offset for that name — no manual cursor bookkeeping in your app.
- Reopen the feed with the **same name** → it starts **after** the last committed offset (resume).
- Open it with a **new name** → the server has no offset for it, so it starts from the **beginning**
  of the retained feed (replay). Use this to rebuild state from scratch.
- Delivery is **at-least-once**: after a crash a few already-seen events can be redelivered, so
  apply idempotently. This sample dedups by `key + kind + revision`.

---

# 📦 Summary

| | Value |
|---|---|
| API | `IZarisChangeFeedClient.OpenChangeFeed(ChangeFeedOptions, ct)` |
| Durability | Durable, resumable by `ConsumerName`; at-least-once |
| Good for | Audit/change logs, ETL/outbox, event sourcing, replication |
| Needs | A running cluster with the change feed enabled (not InProc) |

The demo intentionally **keeps the data it writes** across runs (a few keys per run under
`audit/`) — that persistence is what lets the next run prove it resumed. Clear the `audit/` prefix
with the **Basic** sample or AdminShell when you want a clean slate.

See **[../../docs/cdc-vs-watch.md](../../docs/cdc-vs-watch.md)** for when to use CDC vs the Watch API.

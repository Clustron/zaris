# 🚀 Clustron Zaris — Batch Sample

This sample demonstrates the mixed **batch** API (`ExecuteBatchAsync`): a single request that
carries a mix of PUT / GET / DELETE operations, dispatched in one round-trip per owning node.

It is distinct from the **Bulk** sample (`PutMany` / `GetMany` / `DeleteMany`), which handles one
operation kind at a time.

---

# 📌 What This Sample Demonstrates

- Build a `KvBatchRequest` from `KvBatchItem`s (`Op`, `Key`, `PutValue`)
- Execute a write batch (multiple PUTs)
- Execute a mixed batch (GET + DELETE + GET) in a single call
- Read per-item results (`Index`, `Op`, `Success`, `Status`, `ValueObject`)

---

# 🚀 Quick Start

Runs fully in-process — no cluster required. `appsettings.json`:

```json
{
  "ConnectionStrings": {
    "teststore": "zaris://inproc/teststore"
  }
}
```

Run:

```bash
dotnet run --project Batch
```

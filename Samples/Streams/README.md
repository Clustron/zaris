# 🚀 Clustron Zaris — Streams Sample

This sample demonstrates the native **Streams** API in Clustron Zaris (`IZaris.Streams` /
`IStreamClient`): an append-only, replicated event log with **consumer groups** — the same model
as Redis Streams, reached through the typed .NET client.

---

# 📌 What This Sample Demonstrates

- Append events to a stream (`AddAsync`, auto-generated IDs)
- Inspect the log (`LengthAsync`, `RangeAsync`, `ReadAsync`)
- Process events through a **consumer group** (`GroupCreateAsync`, `ReadGroupAsync`)
- Acknowledge processed messages (`AckAsync`)
- Read stream metadata (`InfoAsync`)
- Clean up created keys

Stream entry fields are `byte[]` — the sample encodes/decodes them with `Encoding.UTF8`.

---

# 🚀 Quick Start

The sample runs fully in-process — no cluster required. `appsettings.json`:

```json
{
  "ConnectionStrings": {
    "teststore": "zaris://inproc/teststore"
  }
}
```

Run:

```bash
dotnet run --project Streams
```

To run against a real cluster instead, point the connection string at your nodes, e.g.
`zaris://host1:7861,host2:7861/teststore` (or `zariss://…` for TLS).

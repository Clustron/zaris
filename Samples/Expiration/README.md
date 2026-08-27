# 🚀 Clustron Zaris — Expiration (TTL) Sample

This sample demonstrates **time-to-live (TTL)** management on the core Clustron Zaris client.

---

# 📌 What This Sample Demonstrates

- A freshly written key is persistent (no TTL)
- Attach a TTL to an existing key (`ExpireAsync`)
- Read the remaining TTL (`GetTimeToLiveAsync`)
- Clear a TTL so the key becomes permanent again (`PersistAsync`)
- Watch a key disappear once its TTL elapses

All of these are on the base `IZarisClient` — no cast to `IZaris` is needed.

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
dotnet run --project Expiration
```

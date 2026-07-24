# 🚀 Clustron Dictus — Watch Sample

This sample demonstrates how to use the **Watch API** in Clustron Dictus to build **real-time, reactive distributed systems**.

Instead of polling for changes, your application can subscribe to updates and react instantly when data changes.

---

# 📌 What This Sample Demonstrates

This sample shows how to:

- Subscribe to a single key
- Subscribe to a key prefix (multiple keys)
- Receive real-time updates (create/update/delete)
- Use initial snapshot on subscription
- Track event revisions
- React to live updates
- Gracefully stop watchers
- Clean up data safely

---

# 🚀 Quick Start (Recommended)

The fastest way to run this sample is using **InProc mode**.

This runs Clustron Dictus **inside your application** — no servers, no setup.

```json
{
  "Dictus": {
    "Stores": {
      "teststore": {
        "Mode": "InProc"
      }
    }
  }
}
```

Now run:

```bash
dotnet run
```

✅ That’s it — the sample will run instantly.

---

# 🧠 How the Sample Works

This sample uses Clustron’s **provider-based model**.

```csharp
var client = await _provider.GetAsync("teststore");
```

- You request a client for a store  
- Configuration is applied automatically  
- Connections are handled internally  

You focus on logic — Clustron handles infrastructure.

---

# 🌐 Run with Real Cluster (Next Step)

Once you're comfortable, you can switch to a real distributed setup using **Remote mode**:

```json
{
  "Dictus": {
    "Stores": {
      "teststore": {
        "Mode": "Remote",
        "Seeds": [
          { "Host": "127.0.0.1", "Port": 7681 }
        ]
      }
    }
  }
}
```

Before running:

- Ensure Dictus servers are running  
- Ensure the store exists  
- Ensure ports match  

👉 Full setup guide:  
https://clustron.io/docs/clustron/dictus/getting-started/overview/

---

# 💡 Learning Path

- Start with **InProc** → understand APIs quickly  
- Move to **Remote** → run distributed workloads  

---

# 🧠 How Watch Works

The Watch API lets you subscribe to:

- A single key
- A key prefix

Events are triggered when:

- Key is created
- Key is updated
- Key is deleted

Each event includes:

- Event type
- Key
- Revision
- Value (if available)

---

# 🔄 Sample Flow

## Start Watchers

- Watch a single key
- Watch a prefix

## Simulate Updates

- Background updates
- Occasional deletes
- Real-time event stream

## Stop Watchers

- Stop subscriptions
- Print summary

---

# 📊 Key Features

- Real-time notifications
- Event-driven architecture
- Revision tracking
- Prefix subscriptions
- Snapshot support

---

# 📦 Summary

This sample demonstrates how Clustron Dictus enables:

- Reactive systems
- Event-driven microservices
- Live dashboards
- Cache invalidation
- Distributed coordination

Clustron is a **reactive distributed platform**, not just a key-value store.

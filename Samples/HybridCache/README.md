# 🚀 Clustron DKV — HybridCache Sample

This sample demonstrates how to use **Clustron HybridCache (L1 + L2 caching)** to achieve high performance with consistency.

It combines:
- **L1 (InProc cache)** → ultra-fast local reads  
- **L2 (Clustron DKV)** → distributed, consistent storage  

---

# 📌 What This Sample Demonstrates

This sample shows how to:

- Use HybridCache with Clustron DKV
- Configure L1 (local) and L2 (distributed) cache
- Use GetOrCreate pattern
- Observe L1 cache hits
- Observe L2 fallback after L1 expiry
- Remove cache entries
- Invalidate by tags
- Use TTL for cache expiration
- Clean up cache entries

---

# 🚀 Quick Start (Recommended)

Run instantly using **InProc mode**:

```json
{
  "Dkv": {
    "Stores": {
      "teststore": {
        "Mode": "InProc"
      }
    }
  }
}
```

Run:

```bash
dotnet run
```

✅ No setup required — runs immediately.

---

# 🧠 How the Sample Works

HybridCache uses two layers:

- **L1 (Local Cache)** → fast, in-memory, per-instance  
- **L2 (Clustron DKV)** → shared, distributed cache  

The client is resolved using Clustron’s provider model:

```csharp
var client = await _provider.GetAsync("teststore");
```

HybridCache automatically:
- Reads from L1 first  
- Falls back to L2  
- Updates both layers  

---

# 🌐 Run with Real Cluster (Next Step)

Switch to **Remote mode** for real distributed caching:

```json
{
  "Dkv": {
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

- Ensure DKV servers are running  
- Ensure the store exists  
- Ensure ports match  

👉 Full setup guide:  
https://clustron.io/docs/clustron/dkv/getting-started/overview/

---

# 💡 Learning Path

- Start with **InProc** → understand HybridCache behavior  
- Move to **Remote** → use in distributed environments  

---

# 🧠 How HybridCache Works

## First Access (Cache Miss)

```csharp
await cache.GetOrCreateAsync(key, factory);
```

- Factory executes  
- Value stored in L2  
- Value stored in L1  

---

## L1 Hit

- Returned instantly from local memory  
- No network call  

---

## L2 Hit (after L1 expiry)

- L1 expired  
- Value fetched from L2  
- L1 refreshed  

---

## Remove

```csharp
await cache.RemoveAsync(key);
```

- Removes from both L1 and L2  

---

## Tag Invalidation

```csharp
await cache.RemoveByTagAsync("products");
```

- Invalidates all related entries  
- Works across distributed nodes  

---

## TTL Expiry

- Entry expires automatically  
- Factory runs again on next access  

---

# 📊 Key Features

- Two-level caching (L1 + L2)  
- High performance (local reads)  
- Distributed consistency  
- Tag-based invalidation  
- TTL-based expiration  
- Minimal application changes  

---

# 📦 Summary

This sample demonstrates how Clustron DKV enables:

- High-performance caching  
- Reduced latency  
- Distributed consistency  
- Smart cache invalidation  

Use HybridCache for:

- High-traffic applications  
- Microservices  
- Read-heavy workloads  
- Performance-critical systems  

Clustron HybridCache gives you **the best of both worlds: speed of local cache + power of distributed cache**.

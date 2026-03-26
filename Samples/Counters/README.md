# 🚀 Clustron DKV — Counters Sample

This sample demonstrates how to use **Distributed Counters** in Clustron DKV.

It showcases atomic numeric operations that are safe, consistent, and cluster-aware.

---

# 📌 What This Sample Demonstrates

This sample shows how to:

- Perform atomic increments  
- Retrieve current counter values  
- Set counter values explicitly  
- Enforce Min / Max bounds  
- Apply TTL (time-to-live) to counters  
- Clean up created keys  

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

This sample uses Clustron’s **provider-based model**:

```csharp
var client = await _provider.GetAsync("teststore");
```

- Client is resolved automatically  
- Configuration is applied internally  
- No manual connection handling  

---

# 🌐 Run with Real Cluster (Next Step)

Switch to **Remote mode** to run in a real distributed setup:

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

- Start with **InProc** → understand counters quickly  
- Move to **Remote** → use counters across nodes  

---

# 🧠 Counter Features Demonstrated

## Atomic Increment

```csharp
await counters.AddAsync(key, delta);
```

Ensures atomic updates across the cluster.

---

## Get Counter Value

```csharp
await counters.GetAsync(key);
```

Returns the current value safely across nodes.

---

## Set Counter Value

```csharp
await counters.SetAsync(key, value);
```

Overrides the counter value atomically.

---

## Min / Max Bounds

```csharp
new CounterOptions { MaxValue = 10 }
```

Prevents exceeding defined limits.

---

## Counter TTL

```csharp
new CounterOptions { Ttl = TimeSpan.FromSeconds(20) }
```

Automatically removes the counter after expiry.

---

# 📊 Key Features

- Atomic operations  
- Cluster-wide consistency  
- Bound enforcement  
- TTL-based expiration  

---

# 📦 Summary

This sample demonstrates how Clustron DKV counters are:

- Atomic  
- Distributed  
- Safe under concurrency  
- TTL-enabled  

Use counters for:

- Rate limiting  
- Metrics tracking  
- Distributed counting  
- Usage tracking  

Clustron DKV provides **simple and reliable distributed counters for real-world systems**.

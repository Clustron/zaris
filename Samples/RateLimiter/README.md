# 🚀 Clustron Zaris — Distributed Rate Limiter Sample

This sample demonstrates how to implement a **distributed fixed-window rate limiter** using Clustron Zaris counters.

It simulates requests and enforces a maximum limit within a time window.

---

# 📌 What This Sample Demonstrates

This sample shows how to:

- Use distributed counters to track requests
- Apply TTL to reset time windows automatically
- Enforce request limits
- Simulate request traffic
- Build distributed-safe rate limiting

---

# 🚀 Quick Start (Recommended)

Run instantly using **InProc mode** (no setup required):

```json
{
  "Zaris": {
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

✅ No servers required — works out of the box.

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
  "Zaris": {
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

- Ensure Zaris servers are running  
- Ensure the store exists  
- Ensure ports match  

👉 Full setup guide:  
https://clustron.io/docs/clustron/zaris/getting-started/overview/

---

# 💡 Learning Path

- Start with **InProc** → understand rate limiting  
- Move to **Remote** → enforce limits across nodes  

---

# 🧠 How the Rate Limiter Works

This sample implements a **fixed time window rate limiter**.

Configuration:

- Max Requests: 5  
- Window Duration: 10 seconds  

---

## 🔄 Flow

1. Generate a key for the current time window  
2. Increment a distributed counter  
3. Attach TTL equal to window duration  
4. If count exceeds limit → block request  
5. TTL expiry resets the window  

---

# 📊 Key Features

- Atomic counters  
- TTL-based expiration  
- Cluster-wide consistency  
- Simple distributed enforcement  

---

# 📦 Summary

This sample demonstrates how Clustron Zaris enables:

- Distributed rate limiting  
- API throttling  
- Abuse protection  
- Request quota enforcement  

Counters + TTL provide a **simple and powerful pattern** for distributed control.

# 🚀 Clustron Zaris — Leader Election via Lease Sample

This sample demonstrates how to implement **distributed leader election** using Clustron Zaris leases and watch APIs.

It simulates multiple nodes competing for leadership and automatically handling failures and re-election.

---

# 📌 What This Sample Demonstrates

This sample shows how to:

- Simulate multiple competing nodes  
- Use leases to claim leadership  
- Ensure a single leader using `Put.IfAbsent()`  
- Maintain leadership with KeepAlive  
- Simulate node failures  
- Detect leader loss using Watch API  
- Automatically trigger re-election  
- Clean up created keys  

---

# 🚀 Quick Start (Recommended)

Run instantly using **InProc mode**:

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

- Start with **InProc** → understand leader election  
- Move to **Remote** → coordinate across nodes  

---

# 🧠 How Leader Election Works

This sample simulates multiple nodes competing for leadership.

Each node:

1. Requests a lease  
2. Attempts to acquire leadership using `Put.IfAbsent().WithLease()`  
3. If successful → becomes leader  
4. Sends KeepAlive to maintain leadership  
5. Simulates failure after a few seconds  
6. Other nodes detect leader loss via Watch  
7. Re-election begins automatically  

---

# 🔄 Failure & Recovery Flow

- Leader crashes → lease expires  
- Leader key is removed  
- Watchers detect deletion  
- Other nodes retry election  
- New leader is elected  

This ensures **automatic failover without central coordination**.

---

# 📊 Key Features

- Lease-based leadership  
- Single-writer guarantee  
- Automatic failover  
- Watch-driven reactivity  
- Distributed coordination  

---

# 📦 Summary

This sample demonstrates how Clustron Zaris enables:

- Leader election  
- Fault tolerance  
- Distributed coordination  
- Automatic recovery  

Use this pattern for:

- Master election  
- Distributed schedulers  
- Primary node selection  
- Coordination services  

Clustron Zaris provides a **simple and reliable foundation for distributed coordination**.

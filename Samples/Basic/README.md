# 🚀 Clustron Dictus — Basic Sample

This sample demonstrates the core **Clustron Dictus Client SDK programming model**.

It is the **recommended starting point** for learning how to work with Dictus.

---

# 📌 What This Sample Demonstrates

This sample shows how to:

- Store an object (PUT)  
- Retrieve an object (GET)  
- Read metadata (TTL, labels, content type)  
- Use distributed counters  
- Observe TTL expiration  
- Clean up created keys  

---

# 🚀 Quick Start (Recommended)

Run instantly using **InProc mode**:

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

Switch to **Remote mode**:

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

👉 Full setup guide:  
https://clustron.io/docs/clustron/dictus/getting-started/overview/

---

# 💡 Learning Path

- Start with **InProc** → understand basic APIs  
- Move to **Remote** → run in real cluster  

---

# 🔄 What the Sample Does

1. Stores a `Customer` object with:
   - TTL (30 seconds)  
   - Content type (`application/json`)  
   - Labels (`env=demo`, `sample=basic`)  

2. Retrieves the object and prints:
   - Value  
   - Metadata  

3. Demonstrates:
   - Distributed counters  
   - TTL expiration  

4. Cleans up all keys  

---

# 📊 Key Concepts

- Key-value storage  
- Metadata (TTL, labels, content type)  
- Distributed counters  
- Expiration behavior  

---

# 📦 Summary

| Mode   | Server Required | Use Case              |
|--------|----------------|----------------------|
| InProc | No             | Learning & testing   |
| Remote | Yes            | Production scenarios |

---

This sample provides the **foundation for building distributed systems using Clustron Dictus**.

Start here before moving to advanced samples like:

- CAS  
- Transactions  
- Watch  
- Leases  
- Distributed queues  

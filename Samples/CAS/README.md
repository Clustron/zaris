# 🚀 Clustron Zaris — Compare-And-Swap (CAS) Sample

This sample demonstrates how to use **optimistic concurrency control** in Clustron Zaris using Compare-And-Swap (CAS).

It shows how to safely update and delete items using version-based conditional operations.

---

# 📌 What This Sample Demonstrates

This sample shows how to:

- Insert items using IfAbsent  
- Retrieve values along with versions  
- Perform conditional updates using IfMatch  
- Handle conflicts safely  
- Perform conditional deletes  
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

Switch to **Remote mode**:

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

👉 Full setup guide:  
https://clustron.io/docs/clustron/zaris/getting-started/overview/

---

# 💡 Learning Path

- Start with **InProc** → understand CAS behavior  
- Move to **Remote** → handle real concurrency  

---

# 🧠 What is CAS?

CAS (Compare-And-Swap) ensures that an operation succeeds **only if the version matches**.

This prevents lost updates when multiple clients modify the same data.

---

# 🧪 Sample Flow

1. Insert item using `Put.IfAbsent()`  
2. Read value + version  
3. Update using `Put.WithIfMatch(version)` → ✅ success  
4. Update again using old version → ❌ conflict  
5. Delete using `Delete.IfMatch(version)`  
6. Verify deletion  

---

# 📊 Key Features

- Optimistic concurrency  
- Version-based safety  
- Conflict detection  
- Safe deletes  

---

# 📦 Summary

This sample demonstrates how Clustron Zaris enables:

- Safe concurrent updates  
- Conflict-aware operations  
- Version-controlled data access  

Use CAS for:

- Financial updates  
- Inventory systems  
- Distributed coordination  
- Any critical state changes  

Clustron Zaris provides **simple yet powerful concurrency control for distributed systems**.

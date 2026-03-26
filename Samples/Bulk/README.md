# 🚀 Clustron DKV — Bulk Operations Sample

This sample demonstrates how to use **high-performance bulk operations** in Clustron DKV.

It showcases batch-based PUT, GET, and DELETE using optimized client APIs.

---

# 📌 What This Sample Demonstrates

This sample shows how to:

- Insert multiple objects using `PutManyAsync`  
- Retrieve multiple objects using `GetManyAsync`  
- Delete multiple objects using `DeleteManyAsync`  
- Verify results  
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
- No manual connection required  

---

# 🌐 Run with Real Cluster (Next Step)

Switch to **Remote mode**:

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

👉 https://clustron.io/docs/clustron/dkv/getting-started/overview/

---

# 💡 Learning Path

- Start with **InProc** → understand bulk APIs  
- Move to **Remote** → scale batch operations  

---

# 🧪 Sample Flow

1. Insert multiple customers using `PutManyAsync`  
2. Retrieve all using `GetManyAsync`  
3. Delete all using `DeleteManyAsync`  
4. Verify results  

---

# 📊 Key Features

- Batch operations  
- Reduced network overhead  
- High throughput  
- Efficient data access  

---

# 📦 Summary

| Operation   | API              |
|------------|------------------|
| Bulk PUT   | PutManyAsync     |
| Bulk GET   | GetManyAsync     |
| Bulk DELETE| DeleteManyAsync  |

This sample demonstrates how Clustron DKV enables:

- Efficient batch processing  
- High-performance data operations  
- Scalable distributed workloads  

Use bulk APIs for:

- Data ingestion  
- Batch processing  
- ETL pipelines  
- High-throughput services  

Clustron DKV provides **fast and efficient bulk operations for modern distributed systems**.

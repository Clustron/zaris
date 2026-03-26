# 🚀 Clustron DKV — Transaction Sample

This sample demonstrates how to use **Transactions in Clustron DKV** to perform **atomic multi-key operations**.

It shows how applications can safely modify multiple keys while maintaining **data consistency**.

---

# 📌 What This Sample Demonstrates

This sample shows how to:

- Start a transaction
- Read values inside a transaction
- Modify multiple keys atomically
- Commit a transaction
- Roll back a transaction
- Detect conflicts from concurrent updates
- Delete keys inside a transaction
- Observe read-your-writes behavior
- Clean up created keys

---

# 🚀 Quick Start (Recommended)

The fastest way to run this sample is using **InProc mode**.

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

✅ No setup required — runs instantly.

---

# 🧠 How the Sample Works

This sample uses Clustron’s **provider-based model**:

```csharp
var client = await _provider.GetAsync("teststore");
```

- You request a client for a store  
- Configuration is applied automatically  
- Connections are handled internally  

---

# 🌐 Run with Real Cluster (Next Step)

To run against a real distributed cluster, switch to **Remote mode**:

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

- Start with **InProc** → understand transactions quickly  
- Move to **Remote** → run distributed workloads  

---

# 🧠 How Transactions Work

Clustron DKV uses **optimistic multi-key transactions**.

A transaction:

1. Reads keys and tracks their versions  
2. Applies changes locally  
3. Attempts to commit  

During commit:

- If no keys changed → ✅ success  
- If any key changed → ❌ conflict  

---

# 🔄 Sample Flow

## Initialize Data

```
keyA = 10
keyB = 20
```

---

## Successful Transaction

```
TX START
  GET A = 10
  GET B = 20
  PUT A = 15
  PUT B = 25
COMMIT
```

Result:

```
A = 15
B = 25
```

---

## Rollback Example

```
TX START
  PUT A = 999
ROLLBACK
```

Result:

```
A unchanged
```

---

## Conflict Example

```
TX START
  GET A = 15

External update:
  PUT A = 500

TX COMMIT → fails
```

---

## Delete Inside Transaction

```
TX START
  DELETE B
COMMIT
```

Result:

```
B removed
```

---

# 📊 Key Features

- Atomic multi-key updates  
- Conflict detection  
- Rollback support  
- Read-your-writes  
- Distributed consistency  

---

# 📦 Summary

This sample demonstrates how Clustron DKV enables:

- Reliable multi-key operations  
- Safe distributed updates  
- Consistent application state  

Use transactions to build:

- Financial systems  
- Order processing  
- Inventory management  
- Distributed coordination  

Clustron DKV provides **simple APIs with strong consistency guarantees**.

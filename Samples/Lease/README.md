# 🚀 Clustron DKV — Lease Sample (Expiry & Revoke Validation)

This sample demonstrates how to use **Leases** in Clustron DKV to manage **time-bound ownership of keys**.

It validates both automatic lease expiry and explicit lease revocation behavior.

---

# 📌 What This Sample Demonstrates

This sample shows how to:

- Grant a time-bound lease
- Attach multiple keys to a lease
- Observe automatic deletion on expiry
- Use Watch API to detect deletions
- Explicitly revoke a lease
- Compare expiry vs revoke behavior
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

- Start with **InProc** → understand leases quickly  
- Move to **Remote** → use leases in distributed systems  

---

# 🧠 How Leases Work

A lease represents **time-bound ownership** of keys.

When a key is written with a lease:

- It is automatically deleted when the lease expires  
- It is immediately deleted when the lease is revoked  

---

# 🔄 Sample Flow

## Lease Expiry Test

- Grant a lease (10 seconds)  
- Insert keys bound to the lease  
- Attach Watch to observe deletion  
- Wait for expiry  
- Verify automatic cleanup  

---

## Explicit Revoke Test

- Grant another lease (30 seconds)  
- Insert keys bound to lease  
- Revoke lease manually  
- Verify immediate deletion  

---

# 📊 Key Features

- Time-bound ownership  
- Automatic cleanup via TTL  
- Explicit revoke support  
- Watch integration  
- Distributed coordination  

---

# 📦 Summary

This sample demonstrates how Clustron DKV enables:

- Automatic resource cleanup  
- Lease-based coordination  
- Ephemeral key management  

Use leases for:

- Distributed locks  
- Session management  
- Temporary ownership  
- Coordination patterns  

Clustron DKV provides a **simple and powerful lease model for distributed systems**.

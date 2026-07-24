# 🚀 Clustron Dictus — Simplified Enterprise Job Queue Sample

This sample demonstrates how to build a **distributed, fault-tolerant job queue** using Clustron Dictus primitives.

It simulates a small enterprise-style processing system with producers, multiple workers, optimistic concurrency, leases, and recovery logic.

---

# 📌 What This Sample Demonstrates

This sample shows how to:

- Produce jobs with entity + labels  
- Fetch work using search queries  
- Use CAS (Compare-And-Swap) for safe state transitions  
- Use leases for worker-level locking  
- Simulate worker failures  
- Recover orphaned jobs  
- Track completion state  
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

Switch to **Remote mode** to run in a real distributed setup:

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

- Start with **InProc** → understand queue behavior  
- Move to **Remote** → run across multiple nodes  

---

# 🧠 Architecture Overview

This sample simulates:

- 1 Producer  
- 3 Workers  
- 10 Jobs  

Each job transitions through states:

```
pending → processing → completed
```

State transitions are protected using:

- CAS (version matching)  
- Lease-based locking  
- Recovery logic  

---

# 🔄 How Job Processing Works

## Job Creation

Jobs are stored with:

- Entity: `job`  
- Label: `status=pending`  

---

## Worker Execution

Each worker:

1. Acquires a lease  
2. Searches for `status=pending` jobs  
3. Uses CAS to transition to `processing`  
4. Creates a lease-backed lock key  
5. Processes the job  
6. Marks job as `completed` using CAS  

---

## Failure Simulation

Workers randomly fail some jobs.

On failure:

- Job status reverts to `pending`  
- Lock is removed  
- Another worker can retry  

---

## Recovery Logic

If a worker crashes:

- Jobs may remain in `processing`  
- Lock may be missing  
- Another worker detects and resets job to `pending`  

This demonstrates **self-healing distributed queue behavior**.

---

# 📊 Key Features

- Entity-based modeling  
- Label-based state tracking  
- Distributed search for work  
- CAS for safe updates  
- Lease-based locking  
- Automatic recovery  

---

# 📦 Summary

This sample demonstrates how Clustron Dictus enables:

- Distributed job queues  
- Fault-tolerant workers  
- Optimistic concurrency workflows  
- Self-healing systems  

Use this pattern for:

- Background processing  
- Task queues  
- Workflow engines  
- Distributed job orchestration  

Clustron Dictus provides **powerful primitives to build reliable distributed systems with minimal complexity**.

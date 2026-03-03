# 🚀 Clustron DKV --- Simplified Enterprise Job Queue Sample

This sample demonstrates how to build a **distributed, fault-tolerant
job queue** using Clustron DKV primitives.

It simulates a small enterprise-style processing system with producers,
multiple workers, optimistic concurrency, leases, and recovery logic.

------------------------------------------------------------------------

# 📌 What This Sample Demonstrates

This sample performs the following operations:

-   Connect to a DKV cluster (InProc or Remote)
-   Produce jobs with entity + labels
-   Use search queries to fetch pending work
-   Use CAS (Compare-And-Swap) for safe state transitions
-   Use leases for worker-level locking
-   Simulate worker failures
-   Recover orphaned jobs
-   Track authoritative completion state
-   Clean up created keys

------------------------------------------------------------------------

# ⚙️ Configuration

All samples use a unified configuration structure via
`appsettings.json`.

## 🧠 Configuration Schema

``` json
{
  "Dkv": {
    "ClusterId": "string",
    "Mode": "InProc | Remote",
    "Seeds": [
      {
        "Host": "string",
        "Port": number
      }
    ],
    "LogFilePath": "string | null"
  }
}
```

------------------------------------------------------------------------

## 🔹 ClusterId

The **Store ID** created in DKV.

This must match the store name defined when creating your cluster store.

Example:

``` json
"ClusterId": "teststore"
```

------------------------------------------------------------------------

## 🔹 Mode

  Value    Description
  -------- -----------------------------------------------
  InProc   Embedded in-memory store (no server required)
  Remote   Connects to external DKV server nodes

------------------------------------------------------------------------

## 🔹 Seeds

A list of one or more DKV server nodes.

Only one seed is required. After connecting, the client automatically:

-   Discovers cluster topology\
-   Connects to all nodes\
-   Handles failover\
-   Manages topology updates

Example:

``` json
"Seeds": [
  { "Host": "127.0.0.1", "Port": 7070 }
]
```

------------------------------------------------------------------------

## 🔹 LogFilePath (Optional)

Specifies where client logs are written.

``` json
"LogFilePath": "logs/dkv.log"
```

Use `null` to disable file logging.

------------------------------------------------------------------------

# 🏃 Running the Sample

``` bash
dotnet run
```

------------------------------------------------------------------------

# 🧪 Running in InProc Mode (No Server Required)

``` json
{
  "Dkv": {
    "ClusterId": "sample-cluster",
    "Mode": "InProc",
    "Seeds": [],
    "LogFilePath": null
  }
}
```

Use InProc mode for:

-   API exploration\
-   Unit testing\
-   Local development\
-   CI environments

------------------------------------------------------------------------

# 🌐 Running in Remote Mode (Real Cluster)

``` json
{
  "Dkv": {
    "ClusterId": "teststore",
    "Mode": "Remote",
    "Seeds": [
      { "Host": "127.0.0.1", "Port": 7070 }
    ],
    "LogFilePath": null
  }
}
```

Before running:

-   Ensure DKV servers are running\
-   Ensure the store exists\
-   Ensure the port matches the configured `ClientPort`

------------------------------------------------------------------------

# 🧠 Architecture Overview

This sample simulates:

-   1 Producer
-   3 Workers
-   10 Jobs

Each job transitions through states:

`pending → processing → completed`

State transitions are protected by:

-   CAS (version matching)
-   Lease-based locking
-   Recovery logic

------------------------------------------------------------------------

# 🔄 How Job Processing Works

##  Job Creation

Jobs are stored with:

-   Entity: `job`
-   Label: `status=pending`

------------------------------------------------------------------------

##  Worker Execution

Each worker:

1.  Acquires a lease\
2.  Searches for `status=pending` jobs\
3.  Uses CAS to transition to `processing`\
4.  Creates a lease-backed lock key\
5.  Simulates processing\
6.  Marks job as `completed` using CAS

------------------------------------------------------------------------

## Failure Simulation

Workers randomly fail some jobs.

On failure:

-   Job status reverts to `pending`
-   Lock is removed
-   Another worker can retry

------------------------------------------------------------------------

##  Recovery Logic

If a worker crashes:

-   Jobs stuck in `processing`
-   No lock present
-   Worker detects and restores job to `pending`

This demonstrates **self-healing distributed queue behavior**.

------------------------------------------------------------------------

# 📊 Key DKV Features Used

  Feature          Purpose
  ---------------- ----------------------------------
  Entities         Logical job grouping
  Labels           Job state tracking
  Search           Fetch pending work
  CAS              Safe state transitions
  Leases           Worker-level locking
  TTL              (Optional in advanced scenarios)
  Prefix cleanup   Safe sample isolation

------------------------------------------------------------------------

# 📦 Summary

This sample demonstrates how Clustron DKV can be used to build:

-   Distributed job queues
-   Fault-tolerant workers
-   Optimistic concurrency workflows
-   Self-healing recovery systems

It models real-world enterprise processing patterns using core DKV
primitives.

# 🚀 Clustron DKV --- Leader Election via Lease Sample

This sample demonstrates how to implement **distributed leader
election** using Clustron DKV leases and watch APIs.

It simulates multiple nodes competing to become the leader while
handling failures and automatic re-election.

------------------------------------------------------------------------

# 📌 What This Sample Demonstrates

This sample performs the following operations:

-   Connect to a DKV cluster (InProc or Remote)
-   Simulate multiple competing nodes
-   Use leases to claim leadership
-   Use `Put.IfAbsent()` to ensure a single leader
-   Use KeepAlive to maintain leadership
-   Simulate node crashes
-   Use Watch API to detect leader loss
-   Automatically trigger re-election
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

# 🧠 How Leader Election Works

This sample simulates 3 nodes competing for leadership.

Each node:

1.  Requests a lease (`GrantAsync`)\
2.  Attempts to write the leader key using `Put.IfAbsent().WithLease()`\
3.  If successful → becomes leader\
4.  Sends periodic `KeepAlive` calls to maintain leadership\
5.  Simulates crash after a few seconds\
6.  Other nodes detect deletion via Watch API\
7.  Re-election automatically begins

------------------------------------------------------------------------

# 🔄 Failure & Recovery Flow

-   If leader crashes → lease expires\
-   Leader key is deleted\
-   Watchers detect deletion event\
-   Waiting nodes retry election\
-   New leader is elected

This ensures **automatic failover without central coordination**.

------------------------------------------------------------------------

# 📊 Key DKV Features Used

  Feature          Purpose
  ---------------- -------------------------
  Leases           Time-bound leadership
  IfAbsent         Single-writer guarantee
  KeepAlive        Maintain leadership
  Watch API        Detect leader loss
  Prefix cleanup   Safe sample isolation

------------------------------------------------------------------------

# 📦 Summary

This sample demonstrates how Clustron DKV enables:

-   Distributed coordination
-   Automatic leader election
-   Crash recovery
-   Lease-based ownership
-   Watch-driven reactivity

It models real-world coordination patterns such as:

-   Master election
-   Distributed schedulers
-   Primary node selection
-   Cluster coordination services

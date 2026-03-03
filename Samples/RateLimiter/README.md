# 🚀 Clustron DKV --- Distributed Rate Limiter Sample

This sample demonstrates how to implement a **distributed fixed-window
rate limiter** using Clustron DKV counters.

It simulates multiple requests and enforces a maximum request limit
within a time window.

------------------------------------------------------------------------

# 📌 What This Sample Demonstrates

This sample performs the following operations:

-   Connect to a DKV cluster (InProc or Remote)
-   Use distributed counters to track request counts
-   Apply TTL to automatically reset time windows
-   Enforce a fixed request limit
-   Simulate request traffic
-   Demonstrate distributed-safe rate limiting

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

# 🧠 How the Rate Limiter Works

This sample implements a **fixed time window rate limiter**.

Configuration in code:

-   Max Requests: 5\
-   Window Duration: 10 seconds

------------------------------------------------------------------------

## 🔄 Flow

1.  Compute the current time window key\
2.  Increment a distributed counter for that window\
3.  Attach TTL equal to the window duration\
4.  If counter value exceeds limit → request is blocked\
5.  When TTL expires → window automatically resets

------------------------------------------------------------------------

# 📊 Key DKV Features Used

  Feature                    Purpose
  -------------------------- -------------------------
  Counters                   Atomic request tracking
  TTL                        Automatic window reset
  Cluster-wide consistency   Distributed enforcement
  Prefix isolation           Safe sample runs

------------------------------------------------------------------------

# 📦 Summary

This sample demonstrates that Clustron DKV can be used to build:

-   Distributed rate limiters\
-   API throttling systems\
-   Abuse protection mechanisms\
-   Request quota enforcement

It shows how counters + TTL together provide a simple yet powerful
distributed rate limiting pattern.

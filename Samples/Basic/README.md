# 🚀 Clustron DKV --- Basic Sample

This sample demonstrates the core **Clustron DKV Client SDK**
programming model.

It is the recommended starting point for learning how to work with DKV.

------------------------------------------------------------------------

# 📌 What This Sample Demonstrates

This sample performs the following operations:

-   Connect to a DKV cluster (InProc or Remote)
-   Store an object (`PUT`)
-   Retrieve an object (`GET`)
-   Read metadata (TTL, labels, content type)
-   Use distributed counters
-   Observe TTL expiration
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
  { "Host": "127.0.0.1", "Port": 7070 },
  { "Host": "127.0.0.1", "Port": 7071 }
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
      { "Host": "127.0.0.1", "Port": 7070 },
      { "Host": "127.0.0.1", "Port": 7071 }
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

# 🔄 How Remote Connectivity Works

1.  Client connects to one configured seed node\
2.  The server returns cluster topology\
3.  The client connects to all cluster nodes automatically\
4.  The client handles:
    -   Node joins
    -   Node leaves
    -   Failover
    -   Reconnection
    -   Topology updates

You only configure the seeds once.

------------------------------------------------------------------------

# 🧪 What the Sample Actually Does

1.  Loads configuration\
2.  Initializes the client\
3.  Clears previous keys\
4.  Stores a `Customer` object with:
    -   Entity type: `customer`
    -   TTL: 30 seconds
    -   Content type: `application/json`
    -   Labels: `env=demo`, `sample=basic`
5.  Retrieves and prints metadata and values\
6.  Demonstrates distributed counter usage\
7.  Waits for TTL expiration\
8.  Cleans up created keys

------------------------------------------------------------------------

# 📦 Summary

  Mode     Server Required   Use Case
  -------- ----------------- -------------------
  InProc   No                Local development
  Remote   Yes               Real cluster

------------------------------------------------------------------------

This sample provides the foundation for building distributed systems
using **Clustron DKV**.

# 🚀 Clustron DKV --- Watch Sample

This sample demonstrates how to use the **Watch API** in Clustron DKV to
observe real-time changes on keys and key prefixes.

It simulates live updates and shows how clients can react to create,
update, and delete events across a cluster.

------------------------------------------------------------------------

# 📌 What This Sample Demonstrates

This sample performs the following operations:

-   Connect to a DKV cluster (InProc or Remote)
-   Watch a single key
-   Watch a key prefix
-   Include initial snapshot on watch start
-   Receive real-time change events
-   Track revision numbers
-   Simulate background updates and deletes
-   Stop watchers gracefully
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

# 🧠 How the Watch API Works

The Watch API allows clients to subscribe to:

-   A specific key\
-   A key prefix

Watchers receive events when:

-   A key is created\
-   A key is updated\
-   A key is deleted

Each event includes:

-   Event type\
-   Key\
-   Revision number\
-   Value (if applicable)

------------------------------------------------------------------------

# 🔄 Sample Flow

##  Start Watchers

-   Watch a single key with snapshot enabled\
-   Watch a prefix for multiple related keys

------------------------------------------------------------------------

##  Simulate Live Updates

A background task:

-   Updates keys periodically\
-   Deletes keys occasionally\
-   Generates real-time watch events

------------------------------------------------------------------------

## Stop Watchers

-   Stop subscriptions gracefully\
-   Print event summary

------------------------------------------------------------------------

# 📊 Key DKV Features Used

  Feature             Purpose
  ------------------- -------------------------
  WatchKeyAsync       Subscribe to single key
  WatchPrefixAsync    Subscribe to prefix
  Snapshot            Initial state retrieval
  Revision tracking   Event ordering
  Real-time events    Reactive systems
  Prefix cleanup      Safe sample isolation

------------------------------------------------------------------------

# 📦 Summary

This sample demonstrates how Clustron DKV enables:

-   Reactive distributed systems\
-   Event-driven architectures\
-   Change notifications\
-   Cache invalidation workflows\
-   Real-time coordination

It models real-world use cases such as:

-   Live configuration updates\
-   Distributed cache synchronization\
-   Event streaming\
-   Reactive microservices

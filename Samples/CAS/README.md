# 🚀 Clustron DKV --- Compare-And-Swap (CAS) Sample

This sample demonstrates how to use **optimistic concurrency control**
in Clustron DKV using Compare-And-Swap (CAS) semantics.

It shows how to safely update and delete items using version-based
conditional operations.

------------------------------------------------------------------------

# 📌 What This Sample Demonstrates

This sample performs the following operations:

-   Connect to a DKV cluster (InProc or Remote)
-   Insert an item using `IfAbsent`
-   Retrieve item along with its version
-   Perform a successful CAS update using `IfMatch`
-   Attempt a failed CAS update using a stale version
-   Perform a CAS delete using version matching
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

# 🧠 What is CAS (Compare-And-Swap)?

CAS is an **optimistic concurrency mechanism** that ensures an update or
delete only succeeds if the item's version matches the expected version.

It prevents lost updates in concurrent environments.

------------------------------------------------------------------------

# 🧪 What the Sample Actually Does

1.  Inserts a customer using `Put.IfAbsent()`\
2.  Retrieves the item and reads its `ItemVersion`\
3.  Performs a successful update using `Put.WithIfMatch(version)`\
4.  Attempts another update using a stale version (expected to fail with
    `Conflict`)\
5.  Performs a version-matched delete using `Delete.IfMatch(version)`\
6.  Verifies deletion\
7.  Cleans up using key prefix

------------------------------------------------------------------------

# 📦 Summary

  Operation            API Used
  -------------------- ----------------------------------
  Insert If Absent     `Put.IfAbsent()`
  Conditional Update   `Put.WithIfMatch()`
  Conditional Delete   `Delete.IfMatch()`
  Version Retrieval    `GetAsync()` (returns `Version`)

This sample demonstrates safe concurrent updates using version-based
optimistic concurrency in **Clustron DKV**.

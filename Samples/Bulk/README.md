# 🚀 Clustron DKV --- Bulk Operations Sample

This sample demonstrates how to use the **Clustron DKV Client SDK** for
high-performance bulk operations.

It showcases batch-based PUT, GET, and DELETE operations using optimized
client APIs.

------------------------------------------------------------------------

# 📌 What This Sample Demonstrates

This sample performs the following operations:

-   Connect to a DKV cluster (InProc or Remote)
-   Insert multiple objects using `PutManyAsync`
-   Retrieve multiple objects using `GetManyAsync`
-   Delete multiple objects using `DeleteManyAsync`
-   Verify deletion results
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

# 🧪 What the Sample Actually Does

1.  Loads configuration\
2.  Initializes the client\
3.  Performs bulk insert of 5 `Customer` objects\
4.  Retrieves all inserted customers in a single batch call\
5.  Deletes all customers in a single batch call\
6.  Verifies deletion results\
7.  Performs cleanup using a key prefix

------------------------------------------------------------------------

# 📦 Summary

  Operation     API Used
  ------------- -------------------
  Bulk PUT      `PutManyAsync`
  Bulk GET      `GetManyAsync`
  Bulk DELETE   `DeleteManyAsync`

This sample demonstrates how to efficiently handle batch operations in
**Clustron DKV**.

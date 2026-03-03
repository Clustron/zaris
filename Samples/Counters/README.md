# 🚀 Clustron DKV --- Counters Sample

This sample demonstrates how to use **Distributed Counters** in Clustron
DKV.

It showcases atomic numeric operations that are safe, consistent, and
cluster-aware.

------------------------------------------------------------------------

# 📌 What This Sample Demonstrates

This sample performs the following operations:

-   Connect to a DKV cluster (InProc or Remote)
-   Perform atomic increments (`AddAsync`)
-   Retrieve current counter values
-   Set counter values explicitly
-   Enforce Min / Max bounds
-   Apply TTL (time-to-live) to counters
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

# 🧠 Counter Features Demonstrated

## Atomic Increment

`AddAsync(key, delta)`

Ensures atomic updates across the cluster.

------------------------------------------------------------------------

## Get Counter Value

Returns the current numeric value safely across nodes.

------------------------------------------------------------------------

## Set Counter Value

`SetAsync(key, value)`

Overrides the counter value atomically.

------------------------------------------------------------------------

## Min / Max Bounds Enforcement

Counters can enforce limits:

``` csharp
new CounterOptions { MaxValue = 10 }
```

If an operation exceeds the bound, it fails safely.

------------------------------------------------------------------------

## Counter TTL

Counters support expiration:

``` csharp
new CounterOptions { Ttl = TimeSpan.FromSeconds(20) }
```

After TTL expires, the counter is automatically removed.

------------------------------------------------------------------------

# 📦 Summary

  Feature        Supported
  -------------- -----------
  Atomic Add     Yes
  Get            Yes
  Set            Yes
  Min / Max      Yes
  TTL            Yes
  Cluster-wide   Yes

------------------------------------------------------------------------

This sample demonstrates that DKV counters are:

-   Atomic\
-   Cluster-aware\
-   Bound-safe\
-   TTL-enabled

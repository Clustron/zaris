# Clustron DKV -- Counters Sample

This sample demonstrates how to use **Distributed Counters** in Clustron
DKV.

It covers:

-   Atomic increments
-   Retrieving counter values
-   Setting counter values
-   Min / Max bounds enforcement
-   TTL (time-to-live) on counters
-   Cleanup of created keys

This sample helps you understand how DKV counters provide safe, atomic
numeric operations across a cluster.

------------------------------------------------------------------------

## What This Sample Does

The sample performs the following steps:

1.  Loads configuration from `appsettings.json`
2.  Initializes the DKV client
3.  Performs atomic increments
4.  Reads the current counter value
5.  Sets the counter to a specific value
6.  Demonstrates min/max bounds enforcement
7.  Demonstrates TTL expiration on counters
8.  Cleans up all created keys

------------------------------------------------------------------------

## Quick Start (Default: InProc Mode)

By default, this sample runs in **InProc mode**.

This means:

-   No DKV server setup is required
-   Everything runs embedded inside your process
-   Ideal for quick API testing

### Default `appsettings.json`

``` json
{
  "Dkv": {
    "ClusterId": "sample-cluster",
    "Mode": "InProc",
    "LogFilePath": "logs/dkv.log"
  }
}
```

### Run the Sample

``` bash
dotnet run
```

------------------------------------------------------------------------

## Running Against a Real DKV Cluster (Remote Mode)

To run this sample against an actual DKV cluster:

1.  Install and start DKV servers
2.  Create a store
3.  Update `appsettings.json` to use Remote mode
4.  Run the sample

Full setup guide:
https://github.com/zeroheartbeat/clustron-dkv/blob/main/docs/getting-started.md

------------------------------------------------------------------------

## Remote Mode Configuration Example

``` json
{
  "Dkv": {
    "ClusterId": "my-store",
    "Mode": "Remote",
    "RemoteHost": "192.168.1.10",
    "RemotePort": 4100,
    "LogFilePath": "logs/dkv.log"
  }
}
```

------------------------------------------------------------------------

## Configuration Properties Explained

### ClusterId

The **Store ID** that you created.

When creating a store using Admin tools or CLI, you provide a store
name. That store name becomes your `ClusterId`.

If your store name is:

`my-store`

Then:

``` json
"ClusterId": "my-store"
```

------------------------------------------------------------------------

### Mode

Determines how the client runs.

  Value    Meaning
  -------- ----------------------------------------------
  InProc   Embedded in-memory store inside your process
  Remote   Connects to external DKV server nodes

Use `Remote` when connecting to a real cluster.

------------------------------------------------------------------------

### RemoteHost

The IP address (or hostname) of a DKV server node.

This acts as the **seed server**.

You only need to configure one server. After connecting, the client
automatically:

-   Discovers all cluster nodes
-   Connects to the entire cluster
-   Handles topology and failover internally

Example:

``` json
"RemoteHost": "192.168.1.10"
```

------------------------------------------------------------------------

### RemotePort

The `ClientPort` selected when creating the store.

This port is used for client operations. It is **not** the replication
port.

If during store creation you selected:

`ClientPort: 4100`

Then:

``` json
"RemotePort": 4100
```

------------------------------------------------------------------------

### LogFilePath

Optional path for client logs.

Example:

``` json
"LogFilePath": "logs/dkv.log"
```

------------------------------------------------------------------------

## Counter Features Demonstrated

### Atomic Increment

AddAsync(key, delta)

Ensures atomic updates across the cluster.

------------------------------------------------------------------------

### Set Counter

SetAsync(key, value)

Overrides the counter value atomically.

------------------------------------------------------------------------

### Bounds (Min / Max)

Counters can enforce limits:

new CounterOptions { MaxValue = 10 }

If the operation exceeds the bound, it fails safely.

------------------------------------------------------------------------

### Counter TTL

Counters support expiration:

new CounterOptions { Ttl = TimeSpan.FromSeconds(20) }

After TTL expires, the counter is automatically removed.

------------------------------------------------------------------------

## Summary

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

-   Atomic
-   Cluster-aware
-   Bound-safe
-   TTL-enabled

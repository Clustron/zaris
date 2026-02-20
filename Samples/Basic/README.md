# Clustron DKV -- Basic Sample

This sample demonstrates the core Clustron DKV client APIs including:

-   Connecting to a cluster\
-   Storing an object (`PUT`)\
-   Retrieving an object (`GET`)\
-   Reading metadata (TTL, labels, content type)\
-   Using distributed counters\
-   TTL expiration behavior\
-   Cleaning up created keys

This is the recommended starting point for learning the DKV programming
model.

------------------------------------------------------------------------

## What This Sample Does

The sample performs the following steps:

1.  Loads configuration from `appsettings.json`
2.  Initializes the DKV client
3.  Stores a `Customer` object with:
    -   Entity type (`customer`)
    -   TTL (30 seconds)
    -   Content type (`application/json`)
    -   Labels (`env`, `sample`)
4.  Retrieves and prints:
    -   The stored object
    -   TTL
    -   Content type
    -   Creation time
    -   Labels
5.  Demonstrates distributed counter usage
6.  Waits for TTL expiration and verifies removal
7.  Cleans up all created keys

------------------------------------------------------------------------

## Quick Start (Default: InProc Mode)

By default, this sample runs in **InProc mode**.

This means:

-   No DKV server setup is required\
-   Everything runs embedded inside your process\
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

You should see successful PUT, GET, counter, and TTL expiration output.

------------------------------------------------------------------------

## Running Against a Real DKV Cluster (Remote Mode)

To run this sample against an actual DKV cluster:

1.  Install and start DKV servers\
2.  Create a store\
3.  Update `appsettings.json` to use Remote mode\
4.  Run the sample

Full setup guide:\
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
name.\
That store name becomes your `ClusterId`.

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

You only need to configure one server.\
After connecting, the client automatically:

-   Discovers all cluster nodes\
-   Connects to the entire cluster\
-   Handles topology and failover internally

Example:

``` json
"RemoteHost": "192.168.1.10"
```

------------------------------------------------------------------------

### RemotePort

The `ClientPort` selected when creating the store.

This port is used for client operations.\
It is **not** the replication port.

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

## How Remote Connectivity Works

1.  Client connects to the seed server (`RemoteHost:RemotePort`)
2.  Server returns cluster topology
3.  Client connects to all nodes automatically
4.  Client handles:
    -   Failover\
    -   Reconnection\
    -   Node joins/leaves\
    -   Topology updates

You only configure one server.

------------------------------------------------------------------------

## Summary

  Scenario   Server Required
  ---------- -----------------
  InProc     No
  Remote     Yes

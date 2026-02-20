# Clustron DKV -- Lease Sample

This sample demonstrates how to use **Distributed Leases** in Clustron
DKV.

Leases allow you to:

-   Automatically bind multiple keys to a lease
-   Expire a group of keys together
-   Extend lease lifetime using KeepAlive
-   Immediately revoke a lease and delete all attached keys

This feature is ideal for:

-   Session management
-   Temporary workflows
-   Ephemeral job data
-   Distributed coordination

------------------------------------------------------------------------

## What This Sample Does

The sample performs the following:

1.  Loads configuration from `appsettings.json`
2.  Initializes the DKV client
3.  Grants a lease (30 seconds)
4.  Attaches multiple keys to the lease
5.  Waits for lease expiry and verifies automatic cleanup
6.  Demonstrates KeepAlive to extend lease lifetime
7.  Demonstrates Revoke to immediately delete all attached keys

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

Example:

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

You only need to configure one server. The client automatically
discovers and connects to the full cluster.

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

``` json
"LogFilePath": "logs/dkv.log"
```

------------------------------------------------------------------------

## Lease Features Demonstrated

### Grant Lease

    GrantAsync(TimeSpan)

Creates a lease with a specific expiration time.

------------------------------------------------------------------------

### Attach Keys to Lease

    Put.WithLease(leaseId)

All attached keys are automatically removed when the lease expires.

------------------------------------------------------------------------

### KeepAlive

    KeepAliveAsync(leaseId)

Extends the lease lifetime.

------------------------------------------------------------------------

### Revoke

    RevokeAsync(leaseId)

Immediately removes all keys bound to the lease.

------------------------------------------------------------------------

## Summary

  Feature            Supported
  ------------------ -----------
  Lease Grant        Yes
  Automatic Expiry   Yes
  KeepAlive          Yes
  Revoke             Yes
  Cluster-wide       Yes

------------------------------------------------------------------------

This sample demonstrates that DKV leases provide:

-   Automatic grouped expiration
-   Distributed lease lifecycle management
-   Safe cluster-wide coordination

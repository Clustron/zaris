# 🚀 Clustron DKV --- Transaction Sample

This sample demonstrates how to use **Transactions in Clustron DKV** to
perform **atomic multi-key operations** with support for:

-   Commit
-   Rollback
-   Conflict detection
-   Read-your-writes behavior
-   Transactional deletes

It shows how applications can safely modify multiple keys while
maintaining **data consistency across the cluster**.

------------------------------------------------------------------------

# 📌 What This Sample Demonstrates

This sample performs the following operations:

-   Connect to a DKV cluster (InProc or Remote)
-   Start a transaction
-   Read values inside a transaction
-   Modify multiple keys atomically
-   Commit a successful transaction
-   Roll back a transaction
-   Detect conflicts caused by external updates
-   Delete keys inside a transaction
-   Demonstrate read-your-writes semantics
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

-   Discovers cluster topology
-   Connects to all nodes
-   Handles failover
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

-   Ensure DKV servers are running
-   Ensure the store exists
-   Ensure the port matches the configured `ClientPort`

------------------------------------------------------------------------

# 🧠 How Transactions Work in Clustron DKV

Clustron DKV provides **optimistic multi-key transactions**.

A transaction:

1.  Reads keys and tracks their **revision versions**
2.  Applies modifications locally inside the transaction
3.  Attempts to commit the changes

During commit:

-   If no keys were modified by another client → **commit succeeds**
-   If any key changed → **transaction fails due to conflict**

This ensures **consistent updates across distributed nodes**.

------------------------------------------------------------------------

# 🔄 Sample Flow

## Initialize Data

Two keys are created:

    keyA = 10
    keyB = 20

------------------------------------------------------------------------

## Successful Transaction

A transaction updates both keys atomically.

    TX START
      GET A = 10
      GET B = 20
      PUT A = 15
      PUT B = 25
    COMMIT

Result:

    A = 15
    B = 25

------------------------------------------------------------------------

## Rollback Example

A transaction modifies a value but rolls back before committing.

    TX START
      PUT A = 999
    ROLLBACK

Result:

    A remains unchanged

------------------------------------------------------------------------

## Conflict Example

Another client updates a key while the transaction is in progress.

    TX START
      GET A = 15

External update occurs:

    PUT A = 500

Transaction commit:

    PUT A = 16
    COMMIT

Result:

    Transaction fails due to version conflict

------------------------------------------------------------------------

## Delete Inside Transaction

Keys can also be deleted inside transactions.

    TX START
      DELETE B
    COMMIT

Result:

    B is removed from the store

------------------------------------------------------------------------

# 📊 Key DKV Features Used

  Feature                 Purpose
  ----------------------- ----------------------------
  BeginTransactionAsync   Start a transaction
  GetAsync                Read key values
  PutAsync                Update values
  DeleteAsync             Remove keys
  CommitAsync             Apply atomic changes
  RollbackAsync           Discard changes
  Conflict detection      Prevent stale updates
  Read-your-writes        See your changes inside TX

------------------------------------------------------------------------

# 📦 Summary

This sample demonstrates how Clustron DKV transactions enable:

-   Atomic multi-key updates
-   Safe distributed modifications
-   Conflict detection using optimistic concurrency
-   Transaction rollback support
-   Consistent distributed state

These capabilities are essential for building:

-   Financial transfers
-   Inventory systems
-   Order processing workflows
-   Distributed coordination services
-   Reliable microservices

Clustron DKV transactions provide **simple APIs with strong consistency
guarantees for distributed applications**.

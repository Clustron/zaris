# Connect-DkvStore

## Synopsis

Connects the current PowerShell session to a **Clustron Distributed
Key‑Value (DKV) store**.

This command establishes a client connection to a store and stores the
connection context in the current PowerShell session. Once connected,
other ClientShell cmdlets can operate without specifying endpoints or
store details again.

Only **one active store connection** is allowed per session.

------------------------------------------------------------------------

# Syntax

``` powershell
Connect-DkvStore [-StoreName] <string> [-Endpoints] <string[]> [-TimeoutSec <int>] [-Force]
```

------------------------------------------------------------------------

# Description

`Connect-DkvStore` initializes a DKV client connection using one or more
seed endpoints.

The command:

1.  Validates endpoints.
2.  Parses the first endpoint as the seed server.
3.  Creates a client instance using the internal client factory.
4.  Stores the client in the session context.
5.  Enables other cmdlets to automatically reuse the connection.

If a connection already exists, the command fails unless `-Force` is
specified.

------------------------------------------------------------------------

# Parameters

## -StoreName

Specifies the name of the distributed store to connect to.

``` powershell
-Type: String
-Mandatory: True
-Position: 0
```

Example:

``` powershell
Connect-DkvStore -StoreName OrdersStore -Endpoints 10.0.0.11:7101
```

------------------------------------------------------------------------

## -Endpoints

Specifies one or more seed endpoints used to connect to the cluster.

Endpoints must be in the format:

    host:port

Examples:

    10.0.0.11:7101
    node1.company.net:7101

Only the **first endpoint** is used as the seed server for the initial
connection.

``` powershell
-Type: String[]
-Mandatory: True
-Position: 1
```

Example:

``` powershell
Connect-DkvStore -StoreName OrdersStore -Endpoints 10.0.0.11:7101,10.0.0.12:7101
```

------------------------------------------------------------------------

## -TimeoutSec

Specifies the connection timeout in seconds.

``` powershell
-Type: Int32
-Range: 1-120
-Default: 5
```

Example:

``` powershell
Connect-DkvStore -StoreName OrdersStore -Endpoints 10.0.0.11:7101 -TimeoutSec 10
```

------------------------------------------------------------------------

## -Force

Forces replacement of an existing store connection.

Without this switch, attempting to connect while a store is already
connected will produce an error.

``` powershell
-Type: SwitchParameter
```

Example:

``` powershell
Connect-DkvStore -StoreName OrdersStore -Endpoints 10.0.0.11:7101 -Force
```

------------------------------------------------------------------------

# Output

Returns a connection result object containing:

  Property         Description
  ---------------- ---------------------------------------
  StoreName        Connected store name
  Endpoints        List of configured endpoints
  ConnectedAtUtc   Connection timestamp
  LogFile          Client log file path
  Success          Connection result
  Error            Error information if connection fails

Example output:

    StoreName      : OrdersStore
    Endpoints      : {10.0.0.11:7101}
    ConnectedAtUtc : 2026-03-08T12:30:00Z
    LogFile        : C:\Users\User\AppData\Local\Clustron\DkvClient\dkv-OrdersStore-20260308.log
    Success        : True
    Error          :

------------------------------------------------------------------------

# Examples

## Connect to a store

``` powershell
Connect-DkvStore -StoreName OrdersStore -Endpoints 10.0.0.11:7101
```

------------------------------------------------------------------------

## Connect using multiple endpoints

``` powershell
Connect-DkvStore `
    -StoreName OrdersStore `
    -Endpoints 10.0.0.11:7101,10.0.0.12:7101
```

------------------------------------------------------------------------

## Replace an existing connection

``` powershell
Connect-DkvStore `
    -StoreName OrdersStore `
    -Endpoints 10.0.0.11:7101 `
    -Force
```

------------------------------------------------------------------------

# Logging

ClientShell automatically creates a log file under:

    %LOCALAPPDATA%\Clustron\DkvClient

Example:

    dkv-OrdersStore-20260308.log

The log contains:

-   connection lifecycle events
-   errors
-   client diagnostics

------------------------------------------------------------------------

# Notes

-   Only one DKV store can be connected per PowerShell session.
-   Use `-Force` to replace the existing connection.
-   The first endpoint is used as the seed node for cluster discovery.
-   Additional endpoints are preserved for reconnection scenarios.

# Test-DkvConnection

## Synopsis

Tests connectivity to a **Clustron Distributed Key-Value (DKV) store**.

------------------------------------------------------------------------

# Syntax

``` powershell
Test-DkvConnection [-StoreName <string>] [-Endpoints <string[]>] [-TimeoutSec <int>]
```

------------------------------------------------------------------------

# Description

`Test-DkvConnection` verifies whether a connection to a Clustron DKV
store can be established.

The command performs a lightweight probe by:

1.  Resolving an existing PowerShell connection (if one exists).
2.  Otherwise creating a temporary connection using the provided
    `StoreName` and `Endpoints`.
3.  Executing a minimal read-only operation to validate connectivity.

The command returns connection status and diagnostic information such as
response time.

Typical use cases:

-   validating cluster connectivity
-   verifying endpoint configuration
-   troubleshooting connection issues
-   testing automation scripts before performing operations

------------------------------------------------------------------------

# Parameters

## -StoreName

Specifies the store to connect to when no active PowerShell connection
exists.

``` powershell
Type: String
Mandatory: False (required if no active connection exists)
```

Example:

``` powershell
Test-DkvConnection -StoreName SessionStore
```

------------------------------------------------------------------------

## -Endpoints

Specifies one or more seed endpoints used to connect to the store.

``` powershell
Type: String[]
Mandatory: False (required if no active connection exists)
```

Example:

``` powershell
Test-DkvConnection -StoreName SessionStore -Endpoints "127.0.0.1:7070"
```

------------------------------------------------------------------------

## -TimeoutSec

Specifies the connection timeout in seconds.

``` powershell
Type: Int32
Default: 5
Range: 1–120
```

Example:

``` powershell
Test-DkvConnection -StoreName SessionStore -Endpoints "node1:7070" -TimeoutSec 10
```

------------------------------------------------------------------------

# Output

Returns an object describing the connection result.

  Property       Description
  -------------- ----------------------------------------
  StoreName      Target store
  Connected      Indicates whether connection succeeded
  ServerCount    Number of nodes reachable
  DurationMs     Time taken to perform the test
  TimestampUtc   Time the test was executed
  Error          Error message if connection failed

Example output:

    StoreName    : SessionStore
    Connected    : True
    ServerCount  : 3
    DurationMs   : 42
    TimestampUtc : 2026-03-08T10:32:10Z

------------------------------------------------------------------------

# Examples

## Test current connection

``` powershell
Test-DkvConnection
```

------------------------------------------------------------------------

## Test a remote cluster

``` powershell
Test-DkvConnection -StoreName TestStore -Endpoints "node1:7070"
```

------------------------------------------------------------------------

## Test with custom timeout

``` powershell
Test-DkvConnection -StoreName TestStore -Endpoints "node1:7070" -TimeoutSec 15
```

------------------------------------------------------------------------

# Notes

-   If a connection already exists via `Connect-DkvStore`, it will be
    reused.
-   Otherwise the command establishes a temporary connection for the
    test.
-   The test operation is **non-mutating** and safe to run in
    production.

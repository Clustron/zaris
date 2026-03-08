# Get-DkvConnection

## Synopsis

Returns information about the **current Clustron DKV client
connection**.

------------------------------------------------------------------------

# Syntax

``` powershell
Get-DkvConnection
```

------------------------------------------------------------------------

# Description

`Get-DkvConnection` retrieves the current connection context for the
Clustron DKV client shell.

If a connection has been established using `Connect-DkvStore`, the
cmdlet returns details such as:

-   store name
-   cluster endpoints
-   client identifier
-   connection timestamp

If no active connection exists, the command returns an object indicating
that the client is not connected.

This command is useful for:

-   verifying connection status
-   debugging connection issues
-   scripting connection checks
-   confirming which store is currently active

------------------------------------------------------------------------

# Output

Returns an object with the following properties.

  Property         Description
  ---------------- ----------------------------------------------------
  IsConnected      Indicates whether a connection is currently active
  StoreName        Name of the connected store
  Endpoints        Cluster seed endpoints
  ClientId         Unique identifier of the client instance
  ConnectedAtUtc   Timestamp when the connection was established
  Success          Indicates command execution success
  Error            Error information if applicable

Example output:

    IsConnected   : True
    StoreName     : TestStore
    Endpoints     : {127.0.0.1:7070, 127.0.0.1:7071}
    ClientId      : 2f4d1c1e-23c1-4c2e-a6a4-bff2e1e62a2f
    ConnectedAtUtc: 2026-03-08T12:10:21Z
    Success       : True
    Error         : 

------------------------------------------------------------------------

# Examples

## Check connection status

``` powershell
Get-DkvConnection
```

Example output when connected:

    IsConnected : True
    StoreName   : TestStore

------------------------------------------------------------------------

## Check connection in a script

``` powershell
if ((Get-DkvConnection).IsConnected) {
    Write-Host "Connected to DKV store"
}
```

------------------------------------------------------------------------

# Notes

-   This command does **not perform network operations**.
-   It simply returns the **current client context stored in the
    PowerShell session**.
-   If no connection exists, the cmdlet still returns a valid object
    with `IsConnected = false`.

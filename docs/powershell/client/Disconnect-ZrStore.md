# Disconnect-ZrStore

## Synopsis

Disconnects the current PowerShell session from a **Clustron Distributed
Key-Value (Zaris) store**.

------------------------------------------------------------------------

# Syntax

``` powershell
Disconnect-ZrStore
```

------------------------------------------------------------------------

# Description

`Disconnect-ZrStore` closes the active client connection to a Clustron
store and removes the connection context from the PowerShell session.

After disconnecting, other ClientShell cmdlets cannot execute until a
new connection is established using:

``` powershell
Connect-ZrStore
```

The command is **idempotent**, meaning it succeeds even if no connection
currently exists.

------------------------------------------------------------------------

# Output

Returns an object describing the result of the disconnect operation.

  Property         Description
  ---------------- -----------------------------------------
  StoreName        Name of the store that was disconnected
  WasConnected     Indicates whether a connection existed
  DisconnectedAt   Timestamp of the disconnection
  Success          Operation success flag
  Error            Error message if operation fails

Example output when a connection existed:

    StoreName      : OrdersStore
    WasConnected   : True
    DisconnectedAt : 2026-03-08T12:45:00Z
    Success        : True
    Error          :

Example output when no connection existed:

    StoreName      :
    WasConnected   : False
    DisconnectedAt :
    Success        : True
    Error          :

------------------------------------------------------------------------

# Examples

## Disconnect from the current store

``` powershell
Disconnect-ZrStore
```

------------------------------------------------------------------------

## Disconnect after performing operations

``` powershell
Connect-ZrStore -StoreName OrdersStore -Endpoints 10.0.0.11:7101

Set-ZrItem -Key order:1001 -Value "created"

Disconnect-ZrStore
```

------------------------------------------------------------------------

# Notes

-   The command safely disposes the underlying client instance.
-   Only one store connection exists per PowerShell session.
-   Running the command when not connected will still succeed.

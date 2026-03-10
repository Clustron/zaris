# Set-DkvItem

## Synopsis

Inserts or updates a key-value item in a **Clustron Distributed
Key-Value (DKV) store**.

This cmdlet writes data to the connected store. If the key already
exists, the value is updated and a new revision/version is generated.

------------------------------------------------------------------------

# Syntax

``` powershell
Set-DkvItem [-Key] <string> [-Value] <object> [-ExpireAfterSec <int>] [-LeaseId <LeaseId>]
```

------------------------------------------------------------------------

# Description

`Set-DkvItem` stores a value under a specified key in a Clustron store.

The command supports:

-   inserting new keys
-   updating existing keys
-   optional TTL expiration
-   optional lease-based ownership

Only **one of the following options can be used**:

-   `-ExpireAfterSec`
-   `-LeaseId`

Using both simultaneously will produce an error.

The cmdlet supports PowerShell's **ShouldProcess** model, enabling
`-WhatIf` and `-Confirm` functionality.

------------------------------------------------------------------------

# Parameters

## -Key

Specifies the key under which the value will be stored.

``` powershell
-Type: String
-Mandatory: True
-Position: 0
```

Example:

``` powershell
Set-DkvItem -Key user:100 -Value "John"
```

------------------------------------------------------------------------

## -Value

Specifies the value to store.

Values can be any serializable object supported by the Clustron client.

``` powershell
-Type: Object
-Mandatory: True
-Position: 1
```

Example:

``` powershell
Set-DkvItem -Key user:100 -Value "John"
```

------------------------------------------------------------------------

## -ExpireAfterSec

Specifies a time-to-live (TTL) in seconds.

The item will automatically expire after the specified duration.

``` powershell
-Type: Int32
-Range: 1..Int32.MaxValue
```

Example:

``` powershell
Set-DkvItem -Key session:100 -Value "abc123" -ExpireAfterSec 300
```

------------------------------------------------------------------------

## -LeaseId

Associates the key with a **lease owner**.

Only the holder of the lease may modify the value.

``` powershell
-Type: LeaseId
```

Example:

``` powershell
Set-DkvItem -Key leader -Value node1 -LeaseId $lease
```

------------------------------------------------------------------------

# Output

Returns an object describing the result of the operation.

  Property        Description
  --------------- -------------------------------------------
  StoreName       Target store
  Key             Key written
  IsUpdate        Indicates whether the key already existed
  Revision        Internal revision number
  Version         Version identifier
  TimeToLiveSec   TTL applied to the item
  LeaseId         Lease associated with the item
  Success         Operation success flag
  Error           Error message if the operation failed

Example output:

    StoreName      : OrdersStore
    Key            : order:1001
    IsUpdate       : False
    Revision       : 1
    Version        : 1
    TimeToLiveSec  :
    LeaseId        :
    Success        : True
    Error          :

------------------------------------------------------------------------

# Examples

## Insert a key

``` powershell
Set-DkvItem -Key order:1001 -Value "created"
```

------------------------------------------------------------------------

## Update a key

``` powershell
Set-DkvItem -Key order:1001 -Value "processed"
```

------------------------------------------------------------------------

## Insert with TTL

``` powershell
Set-DkvItem -Key session:abc -Value "token" -ExpireAfterSec 600
```

------------------------------------------------------------------------

## Insert using a lease

``` powershell
Set-DkvItem -Key leader -Value node1 -LeaseId $lease
```

------------------------------------------------------------------------

# Notes

-   If the key already exists, the operation updates the existing value.
-   TTL and Lease options cannot be used together.
-   The cmdlet respects PowerShell `-WhatIf` and `-Confirm` behaviors.
-   Ctrl+C safely cancels the operation without terminating the session.

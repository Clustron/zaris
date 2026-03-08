# Set-DkvItems

## Synopsis

Performs a **bulk insert or update** of multiple key-value items in a
**Clustron Distributed Key-Value (DKV) store**.

------------------------------------------------------------------------

# Syntax

``` powershell
Set-DkvItems [-Items] <IDictionary>
```

------------------------------------------------------------------------

# Description

`Set-DkvItems` inserts or updates multiple items in a Clustron store in
a **single bulk operation**.

The cmdlet accepts a PowerShell dictionary (`IDictionary`) containing
keys and values. Each entry in the dictionary becomes an item written to
the store.

Bulk operations are significantly more efficient than issuing individual
`Set-DkvItem` commands for each key.

The command:

-   validates dictionary keys
-   converts entries to key/value pairs
-   performs a bulk write using the DKV client
-   returns individual results for each key

The cmdlet supports PowerShell **ShouldProcess**, enabling `-WhatIf` and
`-Confirm`.

------------------------------------------------------------------------

# Parameters

## -Items

Specifies a dictionary of keys and values to write to the store.

``` powershell
Type: IDictionary
Mandatory: True
Position: 0
Pipeline: True
```

Example dictionary:

``` powershell
@{
    "user:1" = "Alice"
    "user:2" = "Bob"
    "user:3" = "Charlie"
}
```

Example usage:

``` powershell
Set-DkvItems -Items @{
    "user:1" = "Alice"
    "user:2" = "Bob"
}
```

------------------------------------------------------------------------

# Output

Returns one result object per item written.

  Property    Description
  ----------- -------------------------------------------
  StoreName   Target store
  Key         Item key
  IsUpdate    Indicates whether the key already existed
  Revision    Internal revision identifier
  Version     Version identifier
  Success     Operation success flag
  Error       Error message if the operation failed

Example output:

    StoreName : OrdersStore
    Key       : user:1
    IsUpdate  : False
    Revision  : 1
    Version   : 1
    Success   : True
    Error     :

------------------------------------------------------------------------

# Examples

## Bulk insert items

``` powershell
Set-DkvItems -Items @{
    "user:1" = "Alice"
    "user:2" = "Bob"
    "user:3" = "Charlie"
}
```

------------------------------------------------------------------------

## Bulk update items

``` powershell
Set-DkvItems -Items @{
    "order:1001" = "processed"
    "order:1002" = "shipped"
}
```

------------------------------------------------------------------------

## Using pipeline input

``` powershell
$items = @{
    "session:1" = "abc"
    "session:2" = "xyz"
}

$items | Set-DkvItems
```

------------------------------------------------------------------------

# Notes

-   Keys must be non-null and non-empty.
-   Each item result is returned individually in the pipeline.
-   Bulk writes improve performance compared to multiple `Set-DkvItem`
    calls.
-   Press **Ctrl+C** to safely cancel the operation.

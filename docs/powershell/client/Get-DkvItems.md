# Get-DkvItems

## Synopsis

Retrieves multiple items from a **Clustron Distributed Key-Value (DKV)
store** in a single bulk operation.

------------------------------------------------------------------------

# Syntax

``` powershell
Get-DkvItems [-Keys] <string[]>
```

------------------------------------------------------------------------

# Description

`Get-DkvItems` retrieves values for multiple keys from a Clustron store
using a single bulk request.

This cmdlet is optimized for high-performance retrieval when working
with many keys. Instead of issuing multiple `Get-DkvItem` calls, this
command performs a single cluster operation.

The command:

-   accepts a list of keys
-   retrieves values for all keys
-   returns one result object per key
-   safely handles missing keys without errors

------------------------------------------------------------------------

# Parameters

## -Keys

Specifies one or more keys to retrieve.

``` powershell
Type: String[]
Mandatory: True
Position: 0
Pipeline: True
PipelineByPropertyName: True
```

Example:

``` powershell
Get-DkvItems -Keys "user:1","user:2","user:3"
```

------------------------------------------------------------------------

# Output

Returns one result object per key.

  Property    Description
  ----------- ----------------------------------
  StoreName   Name of the connected store
  Key         Queried key
  Exists      Indicates whether the key exists
  Value       Stored value
  Revision    Internal revision identifier
  Version     Version identifier
  TtlSec      Remaining time-to-live
  Success     Operation success flag
  Error       Error message if operation fails

Example output:

    StoreName : OrdersStore
    Key       : user:1
    Exists    : True
    Value     : Alice
    Revision  : 1
    Version   : 1
    TtlSec    :
    Success   : True
    Error     :

------------------------------------------------------------------------

# Examples

## Retrieve multiple keys

``` powershell
Get-DkvItems -Keys "order:1001","order:1002","order:1003"
```

------------------------------------------------------------------------

## Retrieve keys using pipeline

``` powershell
"order:1001","order:1002","order:1003" | Get-DkvItems
```

------------------------------------------------------------------------

## Retrieve keys from a variable

``` powershell
$keys = @("user:1","user:2","user:3")

Get-DkvItems -Keys $keys
```

------------------------------------------------------------------------

# Notes

-   Missing keys are **not treated as errors**.
-   When a key does not exist, `Exists = false` is returned.
-   Bulk retrieval is significantly faster than multiple `Get-DkvItem`
    calls.
-   Press **Ctrl+C** to safely cancel the operation.

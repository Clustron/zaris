# Get-ZrCounter

## Synopsis

Retrieves the current value of a **counter** stored in a **Clustron
Distributed Key-Value (Zaris) store**.

------------------------------------------------------------------------

# Syntax

``` powershell
Get-ZrCounter [-Key] <string>
```

------------------------------------------------------------------------

# Description

`Get-ZrCounter` reads the value of a counter stored in a Clustron
store.

Counters are maintained atomically inside the cluster and are typically
used for:

-   distributed metrics
-   rate limiting
-   event counting
-   operational telemetry

This command returns the current numeric value of the counter.

------------------------------------------------------------------------

# Parameters

## -Key

Specifies the counter key to retrieve.

``` powershell
Type: String
Mandatory: True
Position: 0
```

Example:

``` powershell
Get-ZrCounter -Key orders:processed
```

------------------------------------------------------------------------

# Output

Returns an object describing the result of the operation.

  Property    Description
  ----------- ----------------------------------
  StoreName   Target store
  Key         Counter key
  Value       Current counter value
  Success     Operation success flag
  Error       Error message if operation fails

Example output:

    StoreName : OrdersStore
    Key       : orders:processed
    Value     : 42
    Success   : True
    Error     :

------------------------------------------------------------------------

# Examples

## Retrieve a counter

``` powershell
Get-ZrCounter -Key orders:processed
```

------------------------------------------------------------------------

## Retrieve an inventory counter

``` powershell
Get-ZrCounter -Key inventory:item-100
```

Example output:

    StoreName : InventoryStore
    Key       : inventory:item-100
    Value     : 95
    Success   : True
    Error     :

------------------------------------------------------------------------

# Notes

-   Counters are maintained atomically across the cluster.
-   If the counter does not exist, the operation may return an error
    depending on store configuration.
-   Press **Ctrl+C** to safely cancel the operation.

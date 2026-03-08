# Decrement-DkvCounter

## Synopsis

Atomically decrements a counter stored in a **Clustron Distributed
Key-Value (DKV) store**.

------------------------------------------------------------------------

# Syntax

``` powershell
Decrement-DkvCounter [-Key] <string> [-Delta <long>]
```

------------------------------------------------------------------------

# Description

`Decrement-DkvCounter` decreases the value of a numeric counter stored
in a Clustron store.

Counters are maintained atomically inside the cluster, ensuring
consistent results even when multiple clients modify the same counter
concurrently.

Internally, this command performs a counter update using a **negative
delta** value.

The command:

-   decrements the counter by the specified amount
-   creates the counter automatically if it does not exist
-   returns the updated counter value

------------------------------------------------------------------------

# Parameters

## -Key

Specifies the counter key.

``` powershell
Type: String
Mandatory: True
Position: 0
```

Example:

``` powershell
Decrement-DkvCounter -Key inventory:item-100
```

------------------------------------------------------------------------

## -Delta

Specifies how much the counter should be decreased.

Default value:

    1

``` powershell
Type: Int64
Mandatory: False
Position: 1
Range: 1..Int64.MaxValue
```

Example:

``` powershell
Decrement-DkvCounter -Key inventory:item-100 -Delta 5
```

------------------------------------------------------------------------

# Output

Returns an object describing the result of the operation.

  Property    Description
  ----------- ------------------------------------
  StoreName   Target store
  Key         Counter key
  Value       Updated counter value
  Delta       Applied decrement (negative value)
  Success     Operation success flag
  Error       Error message if operation fails

Example output:

    StoreName : OrdersStore
    Key       : inventory:item-100
    Value     : 95
    Delta     : -5
    Success   : True
    Error     :

------------------------------------------------------------------------

# Examples

## Decrement counter by 1

``` powershell
Decrement-DkvCounter -Key inventory:item-100
```

------------------------------------------------------------------------

## Decrement counter by 10

``` powershell
Decrement-DkvCounter -Key inventory:item-100 -Delta 10
```

------------------------------------------------------------------------

# Notes

-   Counter operations are atomic across the cluster.
-   If the counter does not exist, it will be automatically created.
-   Internally the command increments the counter with a negative value.
-   Press **Ctrl+C** to safely cancel the operation.

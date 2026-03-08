# Increment-DkvCounter

## Synopsis

Atomically increments a counter stored in a **Clustron Distributed
Key-Value (DKV) store**.

------------------------------------------------------------------------

# Syntax

``` powershell
Increment-DkvCounter [-Key] <string> [-Delta <long>]
```

------------------------------------------------------------------------

# Description

`Increment-DkvCounter` increases the value of a numeric counter stored
in a Clustron store.

Counters are maintained atomically inside the cluster, ensuring that
concurrent increments from multiple clients produce consistent results.

The command:

-   increments the counter by a specified delta
-   creates the counter automatically if it does not exist
-   returns the updated value after the increment operation

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
Increment-DkvCounter -Key orders:processed
```

------------------------------------------------------------------------

## -Delta

Specifies the amount to increment the counter.

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
Increment-DkvCounter -Key orders:processed -Delta 5
```

------------------------------------------------------------------------

# Output

Returns an object describing the result of the operation.

  Property    Description
  ----------- -----------------------------------
  StoreName   Target store
  Key         Counter key
  Value       New counter value
  Delta       Increment amount
  Success     Operation success flag
  Error       Error message if operation failed

Example output:

    StoreName : OrdersStore
    Key       : orders:processed
    Value     : 42
    Delta     : 1
    Success   : True
    Error     :

------------------------------------------------------------------------

# Examples

## Increment a counter by 1

``` powershell
Increment-DkvCounter -Key orders:processed
```

------------------------------------------------------------------------

## Increment a counter by 10

``` powershell
Increment-DkvCounter -Key orders:processed -Delta 10
```

------------------------------------------------------------------------

# Notes

-   Counters are incremented atomically within the cluster.
-   If the counter does not exist, it is automatically created.
-   Press **Ctrl+C** to safely cancel the operation.

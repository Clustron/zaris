# Set-ZrCounter

## Synopsis

Sets the value of a **distributed counter** in a Clustron Distributed
Key-Value (Zaris) store.

------------------------------------------------------------------------

# Syntax

``` powershell
Set-ZrCounter [-Key] <string> [-Value] <long>
```

------------------------------------------------------------------------

# Description

`Set-ZrCounter` explicitly assigns a numeric value to a distributed
counter.

Counters in Clustron Zaris are designed for:

-   distributed metrics
-   rate limiting
-   quotas
-   coordination counters
-   system statistics

Unlike `Increment-ZrCounter` or `Decrement-ZrCounter`, this command
**overwrites the counter value directly**.

If the counter does not already exist, it will be created.

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
Set-ZrCounter -Key requests:today -Value 100
```

------------------------------------------------------------------------

## -Value

Specifies the value to assign to the counter.

``` powershell
Type: Int64
Mandatory: True
Position: 1
```

Example:

``` powershell
Set-ZrCounter -Key rate:limit -Value 5000
```

------------------------------------------------------------------------

# Output

Returns an object describing the result of the operation.

  Property    Description
  ----------- ---------------------------------------
  StoreName   Target Zaris store
  Key         Counter key
  Value       Value assigned
  Success     Indicates whether operation succeeded
  Error       Error message if operation failed

Example output:

    StoreName : MetricsStore
    Key       : requests:today
    Value     : 100
    Success   : True
    Error     :

------------------------------------------------------------------------

# Examples

## Set a counter value

``` powershell
Set-ZrCounter -Key requests:today -Value 100
```

------------------------------------------------------------------------

## Initialize a quota counter

``` powershell
Set-ZrCounter -Key api:quota:user123 -Value 1000
```

------------------------------------------------------------------------

# Notes

-   This operation **overwrites the existing counter value**.
-   Use `Increment-ZrCounter` or `Decrement-ZrCounter` for relative
    updates.
-   Press **Ctrl+C** to safely cancel the operation.

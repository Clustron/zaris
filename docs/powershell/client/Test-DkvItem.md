# Test-DkvItem

## Synopsis

Tests whether a **key exists** in a Clustron Distributed Key-Value (DKV)
store.

------------------------------------------------------------------------

# Syntax

``` powershell
Test-DkvItem [-Key] <string> [-Detailed]
```

------------------------------------------------------------------------

# Description

`Test-DkvItem` checks whether a key exists in the connected Clustron
store.

The command performs a lightweight read operation and returns either:

-   a simple **Boolean result** (default), or
-   a **detailed object** describing the test result.

This command is useful for:

-   conditional automation scripts
-   verifying cache entries
-   validating distributed state
-   checking coordination keys

------------------------------------------------------------------------

# Parameters

## -Key

Specifies the key to test.

``` powershell
Type: String
Mandatory: True
Position: 0
```

Example:

``` powershell
Test-DkvItem -Key session:user123
```

------------------------------------------------------------------------

## -Detailed

Returns a detailed object instead of a Boolean result.

``` powershell
Type: SwitchParameter
Mandatory: False
```

Example:

``` powershell
Test-DkvItem -Key session:user123 -Detailed
```

------------------------------------------------------------------------

# Output

### Default output

Returns a Boolean value.

    True

or

    False

------------------------------------------------------------------------

### Detailed output

  Property    Description
  ----------- ----------------------------------
  StoreName   Target store
  Key         Key tested
  Exists      Indicates whether the key exists
  Success     Operation success flag
  Error       Error message if operation fails

Example:

    StoreName : SessionStore
    Key       : session:user123
    Exists    : True
    Success   : True
    Error     :

------------------------------------------------------------------------

# Examples

## Test if a key exists

``` powershell
Test-DkvItem -Key session:user123
```

------------------------------------------------------------------------

## Get detailed result

``` powershell
Test-DkvItem -Key session:user123 -Detailed
```

------------------------------------------------------------------------

# Notes

-   By default the command returns a **Boolean value**.
-   Use `-Detailed` for structured output useful in scripts.
-   The command performs a **non-mutating read operation**.

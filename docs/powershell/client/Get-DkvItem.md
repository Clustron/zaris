# Get-DkvItem

## Synopsis

Retrieves a value associated with a key from a **Clustron Distributed
Key-Value (DKV) store**.

------------------------------------------------------------------------

# Syntax

``` powershell
Get-DkvItem [-Key] <string>
```

------------------------------------------------------------------------

# Description

`Get-DkvItem` retrieves the value stored under a specified key in a
Clustron store.

The command:

-   queries the store for the specified key
-   returns the stored value if it exists
-   returns metadata such as revision and version
-   safely handles missing keys without throwing errors

If the key does not exist, the cmdlet returns `Exists = false` while
still indicating the operation succeeded.

------------------------------------------------------------------------

# Parameters

## -Key

Specifies the key to retrieve.

``` powershell
Type: String
Mandatory: True
Position: 0
```

Example:

``` powershell
Get-DkvItem -Key user:100
```

------------------------------------------------------------------------

# Output

Returns an object describing the result of the operation.

  Property    Description
  ----------- ---------------------------------------
  StoreName   Name of the connected store
  Key         Key queried
  Exists      Indicates whether the key exists
  Value       Stored value (if present)
  Revision    Internal revision identifier
  Version     Version identifier
  TtlSec      Remaining time-to-live in seconds
  Success     Operation success flag
  Error       Error message if the operation failed

Example output:

    StoreName : OrdersStore
    Key       : order:1001
    Exists    : True
    Value     : created
    Revision  : 1
    Version   : 1
    TtlSec    :
    Success   : True
    Error     :

------------------------------------------------------------------------

# Examples

## Retrieve a key

``` powershell
Get-DkvItem -Key order:1001
```

------------------------------------------------------------------------

## Retrieve a non-existing key

``` powershell
Get-DkvItem -Key order:9999
```

Example output:

    StoreName : OrdersStore
    Key       : order:9999
    Exists    : False
    Value     :
    Success   : True
    Error     :

------------------------------------------------------------------------

# Notes

-   Retrieving a missing key is **not treated as an error**.
-   The command returns `Exists = false` when the key does not exist.
-   Metadata such as revision and TTL are returned when available.
-   Press **Ctrl+C** to safely cancel the operation.

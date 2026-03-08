# Get-DkvItemMetadata

## Synopsis

Retrieves **metadata information** for a key stored in a **Clustron
Distributed Key‑Value (DKV) store**.

------------------------------------------------------------------------

# Syntax

``` powershell
Get-DkvItemMetadata [-Key] <string>
```

------------------------------------------------------------------------

# Description

`Get-DkvItemMetadata` retrieves metadata associated with a key without
focusing on the stored value itself.

The command returns internal metadata maintained by the DKV cluster
including:

-   revision identifier
-   version
-   remaining time-to-live
-   custom metadata associated with the entry

This cmdlet is useful for:

-   debugging distributed data state
-   inspecting lease/version information
-   diagnosing TTL expiration behavior
-   auditing item metadata

------------------------------------------------------------------------

# Parameters

## -Key

Specifies the key whose metadata should be retrieved.

``` powershell
Type: String
Mandatory: True
Position: 0
```

Example:

``` powershell
Get-DkvItemMetadata -Key order:1001
```

------------------------------------------------------------------------

# Output

Returns an object describing the metadata of the item.

  Property     Description
  ------------ -----------------------------------------------
  StoreName    Name of the connected store
  Key          Key queried
  Exists       Indicates whether the key exists
  Revision     Internal revision identifier
  Version      Version identifier
  TimeToLive   Remaining TTL
  Metadata     Metadata dictionary associated with the entry
  Success      Operation success flag
  Error        Error message if operation fails

Example output:

    StoreName : OrdersStore
    Key       : order:1001
    Exists    : True
    Revision  : 3
    Version   : 2
    TimeToLive: 120
    Metadata  : {}
    Success   : True
    Error     :

------------------------------------------------------------------------

# Examples

## Retrieve metadata for an item

``` powershell
Get-DkvItemMetadata -Key order:1001
```

------------------------------------------------------------------------

## Retrieve metadata for a key with TTL

``` powershell
Get-DkvItemMetadata -Key session:abc
```

Example output:

    StoreName : OrdersStore
    Key       : session:abc
    Exists    : True
    Revision  : 1
    Version   : 1
    TimeToLive: 300
    Success   : True

------------------------------------------------------------------------

# Notes

-   This command retrieves **metadata only** and is primarily used for
    diagnostics.
-   Missing keys return `Exists = false`.
-   Press **Ctrl+C** to safely cancel the operation.

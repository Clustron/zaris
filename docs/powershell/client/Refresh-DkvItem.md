# Refresh-DkvItem

## Synopsis

Refreshes the **TTL (time-to-live)** of an existing item in a Clustron
Distributed Key-Value (DKV) store.

------------------------------------------------------------------------

# Syntax

``` powershell
Refresh-DkvItem [-Key] <string>
```

------------------------------------------------------------------------

# Description

`Refresh-DkvItem` extends the lifetime of an existing item by resetting
its TTL to the original value.

This operation works by:

1.  Retrieving the current item.
2.  Reading its existing TTL.
3.  Rewriting the item with the same value and TTL.

If the item does not exist or does not have a TTL, the refresh operation
will fail.

Typical use cases:

-   session keep-alive
-   sliding expiration
-   cache entry refresh
-   distributed coordination objects

------------------------------------------------------------------------

# Parameters

## -Key

Specifies the key of the item whose TTL should be refreshed.

``` powershell
Type: String
Mandatory: True
Position: 0
```

Example:

``` powershell
Refresh-DkvItem -Key session:user123
```

------------------------------------------------------------------------

# Output

Returns an object describing the refresh operation.

  Property     Description
  ------------ ------------------------------------
  StoreName    Target store
  Key          Item key
  Refreshed    Indicates whether refresh occurred
  TimeToLive   TTL value applied
  Revision     Updated revision number
  Version      Item version
  Success      Operation success flag
  Error        Error message if refresh fails

Example output:

    StoreName  : SessionStore
    Key        : session:user123
    Refreshed  : True
    TimeToLive : 00:05:00
    Revision   : 14
    Version    : 2
    Success    : True
    Error      :

------------------------------------------------------------------------

# Examples

## Refresh a session key

``` powershell
Refresh-DkvItem -Key session:user123
```

------------------------------------------------------------------------

## Refresh a cached object

``` powershell
Refresh-DkvItem -Key cache:product:100
```

------------------------------------------------------------------------

# Notes

-   Refresh only works on items that have an existing TTL.
-   If the item has **no TTL**, the command returns an error.
-   If the item **does not exist**, the operation fails.
-   Press **Ctrl+C** to cancel the operation safely.

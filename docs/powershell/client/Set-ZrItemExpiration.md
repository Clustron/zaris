# Set-ZrItemExpiration

## Synopsis

Updates the **time-to-live (TTL)** of an existing item in a Clustron
Distributed Key-Value (Zaris) store.

------------------------------------------------------------------------

# Syntax

``` powershell
Set-ZrItemExpiration [-Key] <string> [-TimeToLive] <TimeSpan>
```

------------------------------------------------------------------------

# Description

`Set-ZrItemExpiration` sets or updates the expiration time for an
existing item.

The command works by:

1.  Retrieving the current item value.
2.  Rewriting the item with the same value.
3.  Applying the specified TTL.

If the item does not exist, the operation fails.

Typical use cases:

-   updating cache expiration
-   implementing sliding expiration
-   extending session lifetime
-   expiring distributed coordination entries

------------------------------------------------------------------------

# Parameters

## -Key

Specifies the key whose expiration should be updated.

``` powershell
Type: String
Mandatory: True
Position: 0
```

Example:

``` powershell
Set-ZrItemExpiration -Key session:user123 -TimeToLive (New-TimeSpan -Minutes 10)
```

------------------------------------------------------------------------

## -TimeToLive

Specifies the TTL to apply to the item.

``` powershell
Type: TimeSpan
Mandatory: True
Position: 1
```

Example:

``` powershell
Set-ZrItemExpiration -Key cache:item1 -TimeToLive (New-TimeSpan -Seconds 60)
```

------------------------------------------------------------------------

# Output

Returns an object describing the expiration update.

  Property     Description
  ------------ ----------------------------------
  StoreName    Target store
  Key          Item key
  TimeToLive   TTL applied
  IsUpdate     Indicates item was updated
  Revision     Item revision
  Version      Item version
  Success      Operation success flag
  Error        Error message if operation fails

Example output:

    StoreName : SessionStore
    Key       : session:user123
    TimeToLive: 00:10:00
    IsUpdate  : True
    Revision  : 42
    Version   : 3
    Success   : True
    Error     :

------------------------------------------------------------------------

# Examples

## Set item expiration

``` powershell
Set-ZrItemExpiration -Key session:user123 -TimeToLive (New-TimeSpan -Minutes 10)
```

------------------------------------------------------------------------

## Expire a cache item in 30 seconds

``` powershell
Set-ZrItemExpiration -Key cache:product:100 -TimeToLive (New-TimeSpan -Seconds 30)
```

------------------------------------------------------------------------

# Notes

-   The key **must already exist**.
-   The command rewrites the item with the new TTL.
-   Use `Refresh-ZrItem` to extend the existing TTL instead.
-   Press **Ctrl+C** to cancel the operation safely.

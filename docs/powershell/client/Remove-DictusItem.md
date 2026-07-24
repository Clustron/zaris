# Remove-DictusItem

## Synopsis

Deletes an item from a **Clustron Distributed Key-Value (Dictus) store**.

------------------------------------------------------------------------

# Syntax

``` powershell
Remove-DictusItem [-Key] <string>
```

------------------------------------------------------------------------

# Description

`Remove-DictusItem` deletes a key and its associated value from a Clustron
store.

If the key exists, it is removed and the operation returns metadata
about the deletion.

If the key does not exist, the operation still succeeds but reports that
no deletion occurred.

Typical use cases:

-   removing cache entries
-   deleting distributed state
-   cleaning up expired sessions
-   removing coordination artifacts

------------------------------------------------------------------------

# Parameters

## -Key

Specifies the key to delete.

``` powershell
Type: String
Mandatory: True
Position: 0
```

Example:

``` powershell
Remove-DictusItem -Key session:user123
```

------------------------------------------------------------------------

# Output

Returns an object describing the result of the delete operation.

  Property    Description
  ----------- ---------------------------------------------------
  StoreName   Target store
  Key         Deleted key
  Deleted     Indicates whether the key existed and was deleted
  Revision    Revision after deletion
  Version     Version after deletion
  Success     Operation success flag
  Error       Error message if operation fails

Example output:

    StoreName : SessionStore
    Key       : session:user123
    Deleted   : True
    Revision  : 21
    Version   : 3
    Success   : True
    Error     :

------------------------------------------------------------------------

# Examples

## Delete a session key

``` powershell
Remove-DictusItem -Key session:user123
```

------------------------------------------------------------------------

## Delete a cache entry

``` powershell
Remove-DictusItem -Key cache:product:100
```

------------------------------------------------------------------------

# Notes

-   Deleting a non-existing key is **not treated as an error**.
-   The command returns `Deleted = false` when the key is missing.
-   Press **Ctrl+C** to cancel the operation safely.

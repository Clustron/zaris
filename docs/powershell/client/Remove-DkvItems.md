# Remove-DkvItems

## Synopsis

Deletes multiple items from a **Clustron Distributed Key‑Value (DKV)
store** in a single operation.

------------------------------------------------------------------------

# Syntax

``` powershell
Remove-DkvItems [-Keys] <string[]>
```

------------------------------------------------------------------------

# Description

`Remove-DkvItems` deletes multiple keys from a Clustron store in a
single batch operation.

Bulk deletion is significantly more efficient than issuing multiple
`Remove-DkvItem` commands individually because the request is executed
as a **single cluster operation**.

If a key does not exist, the operation still succeeds and reports that
the key was not deleted.

Typical use cases include:

-   clearing groups of cache entries
-   removing multiple sessions
-   deleting distributed state
-   batch cleanup of application keys

------------------------------------------------------------------------

# Parameters

## -Keys

Specifies the keys to delete.

``` powershell
Type: String[]
Mandatory: True
Position: 0
ValueFromPipeline: True
ValueFromPipelineByPropertyName: True
```

Example:

``` powershell
Remove-DkvItems -Keys "user:1","user:2","user:3"
```

------------------------------------------------------------------------

# Output

Returns one object per key describing the result of the delete
operation.

  Property    Description
  ----------- ---------------------------------------------------
  StoreName   Target store
  Key         Key that was processed
  Deleted     Indicates whether the key existed and was deleted
  Revision    Revision after deletion
  Version     Version after deletion
  Success     Operation success flag
  Error       Error message if operation fails

Example output:

    StoreName : UserStore
    Key       : user:1
    Deleted   : True
    Revision  : 18
    Version   : 2
    Success   : True
    Error     :

------------------------------------------------------------------------

# Examples

## Delete multiple keys

``` powershell
Remove-DkvItems -Keys "session:1","session:2","session:3"
```

------------------------------------------------------------------------

## Delete keys from pipeline

``` powershell
"cache:1","cache:2","cache:3" | Remove-DkvItems
```

------------------------------------------------------------------------

# Notes

-   Bulk deletion is **more efficient than multiple single deletes**.
-   Missing keys are **not treated as errors**.
-   Each key produces its own result object.
-   Press **Ctrl+C** to cancel the operation safely.

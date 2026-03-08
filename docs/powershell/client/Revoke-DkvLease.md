# Revoke-DkvLease

## Synopsis

Revokes an existing **lease** in a Clustron Distributed Key-Value (DKV)
store.

------------------------------------------------------------------------

# Syntax

``` powershell
Revoke-DkvLease [-LeaseId] <LeaseId>
```

------------------------------------------------------------------------

# Description

`Revoke-DkvLease` immediately invalidates a previously granted lease.

Once revoked:

-   The lease becomes invalid.
-   Any items bound to that lease may lose ownership guarantees.
-   Operations that depend on the lease will fail.

Revoking a lease is typically used for:

-   releasing distributed locks
-   terminating leader election ownership
-   cleaning up coordination primitives
-   shutting down services that hold leases

------------------------------------------------------------------------

# Parameters

## -LeaseId

Specifies the lease identifier to revoke.

``` powershell
Type: LeaseId
Mandatory: True
Position: 0
```

Example:

``` powershell
Revoke-DkvLease -LeaseId $leaseId
```

------------------------------------------------------------------------

# Output

Returns an object describing the result of the revoke operation.

  Property   Description
  ---------- -----------------------------------------
  LeaseId    Lease identifier
  Revoked    Indicates whether the lease was revoked
  Success    Operation success flag
  Error      Error message if operation fails

Example output:

    LeaseId : 6e8d6a2f-ec55-4d41-b6c3-7b34b6a2d9f1
    Revoked : True
    Success : True
    Error   :

------------------------------------------------------------------------

# Examples

## Revoke a lease

``` powershell
Revoke-DkvLease -LeaseId $leaseId
```

------------------------------------------------------------------------

# Notes

-   Revoking a lease immediately invalidates it across the cluster.
-   Any operation requiring the revoked lease will fail afterward.
-   Press **Ctrl+C** to safely cancel the operation.

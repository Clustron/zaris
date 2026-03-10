# Refresh-DkvLease

## Synopsis

Refreshes (keeps alive) an existing **lease** in a Clustron Distributed
Key‑Value (DKV) store.

------------------------------------------------------------------------

# Syntax

``` powershell
Refresh-DkvLease [-LeaseId] <LeaseId>
```

------------------------------------------------------------------------

# Description

`Refresh-DkvLease` sends a **keep‑alive request** for an existing lease
to prevent it from expiring.

Leases in Clustron are time‑bound coordination primitives used for:

-   distributed locks
-   leader election
-   ownership semantics
-   coordination between services

Each lease has a **time‑to‑live (TTL)**.\
Calling `Refresh-DkvLease` resets the lease timer and keeps the lease
active.

If the lease has already expired or is invalid, the operation fails.

------------------------------------------------------------------------

# Parameters

## -LeaseId

Specifies the lease identifier to refresh.

``` powershell
Type: LeaseId
Mandatory: True
Position: 0
```

Example:

``` powershell
Refresh-DkvLease -LeaseId $leaseId
```

------------------------------------------------------------------------

# Output

Returns an object describing the result of the refresh operation.

  Property   Description
  ---------- -----------------------------------------------
  LeaseId    Lease identifier
  Success    Indicates whether the lease refresh succeeded
  Error      Error message if the operation fails

Example output:

    LeaseId : 8b13d8f6-3f61-4c8a-9b1b-2e6c91f2cde4
    Success : True
    Error   :

------------------------------------------------------------------------

# Examples

## Refresh an active lease

``` powershell
Refresh-DkvLease -LeaseId $leaseId
```

------------------------------------------------------------------------

# Notes

-   This command extends the lifetime of an existing lease.
-   If the lease has expired, the refresh operation fails.
-   Used by systems that implement **distributed locks or leader
    election**.
-   Press **Ctrl+C** to safely cancel the operation.

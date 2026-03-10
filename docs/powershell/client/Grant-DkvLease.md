# Grant-DkvLease

## Synopsis

Creates a **lease** in a Clustron Distributed Key-Value (DKV) store.

------------------------------------------------------------------------

# Syntax

``` powershell
Grant-DkvLease [-TimeToLive] <TimeSpan>
```

------------------------------------------------------------------------

# Description

`Grant-DkvLease` creates a lease with a specified time-to-live (TTL).

Leases are used to provide **temporary ownership semantics** in
distributed systems. They are commonly used for:

-   leader election
-   distributed locks
-   resource ownership
-   coordination between services

The lease expires automatically when its TTL elapses unless it is
renewed.

Once granted, the lease ID can be used with operations such as:

    Set-DkvItem -LeaseId <LeaseId>

------------------------------------------------------------------------

# Parameters

## -TimeToLive

Specifies the lifetime of the lease.

The lease automatically expires after this duration.

``` powershell
Type: TimeSpan
Mandatory: True
```

Example:

``` powershell
Grant-DkvLease -TimeToLive (New-TimeSpan -Seconds 30)
```

------------------------------------------------------------------------

# Output

Returns an object describing the lease creation result.

  Property     Description
  ------------ ----------------------------------
  LeaseId      Identifier of the created lease
  TtlSeconds   Lease TTL in seconds
  Success      Operation success flag
  Error        Error message if operation fails

Example output:

    LeaseId    : 8f1cba47-9c2a-4b1f-8f20-6d4f9e6d2b3f
    TtlSeconds : 30
    Success    : True
    Error      :

------------------------------------------------------------------------

# Examples

## Create a 30 second lease

``` powershell
Grant-DkvLease -TimeToLive (New-TimeSpan -Seconds 30)
```

------------------------------------------------------------------------

## Create a 5 minute lease

``` powershell
Grant-DkvLease -TimeToLive (New-TimeSpan -Minutes 5)
```

------------------------------------------------------------------------

# Notes

-   Leases expire automatically when TTL elapses.
-   A lease ID can be attached to items for ownership control.
-   Lease-based operations help implement distributed coordination
    patterns.
-   Press **Ctrl+C** to safely cancel the operation.

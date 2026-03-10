# Test-DkvLease

## Synopsis

Runs a **self-test suite for lease functionality** in a Clustron
Distributed Key-Value (DKV) store.

------------------------------------------------------------------------

# Syntax

``` powershell
Test-DkvLease
```

------------------------------------------------------------------------

# Description

`Test-DkvLease` executes a set of automated checks to validate the
behavior of the **lease subsystem** in a Clustron DKV cluster.

The command performs several lease-related scenarios including:

1.  **Grant Lease**
2.  **Attach Lease to Key**
3.  **Automatic Expiration**
4.  **Lease KeepAlive**
5.  **Lease Revocation**

Each step emits a test result indicating whether the behavior worked as
expected.

This cmdlet is useful for:

-   validating lease functionality in a cluster
-   verifying coordination primitives
-   testing cluster configuration
-   diagnosing lease-related issues

At the end of the test run, a summary result is emitted.

------------------------------------------------------------------------

# Output

The command produces two types of output:

### Individual Test Results

Each step produces an object describing the result.

  Property   Description
  ---------- -----------------------------------
  Test       Name of the test
  Success    Indicates whether the test passed

Example:

    Test        : GrantLease
    Success     : True

------------------------------------------------------------------------

### Final Summary

After all tests complete, a summary object is emitted.

  Property   Description
  ---------- ------------------------------------
  Total      Total number of tests
  Passed     Number of successful tests
  Failed     Number of failed tests
  Success    Indicates whether all tests passed

Example:

    Total   : 6
    Passed  : 6
    Failed  : 0
    Success : True

------------------------------------------------------------------------

# Examples

## Run lease validation

``` powershell
Test-DkvLease
```

------------------------------------------------------------------------

# Tests Performed

The command performs the following checks:

  Test                    Description
  ----------------------- ----------------------------------------------
  GrantLease              Verifies that a lease can be granted
  KeyExistsBeforeExpiry   Confirms key exists before lease expiration
  LeaseExpiryRemovesKey   Ensures key is removed when lease expires
  GrantLease2             Grants a second lease for keepalive testing
  KeepAliveExtendsLease   Verifies KeepAlive extends lease
  GrantLease3             Grants lease used for revoke test
  RevokeRemovesKey        Ensures revoking lease removes attached keys

------------------------------------------------------------------------

# Notes

-   This command **creates temporary keys in the store** during testing.
-   Lease expiration timing depends on cluster clock and scheduling.
-   The test stops immediately if a critical failure occurs.
-   Press **Ctrl+C** to cancel the test run safely.

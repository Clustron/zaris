# Stress-DkvStore

## Synopsis

Runs a **long‑duration stress test** against a Clustron Distributed
Key‑Value (DKV) store.

------------------------------------------------------------------------

# Syntax

``` powershell
Stress-DkvStore -StoreName <string> -DurationSec <int> -Concurrency <int> [-ObjectSizeBytes <int>]
```

------------------------------------------------------------------------

# Description

`Stress-DkvStore` executes a sustained workload against a Clustron store
to evaluate **system stability, reliability, and failure rates under
heavy load**.

Unlike short benchmarks, this command is designed for **long‑running
stress testing** and is typically used to:

-   validate cluster stability
-   detect memory leaks or resource exhaustion
-   test network resilience
-   evaluate node failure behavior
-   verify system behavior under prolonged load

The command continuously performs mixed read/write operations using
multiple concurrent workers.

The test runs until:

-   the specified duration expires, or
-   the user presses **Ctrl+C**.

------------------------------------------------------------------------

# Parameters

## -StoreName

Specifies the target store used for the stress test.

    Type: String
    Mandatory: True

Example:

``` powershell
Stress-DkvStore -StoreName TestStore -DurationSec 600 -Concurrency 32
```

------------------------------------------------------------------------

## -DurationSec

Specifies how long the stress test should run.

    Type: Int32
    Mandatory: True
    Range: 1 – 86400 seconds

Example:

``` powershell
Stress-DkvStore -StoreName TestStore -DurationSec 3600 -Concurrency 64
```

------------------------------------------------------------------------

## -Concurrency

Specifies the number of concurrent worker tasks generating load.

    Type: Int32
    Mandatory: True
    Range: 1 – 2048

Example:

``` powershell
Stress-DkvStore -StoreName TestStore -DurationSec 600 -Concurrency 128
```

------------------------------------------------------------------------

## -ObjectSizeBytes

Specifies the approximate size of objects used during the test.

    Type: Int32
    Default: 100 bytes
    Range: 32 – 1 MB

Example:

``` powershell
Stress-DkvStore -StoreName TestStore -DurationSec 600 -Concurrency 64 -ObjectSizeBytes 512
```

------------------------------------------------------------------------

# Output

Returns an object describing the stress test results.

  Property          Description
  ----------------- ---------------------------------
  StoreName         Target store
  DurationSec       Requested test duration
  Concurrency       Number of worker threads
  ObjectSizeBytes   Size of test objects
  TotalOperations   Total operations executed
  Failures          Number of failed operations
  ErrorRatePct      Percentage of failed operations
  LastError         Last encountered error type

Example output:

    StoreName      : TestStore
    DurationSec    : 600
    Concurrency    : 64
    ObjectSizeBytes: 100
    TotalOperations: 950000
    Failures       : 12
    ErrorRatePct   : 0.0012
    LastError      : TimeoutException

------------------------------------------------------------------------

# Examples

## Run a 10 minute stress test

``` powershell
Stress-DkvStore -StoreName TestStore -DurationSec 600 -Concurrency 32
```

------------------------------------------------------------------------

## Run a high‑concurrency stress test

``` powershell
Stress-DkvStore -StoreName TestStore -DurationSec 1800 -Concurrency 256
```

------------------------------------------------------------------------

## Test with larger objects

``` powershell
Stress-DkvStore -StoreName TestStore -DurationSec 900 -Concurrency 64 -ObjectSizeBytes 1024
```

------------------------------------------------------------------------

# Notes

-   This command generates **sustained heavy load** on the cluster.
-   It is intended for **testing environments** or controlled production
    validation.
-   Results depend on cluster size, hardware, and network conditions.
-   Press **Ctrl+C** to safely stop the test.

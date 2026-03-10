# Test-DkvThroughput

## Synopsis

Measures **operation throughput (OPS/sec)** of a Clustron Distributed
Key-Value (DKV) store under concurrent load.

------------------------------------------------------------------------

# Syntax

``` powershell
Test-DkvThroughput -StoreName <string> -DurationSec <int> -Concurrency <int> [-ObjectSizeBytes <int>]
```

------------------------------------------------------------------------

# Description

`Test-DkvThroughput` runs a workload benchmark against a Clustron store
and measures **operations per second**.

The command:

1.  Launches a configurable number of concurrent workers.
2.  Generates load against the store for the specified duration.
3.  Measures total operations and throughput.
4.  Reports the number of failed operations.

This command is useful for:

-   benchmarking cluster capacity
-   validating production deployments
-   comparing hardware/network configurations
-   performance testing before scaling

The benchmark runs until the configured duration expires or the user
presses **Ctrl+C**.

------------------------------------------------------------------------

# Parameters

## -StoreName

Specifies the target store used for the benchmark.

``` powershell
Type: String
Mandatory: True
```

Example:

``` powershell
Test-DkvThroughput -StoreName TestStore -DurationSec 30 -Concurrency 16
```

------------------------------------------------------------------------

## -DurationSec

Specifies how long the benchmark should run.

``` powershell
Type: Int32
Mandatory: True
Range: 1 – 3600 seconds
```

Example:

``` powershell
Test-DkvThroughput -StoreName TestStore -DurationSec 60 -Concurrency 32
```

------------------------------------------------------------------------

## -Concurrency

Specifies the number of concurrent worker tasks generating load.

``` powershell
Type: Int32
Mandatory: True
Range: 1 – 1024
```

Example:

``` powershell
Test-DkvThroughput -StoreName TestStore -DurationSec 30 -Concurrency 64
```

------------------------------------------------------------------------

## -ObjectSizeBytes

Specifies the approximate size of objects used in the benchmark.

``` powershell
Type: Int32
Default: 100 bytes
Range: 32 – 1 MB
```

Example:

``` powershell
Test-DkvThroughput -StoreName TestStore -DurationSec 30 -Concurrency 32 -ObjectSizeBytes 512
```

------------------------------------------------------------------------

# Output

Returns an object describing the benchmark results.

  Property               Description
  ---------------------- ------------------------------------
  StoreName              Target store
  DurationSecRequested   Requested benchmark duration
  DurationSecActual      Actual runtime
  Concurrency            Number of worker threads
  ObjectSizeBytes        Size of test objects
  TotalOperations        Total operations executed
  OpsPerSecond           Throughput (operations per second)
  Failures               Number of failed operations

Example output:

    StoreName           : TestStore
    DurationSecRequested: 30
    DurationSecActual   : 30.01
    Concurrency         : 32
    ObjectSizeBytes     : 100
    TotalOperations     : 125000
    OpsPerSecond        : 4166.53
    Failures            : 0

------------------------------------------------------------------------

# Examples

## Run a basic throughput test

``` powershell
Test-DkvThroughput -StoreName TestStore -DurationSec 30 -Concurrency 16
```

------------------------------------------------------------------------

## Run a high-concurrency test

``` powershell
Test-DkvThroughput -StoreName TestStore -DurationSec 60 -Concurrency 64
```

------------------------------------------------------------------------

## Benchmark larger objects

``` powershell
Test-DkvThroughput -StoreName TestStore -DurationSec 30 -Concurrency 32 -ObjectSizeBytes 1024
```

------------------------------------------------------------------------

# Notes

-   This command **generates load on the cluster** and should be used
    carefully in production environments.
-   Results depend on hardware, network latency, cluster size, and
    object size.
-   Press **Ctrl+C** to cancel the benchmark safely.

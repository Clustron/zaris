# Benchmark-DkvStore

## Synopsis

Runs a **configurable workload benchmark** against a Clustron
Distributed Key-Value (DKV) store.

------------------------------------------------------------------------

# Syntax

``` powershell
Benchmark-DkvStore -StoreName <string> -Profile <string> [-DurationSec <int>] [-Concurrency <int>] [-ObjectSizeBytes <int>]
```

------------------------------------------------------------------------

# Description

`Benchmark-DkvStore` executes a configurable workload against a Clustron
store and measures overall throughput.

Unlike simple latency or throughput tests, this command simulates
**realistic application workloads** using configurable profiles.

The benchmark runs concurrent operations for the specified duration and
reports total operations and throughput.

Supported workload profiles:

  Profile      Behavior
  ------------ ---------------------------
  ReadHeavy    \~90% reads, \~10% writes
  WriteHeavy   \~90% writes, \~10% reads
  Mixed        \~50% reads, \~50% writes

This command is useful for:

-   evaluating cluster performance
-   testing realistic application patterns
-   validating hardware capacity
-   performance tuning

The benchmark runs until the duration expires or the user presses
**Ctrl+C**.

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
Benchmark-DkvStore -StoreName TestStore -Profile Mixed
```

------------------------------------------------------------------------

## -Profile

Specifies the workload profile.

``` powershell
Type: String
Mandatory: True
Allowed values: ReadHeavy, WriteHeavy, Mixed
```

Example:

``` powershell
Benchmark-DkvStore -StoreName TestStore -Profile ReadHeavy
```

------------------------------------------------------------------------

## -DurationSec

Specifies how long the benchmark should run.

``` powershell
Type: Int32
Default: 60 seconds
Range: 1 – 3600
```

Example:

``` powershell
Benchmark-DkvStore -StoreName TestStore -Profile Mixed -DurationSec 120
```

------------------------------------------------------------------------

## -Concurrency

Specifies the number of concurrent worker tasks generating load.

``` powershell
Type: Int32
Default: 16
Range: 1 – 1024
```

Example:

``` powershell
Benchmark-DkvStore -StoreName TestStore -Profile Mixed -Concurrency 64
```

------------------------------------------------------------------------

## -ObjectSizeBytes

Specifies the approximate size of the objects used during benchmarking.

``` powershell
Type: Int32
Default: 100 bytes
Range: 32 – 1 MB
```

Example:

``` powershell
Benchmark-DkvStore -StoreName TestStore -Profile Mixed -ObjectSizeBytes 512
```

------------------------------------------------------------------------

# Output

Returns an object describing the benchmark results.

  Property               Description
  ---------------------- ------------------------------------
  StoreName              Target store
  Profile                Workload profile used
  DurationSecRequested   Requested runtime
  DurationSecActual      Actual runtime
  Concurrency            Number of workers
  ObjectSizeBytes        Size of test objects
  TotalOperations        Total operations executed
  OpsPerSecond           Throughput (operations per second)
  Failures               Number of failed operations

Example output:

    StoreName           : TestStore
    Profile             : Mixed
    DurationSecRequested: 60
    DurationSecActual   : 60.01
    Concurrency         : 16
    ObjectSizeBytes     : 100
    TotalOperations     : 320000
    OpsPerSecond        : 5333.12
    Failures            : 0

------------------------------------------------------------------------

# Examples

## Run a mixed workload benchmark

``` powershell
Benchmark-DkvStore -StoreName TestStore -Profile Mixed
```

------------------------------------------------------------------------

## Run a read-heavy workload

``` powershell
Benchmark-DkvStore -StoreName TestStore -Profile ReadHeavy -Concurrency 32
```

------------------------------------------------------------------------

## Run a write-heavy benchmark with larger objects

``` powershell
Benchmark-DkvStore -StoreName TestStore -Profile WriteHeavy -ObjectSizeBytes 1024
```

------------------------------------------------------------------------

# Notes

-   This command **generates load on the cluster** and should be used
    carefully in production.
-   Throughput results depend heavily on hardware, network latency, and
    cluster size.
-   Press **Ctrl+C** to cancel the benchmark safely.

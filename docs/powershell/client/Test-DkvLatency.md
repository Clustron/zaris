# Test-DkvLatency

## Synopsis

Measures **read latency statistics** for a Clustron Distributed
Key-Value (DKV) store.

------------------------------------------------------------------------

# Syntax

``` powershell
Test-DkvLatency -StoreName <string> -Iterations <int> [-ObjectSizeBytes <int>]
```

------------------------------------------------------------------------

# Description

`Test-DkvLatency` performs a latency benchmark against a Clustron store
by:

1.  **Pre-populating** the store with test objects.
2.  Performing a short **warm‑up phase**.
3.  Measuring **read latency for each request**.
4.  Calculating latency statistics including **min, average, P95, P99,
    and max**.

This command is useful for:

-   evaluating cluster performance
-   validating deployment latency
-   comparing hardware or network environments
-   diagnosing slow responses

The benchmark performs sequential read operations and reports latency in
**milliseconds**.

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
Test-DkvLatency -StoreName TestStore -Iterations 1000
```

------------------------------------------------------------------------

## -Iterations

Specifies the number of operations to measure.

``` powershell
Type: Int32
Mandatory: True
Range: 1 – 1,000,000
```

Example:

``` powershell
Test-DkvLatency -StoreName TestStore -Iterations 5000
```

------------------------------------------------------------------------

## -ObjectSizeBytes

Specifies the approximate size of each stored object.

``` powershell
Type: Int32
Default: 100
Range: 32 – 1 MB
```

Example:

``` powershell
Test-DkvLatency -StoreName TestStore -Iterations 10000 -ObjectSizeBytes 512
```

------------------------------------------------------------------------

# Output

Returns an object containing latency statistics.

  Property          Description
  ----------------- -------------------------------
  StoreName         Target store
  Iterations        Number of operations measured
  ObjectSizeBytes   Size of objects used
  MinMs             Minimum latency
  AvgMs             Average latency
  P95Ms             95th percentile latency
  P99Ms             99th percentile latency
  MaxMs             Maximum latency

Example output:

    StoreName        : TestStore
    Iterations       : 1000
    ObjectSizeBytes  : 100
    MinMs            : 0.21
    AvgMs            : 0.45
    P95Ms            : 0.72
    P99Ms            : 1.10
    MaxMs            : 1.48

------------------------------------------------------------------------

# Examples

## Basic latency benchmark

``` powershell
Test-DkvLatency -StoreName TestStore -Iterations 1000
```

------------------------------------------------------------------------

## Test latency with larger objects

``` powershell
Test-DkvLatency -StoreName TestStore -Iterations 5000 -ObjectSizeBytes 1024
```

------------------------------------------------------------------------

# Notes

-   The command **writes benchmark data into the store** during
    pre‑population.
-   The benchmark performs **read latency measurement**.
-   Results depend heavily on network latency and cluster size.
-   Press **Ctrl+C** to cancel the benchmark safely.

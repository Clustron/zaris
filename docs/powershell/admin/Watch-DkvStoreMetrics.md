# Watch-DkvStoreMetrics

## Synopsis

Displays **live runtime metrics** for a Clustron Distributed Key-Value
(DKV) store in a continuously updating console view.

## Description

`Watch-DkvStoreMetrics` provides a **real‑time monitoring view** of
store metrics across cluster nodes.

The cmdlet periodically queries the Clustron manager API and renders a
**metrics table** in the PowerShell console. The display updates
continuously at a configurable refresh interval.

Metrics are grouped by:

-   **Category**
-   **Metric name**
-   **Cluster node**

Each metric cell displays:

    Rate/sec (Total)

Example:

    2,341/s (134,223)

Where:

-   `Rate/sec` is the average rate calculated over the recent sampling
    window
-   `Total` is the cumulative metric value

The view automatically refreshes in place until the command is cancelled
(Ctrl+C).

Internally the cmdlet queries the manager metrics endpoint:

    GET /admin/v1/metrics?windowSec=5

------------------------------------------------------------------------

# Syntax

``` powershell
Watch-DkvStoreMetrics -StoreName <string> [-RefreshSec <int>] [-Servers <string[]>]
```

Optional parameters inherited from `DkvCmdletBase`:

``` powershell
[-Port <int>] [-TimeoutSec <int>]
```

------------------------------------------------------------------------

# Parameters

## -StoreName

Name of the store whose metrics should be monitored.

Example:

    OrdersStore

Required: **Yes**

------------------------------------------------------------------------

## -RefreshSec

Refresh interval in seconds for updating the metrics display.

Valid range:

    1 – 60 seconds

Default:

    1 second

Example:

``` powershell
-RefreshSec 2
```

------------------------------------------------------------------------

## -Servers

Target Clustron manager servers.

Example:

``` powershell
-Servers 10.0.0.11,10.0.0.12
```

If omitted, the cmdlet uses the active `Connect-DkvManager` session.

------------------------------------------------------------------------

## -Port

Manager API port used when resolving servers.

Default:

    7800

------------------------------------------------------------------------

## -TimeoutSec

Maximum time allowed for a metrics request.

Default:

    30 seconds

------------------------------------------------------------------------

# Examples

## Example 1 --- Monitor store metrics

``` powershell
Watch-DkvStoreMetrics -StoreName OrdersStore
```

The console updates continuously with live metrics.

------------------------------------------------------------------------

## Example 2 --- Monitor metrics with slower refresh

``` powershell
Watch-DkvStoreMetrics -StoreName OrdersStore -RefreshSec 3
```

Updates every 3 seconds.

------------------------------------------------------------------------

## Example 3 --- Monitor metrics from specific manager nodes

``` powershell
Watch-DkvStoreMetrics `
    -StoreName OrdersStore `
    -Servers 10.0.0.11,10.0.0.12
```

------------------------------------------------------------------------

## Example 4 --- Monitor metrics using a connected manager session

``` powershell
Connect-DkvManager -Servers 10.0.0.11,10.0.0.12

Watch-DkvStoreMetrics -StoreName OrdersStore
```

------------------------------------------------------------------------

# Example Output

    Store: OrdersStore    Updated: 15:32:11

    Category            Metric                      10.0.0.11/nodeA        10.0.0.12/nodeB
    ---------------------------------------------------------------------------------------
    Requests            Get                         2,100/s (154,002)      1,980/s (149,210)
    Requests            Put                         980/s (74,221)         1,020/s (76,002)
    Requests            Remove                      210/s (12,341)         198/s (11,900)
    Cache               ItemsCount                  - (2,340,122)          - (2,345,001)

------------------------------------------------------------------------

# Metric Display Format

Metrics are shown in the format:

    Rate/sec (Total)

Example:

    2,341/s (134,223)

  Value      Meaning
  ---------- -------------------------------
  Rate/sec   Average operations per second
  Total      Total cumulative value

If a rate cannot be calculated, the display shows:

    - (Total)

------------------------------------------------------------------------

# Behavior

### Continuous Display

The console is cleared once and the cursor position is reset on each
refresh cycle so the table updates **in place**.

### Cancellation

Press **Ctrl+C** to stop the monitoring session.

### Metrics Filtering

Only metrics belonging to the specified **StoreName** are displayed.

### Node Identification

Nodes appear in the format:

    serverHost/shortNodeId

Example:

    10.0.0.11/5f7c1a92

------------------------------------------------------------------------

# Notes

### Manager Connection Requirement

If neither `-Servers` nor an active `Connect-DkvManager` session exists,
the cmdlet terminates with an error:

    No managers connected. Use Connect-DkvManager or specify -Servers.

------------------------------------------------------------------------

### Recommended Usage

This cmdlet is typically used during:

-   Performance testing
-   Capacity planning
-   Production diagnostics
-   Cluster troubleshooting

------------------------------------------------------------------------

# Related Cmdlets

-   Connect-DkvManager
-   Get-DkvStore
-   New-DkvStore
-   Add-DkvInstance
-   Start-DkvStore
-   Stop-DkvStore

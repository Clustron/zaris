# Get-DkvStore

## Synopsis

Retrieves information about **Clustron Distributed Key-Value (DKV)
stores** and their runtime status.

## Description

`Get-DkvStore` queries the Clustron manager API and returns a summary of
stores running in the cluster.

The cmdlet can be used in two ways:

1.  **List all stores** in the cluster.
2.  **Retrieve details for a specific store**.

For each store, the cmdlet produces a **summary view** containing:

-   Store name
-   Overall store status
-   Instance counts
-   Running instance count
-   Stopped instance count
-   Participating node count

The command queries one or more manager servers resolved using:

-   `-Servers` parameter
-   Active `Connect-DkvManager` session
-   Fallback resolution from `DkvCmdletBase`

The cmdlet calls the following manager APIs:

    GET /admin/v1/stores
    GET /admin/v1/stores/{StoreName}

------------------------------------------------------------------------

# Syntax

### List all stores

``` powershell
Get-DkvStore
```

### Retrieve a specific store

``` powershell
Get-DkvStore -Name <string>
```

Optional parameters inherited from `DkvCmdletBase`:

``` powershell
[-Servers <string[]>] [-Port <int>] [-TimeoutSec <int>] [-Parallel]
```

------------------------------------------------------------------------

# Parameters

## -Name

Name of the store to retrieve.

If omitted, the cmdlet returns **all stores** in the cluster.

Example:

    OrdersStore

Required: **No**

------------------------------------------------------------------------

## -Servers

Target Clustron manager servers.

Example:

``` powershell
-Servers 10.0.0.11,10.0.0.12
```

If omitted, the cmdlet uses the current `Connect-DkvManager` session
context.

------------------------------------------------------------------------

## -Port

Manager API port used when resolving servers.

Default:

    7800

------------------------------------------------------------------------

## -TimeoutSec

Maximum time allowed for the request.

Default:

    30 seconds

------------------------------------------------------------------------

# Examples

## Example 1 --- List all stores

``` powershell
Get-DkvStore
```

Example output:

    Server             : http://10.0.0.11:7800
    StoreName          : OrdersStore
    StoreStatus        : Running
    InstanceCount      : 3
    RunningInstances   : 3
    StoppedInstances   : 0
    ParticipatingNodes : 1

------------------------------------------------------------------------

## Example 2 --- Get a specific store

``` powershell
Get-DkvStore -Name OrdersStore
```

------------------------------------------------------------------------

## Example 3 --- Query specific manager servers

``` powershell
Get-DkvStore -Servers 10.0.0.11,10.0.0.12
```

------------------------------------------------------------------------

## Example 4 --- Use a connected manager session

``` powershell
Connect-DkvManager -Servers 10.0.0.11,10.0.0.12

Get-DkvStore
```

------------------------------------------------------------------------

## Example 5 --- Retrieve a store from multiple manager nodes

``` powershell
Get-DkvStore `
    -Name OrdersStore `
    -Servers 10.0.0.11,10.0.0.12,10.0.0.13
```

------------------------------------------------------------------------

# Output

The cmdlet returns an **object summary per store**.

Properties include:

  Property             Description
  -------------------- -----------------------------------
  Server               Manager server used for the query
  StoreName            Name of the store
  StoreStatus          Overall store state
  InstanceCount        Total number of instances
  RunningInstances     Number of running instances
  StoppedInstances     Number of stopped instances
  ParticipatingNodes   Number of nodes participating

------------------------------------------------------------------------

# Store Status Calculation

The store status is derived from instance states:

  Condition      Status
  -------------- ---------------
  No instances   `NoInstances`
  All running    `Running`
  All stopped    `Stopped`
  Mixed states   `Partial`

------------------------------------------------------------------------

# Example Cluster

  Node        Instance        Status
  ----------- --------------- ---------
  10.0.0.11   orders-node-1   Running
  10.0.0.12   orders-node-2   Running
  10.0.0.13   orders-node-3   Running

Result:

    StoreStatus : Running
    InstanceCount : 3
    RunningInstances : 3
    StoppedInstances : 0

------------------------------------------------------------------------

# Notes

### Manager Context Requirement

If no `-Servers` parameter is provided and no manager session exists,
the cmdlet will require a connection via:

``` powershell
Connect-DkvManager
```

------------------------------------------------------------------------

### Summary Output

The cmdlet intentionally returns a **summary object** instead of raw
JSON to make the output easy to read in PowerShell pipelines.

------------------------------------------------------------------------

# Related Cmdlets

-   Connect-DkvManager
-   New-DkvStore
-   Add-DkvInstance
-   Start-DkvStore
-   Stop-DkvStore
-   Watch-DkvStoreMetrics

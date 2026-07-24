# Clustron Admin PowerShell Cmdlets

The **Clustron.Dictus.AdminShell** PowerShell module provides
administrative commands for managing **Clustron Distributed Key-Value
(Dictus) stores and instances**.

These cmdlets allow administrators and DevOps engineers to:

-   Connect to Clustron management servers
-   Create and configure distributed stores
-   Add store instances across cluster nodes
-   Start and stop store instances
-   Inspect store status
-   Monitor live runtime metrics

The module is designed for **cluster administration, automation, and
operational diagnostics**.

------------------------------------------------------------------------

# Module

Clustron.Dictus.AdminShell

Load the module:

``` powershell
Import-Module Clustron.Dictus.AdminShell
```

List available commands:

``` powershell
Get-Command -Module Clustron.Dictus.AdminShell
```

------------------------------------------------------------------------

# Cmdlet Categories

Administrative cmdlets are organized by operational responsibility.

------------------------------------------------------------------------

# Connection Management

These commands establish a connection to one or more **Clustron
management servers**.\
Once connected, subsequent commands automatically use this context.

  -----------------------------------------------------------------------
  Cmdlet                 Description
  ---------------------- ------------------------------------------------
  `Connect-DictusManager`   Connects the PowerShell session to one or more
                         Clustron managers

  -----------------------------------------------------------------------

Example:

``` powershell
Connect-DictusManager -Servers 10.0.0.11,10.0.0.12
```

------------------------------------------------------------------------

# Store Management

Commands used to create, start, stop, and inspect distributed stores.

  Cmdlet             Description
  ------------------ --------------------------------------------------
  `New-DictusStore`     Creates a new distributed store
  `Get-DictusStore`     Retrieves store configuration and runtime status
  `Start-DictusStore`   Starts store instances
  `Stop-DictusStore`    Stops store instances

Example:

``` powershell
New-DictusStore `
    -Name OrdersStore `
    -InstanceName orders-node-1 `
    -ClustronPort 7001 `
    -ClientPort 7101

Start-DictusStore -Name OrdersStore

Get-DictusStore -Name OrdersStore
```

------------------------------------------------------------------------

# Instance Management

Commands used to expand an existing store by adding instances to new
cluster nodes.

  Cmdlet              Description
  ------------------- -------------------------------------------------
  `Add-DictusInstance`   Adds one or more instances to an existing store

Example:

``` powershell
Add-DictusInstance `
    -StoreName OrdersStore `
    -InstanceName orders-node-2 `
    -ClustronPort 7002 `
    -ClientPort 7102
```

------------------------------------------------------------------------

# Monitoring

Commands used to observe live runtime metrics from cluster nodes.

  --------------------------------------------------------------------------
  Cmdlet                    Description
  ------------------------- ------------------------------------------------
  `Watch-DictusStoreMetrics`   Displays continuously updating runtime metrics
                            for a store

  --------------------------------------------------------------------------

Example:

``` powershell
Watch-DictusStoreMetrics -StoreName OrdersStore
```

The display updates continuously and can be stopped using **Ctrl+C**.

------------------------------------------------------------------------

# Typical Administrative Workflow

A typical operational workflow when managing a store looks like the
following.

## 1 Connect to the management servers

``` powershell
Connect-DictusManager -Servers 10.0.0.11,10.0.0.12
```

------------------------------------------------------------------------

## 2 Create a store

``` powershell
New-DictusStore `
    -Name OrdersStore `
    -InstanceName orders-node-1 `
    -ClustronPort 7001 `
    -ClientPort 7101
```

------------------------------------------------------------------------

## 3 Add additional instances

``` powershell
Add-DictusInstance `
    -StoreName OrdersStore `
    -InstanceName orders-node-2 `
    -ClustronPort 7002 `
    -ClientPort 7102

Add-DictusInstance `
    -StoreName OrdersStore `
    -InstanceName orders-node-3 `
    -ClustronPort 7003 `
    -ClientPort 7103
```

------------------------------------------------------------------------

## 4 Start the store

``` powershell
Start-DictusStore -Name OrdersStore
```

------------------------------------------------------------------------

## 5 Verify store status

``` powershell
Get-DictusStore
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

## 6 Monitor runtime metrics

``` powershell
Watch-DictusStoreMetrics -StoreName OrdersStore
```

This displays a live metrics table showing operations per second and
totals across cluster nodes.

------------------------------------------------------------------------

# Example Cluster Layout

The examples in this documentation assume a cluster similar to the
following:

  Node        Instance        ClustronPort   ClientPort
  ----------- --------------- -------------- ------------
  10.0.0.11   orders-node-1   7001           7101
  10.0.0.12   orders-node-2   7002           7102
  10.0.0.13   orders-node-3   7003           7103

------------------------------------------------------------------------

# Cmdlet Reference

Detailed documentation for each cmdlet is available in the following
files.

-   Connect-DictusManager.md
-   New-DictusStore.md
-   Add-DictusInstance.md
-   Start-DictusStore.md
-   Stop-DictusStore.md
-   Get-DictusStore.md
-   Watch-DictusStoreMetrics.md

------------------------------------------------------------------------

# Documentation Structure

docs/ └─ powershell/ └─ admin/ ├─ README.md ├─ Connect-DictusManager.md ├─
New-DictusStore.md ├─ Add-DictusInstance.md ├─ Start-DictusStore.md ├─
Stop-DictusStore.md ├─ Get-DictusStore.md └─ Watch-DictusStoreMetrics.md

Each file documents a single cmdlet and includes:

-   Synopsis
-   Syntax
-   Parameters
-   Examples
-   Output
-   Notes

This structure ensures the documentation remains **consistent,
searchable, and easy to navigate**.

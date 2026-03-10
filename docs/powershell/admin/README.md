# Clustron Admin PowerShell Cmdlets

The **Clustron.DKV.AdminShell** PowerShell module provides
administrative commands for managing **Clustron Distributed Key-Value
(DKV) stores and instances**.

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

Clustron.DKV.AdminShell

Load the module:

``` powershell
Import-Module Clustron.DKV.AdminShell
```

List available commands:

``` powershell
Get-Command -Module Clustron.DKV.AdminShell
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
  `Connect-DkvManager`   Connects the PowerShell session to one or more
                         Clustron managers

  -----------------------------------------------------------------------

Example:

``` powershell
Connect-DkvManager -Servers 10.0.0.11,10.0.0.12
```

------------------------------------------------------------------------

# Store Management

Commands used to create, start, stop, and inspect distributed stores.

  Cmdlet             Description
  ------------------ --------------------------------------------------
  `New-DkvStore`     Creates a new distributed store
  `Get-DkvStore`     Retrieves store configuration and runtime status
  `Start-DkvStore`   Starts store instances
  `Stop-DkvStore`    Stops store instances

Example:

``` powershell
New-DkvStore `
    -Name OrdersStore `
    -InstanceName orders-node-1 `
    -ClustronPort 7001 `
    -ClientPort 7101

Start-DkvStore -Name OrdersStore

Get-DkvStore -Name OrdersStore
```

------------------------------------------------------------------------

# Instance Management

Commands used to expand an existing store by adding instances to new
cluster nodes.

  Cmdlet              Description
  ------------------- -------------------------------------------------
  `Add-DkvInstance`   Adds one or more instances to an existing store

Example:

``` powershell
Add-DkvInstance `
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
  `Watch-DkvStoreMetrics`   Displays continuously updating runtime metrics
                            for a store

  --------------------------------------------------------------------------

Example:

``` powershell
Watch-DkvStoreMetrics -StoreName OrdersStore
```

The display updates continuously and can be stopped using **Ctrl+C**.

------------------------------------------------------------------------

# Typical Administrative Workflow

A typical operational workflow when managing a store looks like the
following.

## 1 Connect to the management servers

``` powershell
Connect-DkvManager -Servers 10.0.0.11,10.0.0.12
```

------------------------------------------------------------------------

## 2 Create a store

``` powershell
New-DkvStore `
    -Name OrdersStore `
    -InstanceName orders-node-1 `
    -ClustronPort 7001 `
    -ClientPort 7101
```

------------------------------------------------------------------------

## 3 Add additional instances

``` powershell
Add-DkvInstance `
    -StoreName OrdersStore `
    -InstanceName orders-node-2 `
    -ClustronPort 7002 `
    -ClientPort 7102

Add-DkvInstance `
    -StoreName OrdersStore `
    -InstanceName orders-node-3 `
    -ClustronPort 7003 `
    -ClientPort 7103
```

------------------------------------------------------------------------

## 4 Start the store

``` powershell
Start-DkvStore -Name OrdersStore
```

------------------------------------------------------------------------

## 5 Verify store status

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

## 6 Monitor runtime metrics

``` powershell
Watch-DkvStoreMetrics -StoreName OrdersStore
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

-   Connect-DkvManager.md
-   New-DkvStore.md
-   Add-DkvInstance.md
-   Start-DkvStore.md
-   Stop-DkvStore.md
-   Get-DkvStore.md
-   Watch-DkvStoreMetrics.md

------------------------------------------------------------------------

# Documentation Structure

docs/ └─ powershell/ └─ admin/ ├─ README.md ├─ Connect-DkvManager.md ├─
New-DkvStore.md ├─ Add-DkvInstance.md ├─ Start-DkvStore.md ├─
Stop-DkvStore.md ├─ Get-DkvStore.md └─ Watch-DkvStoreMetrics.md

Each file documents a single cmdlet and includes:

-   Synopsis
-   Syntax
-   Parameters
-   Examples
-   Output
-   Notes

This structure ensures the documentation remains **consistent,
searchable, and easy to navigate**.

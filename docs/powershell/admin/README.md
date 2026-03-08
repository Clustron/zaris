# Clustron Admin PowerShell Cmdlets

The **Clustron.DKV.AdminShell** PowerShell module provides
administrative commands for managing Clustron distributed stores and
their instances.

These cmdlets allow administrators and operators to:

-   Connect to the Clustron management service
-   Create and manage distributed stores
-   Add store instances
-   Start and stop stores
-   Monitor runtime metrics

This module is intended for **cluster administrators, DevOps engineers,
and automation scripts** that manage Clustron deployments.

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

These commands connect PowerShell to the Clustron management service.

  -----------------------------------------------------------------------
  Cmdlet                 Description
  ---------------------- ------------------------------------------------
  `Connect-DkvManager`   Connects PowerShell to the Clustron management
                         service

  -----------------------------------------------------------------------

Example:

``` powershell
Connect-DkvManager 127.0.0.1:7801
```

------------------------------------------------------------------------

# Store Management

Commands used to create, start, stop, and inspect distributed stores.

  Cmdlet             Description
  ------------------ --------------------------------------------------
  `New-DkvStore`     Creates a new distributed store
  `Get-DkvStore`     Retrieves store configuration and runtime status
  `Start-DkvStore`   Starts a store
  `Stop-DkvStore`    Stops a running store

Example:

``` powershell
New-DkvStore -Name TestStore

Start-DkvStore -Name TestStore

Get-DkvStore -Name TestStore
```

------------------------------------------------------------------------

# Instance Management

Commands used to add instances to an existing store.

  Cmdlet              Description
  ------------------- ---------------------------------------
  `Add-DkvInstance`   Adds a new instance (node) to a store

Example:

``` powershell
Add-DkvInstance -StoreName TestStore -Server node1
```

------------------------------------------------------------------------

# Monitoring

Commands used to observe runtime metrics for stores.

  --------------------------------------------------------------------------
  Cmdlet                    Description
  ------------------------- ------------------------------------------------
  `Watch-DkvStoreMetrics`   Streams live performance metrics for a store

  --------------------------------------------------------------------------

Example:

``` powershell
Watch-DkvStoreMetrics -StoreName TestStore
```

------------------------------------------------------------------------

# Typical Administrative Workflow

A typical administrative workflow using the AdminShell module looks like
the following.

Connect to the management service:

``` powershell
Connect-DkvManager -Server localhost -Port 7071
```

Create a store:

``` powershell
New-DkvStore -Name TestStore
```

Add instances:

``` powershell
Add-DkvInstance -StoreName TestStore -Server node1
Add-DkvInstance -StoreName TestStore -Server node2
```

Start the store:

``` powershell
Start-DkvStore -Name TestStore
```

Verify store status:

``` powershell
Get-DkvStore -Name TestStore
```

Monitor metrics:

``` powershell
Watch-DkvStoreMetrics -StoreName TestStore
```

------------------------------------------------------------------------

# Cmdlet Reference

Detailed documentation for each cmdlet is available in the following
files.

-   Add-DkvInstance.md
-   Connect-DkvManager.md
-   Get-DkvStore.md
-   New-DkvStore.md
-   Start-DkvStore.md
-   Stop-DkvStore.md
-   Watch-DkvStoreMetrics.md

------------------------------------------------------------------------

# Documentation Structure

    docs/
     └─ powershell/
          └─ admin/
               ├─ README.md
               ├─ Add-DkvInstance.md
               ├─ Connect-DkvManager.md
               ├─ Get-DkvStore.md
               ├─ New-DkvStore.md
               ├─ Start-DkvStore.md
               ├─ Stop-DkvStore.md
               └─ Watch-DkvStoreMetrics.md

Each file documents a single cmdlet including:

-   Synopsis
-   Syntax
-   Parameters
-   Examples
-   Remarks

This structure makes the documentation easy to navigate and consistent
with professional PowerShell documentation practices.

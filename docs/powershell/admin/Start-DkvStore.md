# Start-DkvStore

## Synopsis

Starts one or more instances of a **Clustron Distributed Key‑Value (DKV)
store**.

## Description

`Start-DkvStore` starts instances of an existing Clustron store on one
or more manager servers.

The cmdlet performs the following steps internally:

1.  Resolves the target manager servers.
2.  Queries the manager to discover the store configuration.
3.  Determines which instances should be started.
4.  Sends start commands to each instance.

The cmdlet can start:

-   **All instances of a store**
-   **A specific instance**

Execution can be performed:

-   Sequentially (default)
-   In parallel using `-Parallel`

If an operation fails, execution can optionally stop immediately using
`-FailFast`.

The cmdlet outputs one **DkvAdminResult** per instance start operation.

------------------------------------------------------------------------

# Syntax

### Start all instances

``` powershell
Start-DkvStore -Name <string>
```

### Start a specific instance

``` powershell
Start-DkvStore -Name <string> -InstanceName <string>
```

Optional execution parameters inherited from `DkvCmdletBase`:

``` powershell
[-Servers <string[]>] [-Port <int>] [-TimeoutSec <int>] [-Parallel] [-FailFast]
```

------------------------------------------------------------------------

# Parameters

## -Name

Name of the store whose instances should be started.

Example:

    OrdersStore

Required: **Yes**

------------------------------------------------------------------------

## -InstanceName

Name of a specific instance to start.

If omitted, **all instances of the store will be started**.

Example:

    orders-node-1

Required: **No**

------------------------------------------------------------------------

## -Servers

Target Clustron manager servers.

Example:

``` powershell
-Servers 10.0.0.11,10.0.0.12
```

If not specified, the cmdlet uses the active `Connect-DkvManager`
session.

------------------------------------------------------------------------

## -Port

Management API port used for the manager servers.

Default:

    7800

------------------------------------------------------------------------

## -TimeoutSec

Maximum allowed time for the administrative operation.

Default:

    30 seconds

------------------------------------------------------------------------

## -Parallel

Executes the operation against multiple servers **concurrently**.

Useful for clusters with many manager nodes.

------------------------------------------------------------------------

## -FailFast

Stops execution immediately when a failure occurs.

Without this flag the cmdlet continues processing remaining servers.

------------------------------------------------------------------------

# Examples

## Example 1 --- Start all instances of a store

``` powershell
Start-DkvStore -Name OrdersStore
```

This starts every instance belonging to the store.

------------------------------------------------------------------------

## Example 2 --- Start a specific instance

``` powershell
Start-DkvStore -Name OrdersStore -InstanceName orders-node-1
```

Only the specified instance will be started.

------------------------------------------------------------------------

## Example 3 --- Start store instances using a connected manager session

``` powershell
Connect-DkvManager -Servers 10.0.0.11,10.0.0.12

Start-DkvStore -Name OrdersStore
```

The command automatically uses the connected manager context.

------------------------------------------------------------------------

## Example 4 --- Start instances on specific manager servers

``` powershell
Start-DkvStore `
    -Name OrdersStore `
    -Servers 10.0.0.11,10.0.0.12
```

------------------------------------------------------------------------

## Example 5 --- Start instances across multiple servers in parallel

``` powershell
Start-DkvStore `
    -Name OrdersStore `
    -Servers 10.0.0.11,10.0.0.12,10.0.0.13 `
    -Parallel
```

------------------------------------------------------------------------

## Example 6 --- Stop execution if any instance fails

``` powershell
Start-DkvStore `
    -Name OrdersStore `
    -FailFast
```

------------------------------------------------------------------------

# Output

The cmdlet returns one **DkvAdminResult** object per instance start
operation.

Typical properties include:

  Property     Description
  ------------ -----------------------
  Server       Target manager server
  Operation    Operation name
  Success      Indicates success
  StatusCode   HTTP status code
  Message      Result message

Example output:

    Server      : http://10.0.0.11:7800
    Operation   : StartInstance:orders-node-1
    Success     : True
    StatusCode  : 200
    Message     : Instance started successfully

------------------------------------------------------------------------

# Notes

### Manager Context Requirement

If neither `-Servers` nor an active `Connect-DkvManager` session is
present, the cmdlet will terminate with an error.

Example error:

    No managers connected. Use Connect-DkvManager or specify -Servers.

------------------------------------------------------------------------

### Instance Discovery

The cmdlet first queries the manager API:

    GET /admin/v1/stores/{StoreName}

This returns the list of instances associated with the store.

------------------------------------------------------------------------

### Instance Start API

Each instance is started using the manager API:

    POST /admin/v1/stores/{StoreName}/instances/{InstanceName}/start

------------------------------------------------------------------------

### Example Production Cluster

  Node        Instance        ClustronPort   ClientPort
  ----------- --------------- -------------- ------------
  10.0.0.11   orders-node-1   7001           7101
  10.0.0.12   orders-node-2   7002           7102
  10.0.0.13   orders-node-3   7003           7103

Starting the store without `-InstanceName` starts all instances.

------------------------------------------------------------------------

# Related Cmdlets

-   Connect-DkvManager
-   New-DkvStore
-   Add-DkvInstance
-   Stop-DkvStore
-   Get-DkvStore
-   Watch-DkvStoreMetrics

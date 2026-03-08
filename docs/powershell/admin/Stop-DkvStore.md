# Stop-DkvStore

## Synopsis

Stops one or more instances of a **Clustron Distributed Key-Value (DKV)
store**.

## Description

`Stop-DkvStore` stops running instances of a Clustron store on one or
more manager servers.

The cmdlet performs the following steps:

1.  Resolves the target manager servers.
2.  Queries the manager to discover the store configuration.
3.  Determines which instances should be stopped.
4.  Sends stop commands to each instance.

The cmdlet can stop:

-   **All instances of a store**
-   **A specific instance**

Execution can be performed:

-   Sequentially (default)
-   In parallel using `-Parallel`

If an operation fails, execution can optionally stop immediately using
`-FailFast`.

The cmdlet outputs one **DkvAdminResult** per instance stop operation.

------------------------------------------------------------------------

# Syntax

### Stop all instances

``` powershell
Stop-DkvStore -Name <string>
```

### Stop a specific instance

``` powershell
Stop-DkvStore -Name <string> -InstanceName <string>
```

Optional execution parameters inherited from `DkvCmdletBase`:

``` powershell
[-Servers <string[]>] [-Port <int>] [-TimeoutSec <int>] [-Parallel] [-FailFast]
```

------------------------------------------------------------------------

# Parameters

## -Name

Name of the store whose instances should be stopped.

Example:

    OrdersStore

Required: **Yes**

------------------------------------------------------------------------

## -InstanceName

Name of a specific instance to stop.

If omitted, **all instances of the store will be stopped**.

Example:

    orders-node-1

Required: **No**

------------------------------------------------------------------------

## -Force

Forces the instance to stop immediately.

When specified, the manager API will be called with:

    ?force=true

This bypasses graceful shutdown checks and immediately terminates the
instance.

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

## Example 1 --- Stop all instances of a store

``` powershell
Stop-DkvStore -Name OrdersStore
```

------------------------------------------------------------------------

## Example 2 --- Stop a specific instance

``` powershell
Stop-DkvStore -Name OrdersStore -InstanceName orders-node-1
```

------------------------------------------------------------------------

## Example 3 --- Force stop an instance

``` powershell
Stop-DkvStore -Name OrdersStore -InstanceName orders-node-1 -Force
```

This immediately terminates the instance.

------------------------------------------------------------------------

## Example 4 --- Stop instances using a connected manager session

``` powershell
Connect-DkvManager -Servers 10.0.0.11,10.0.0.12

Stop-DkvStore -Name OrdersStore
```

------------------------------------------------------------------------

## Example 5 --- Stop store instances on specific manager servers

``` powershell
Stop-DkvStore `
    -Name OrdersStore `
    -Servers 10.0.0.11,10.0.0.12
```

------------------------------------------------------------------------

## Example 6 --- Stop instances across servers in parallel

``` powershell
Stop-DkvStore `
    -Name OrdersStore `
    -Servers 10.0.0.11,10.0.0.12,10.0.0.13 `
    -Parallel
```

------------------------------------------------------------------------

## Example 7 --- Stop execution on first failure

``` powershell
Stop-DkvStore `
    -Name OrdersStore `
    -FailFast
```

------------------------------------------------------------------------

# Output

The cmdlet returns one **DkvAdminResult** object per instance stop
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
    Operation   : StopInstance:orders-node-1
    Success     : True
    StatusCode  : 200
    Message     : Instance stopped successfully

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

### Instance Stop API

Each instance is stopped using the manager API:

    POST /admin/v1/stores/{StoreName}/instances/{InstanceName}/stop

If `-Force` is specified:

    POST /admin/v1/stores/{StoreName}/instances/{InstanceName}/stop?force=true

------------------------------------------------------------------------

### Example Production Cluster

  Node        Instance        ClustronPort   ClientPort
  ----------- --------------- -------------- ------------
  10.0.0.11   orders-node-1   7001           7101
  10.0.0.12   orders-node-2   7002           7102
  10.0.0.13   orders-node-3   7003           7103

Stopping the store without `-InstanceName` stops all instances.

------------------------------------------------------------------------

# Related Cmdlets

-   Connect-DkvManager
-   New-DkvStore
-   Add-DkvInstance
-   Start-DkvStore
-   Get-DkvStore
-   Watch-DkvStoreMetrics

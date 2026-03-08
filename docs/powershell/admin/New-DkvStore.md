# New-DkvStore

## Synopsis

Creates a new **Clustron Distributed Key‑Value (DKV) store** on one or
more management servers.

## Description

`New-DkvStore` provisions a new distributed store in the Clustron
cluster.\
A store represents a logical key‑value database backed by one or more
**instances** running on cluster nodes.

The cmdlet supports two provisioning modes:

1.  **Single Instance Mode** -- Create a store with a single instance.
2.  **Multi Instance Mode** -- Create a store with multiple instances in
    a single operation.

The command targets one or more manager servers resolved through:

-   `-Servers` parameter
-   The active `Connect-DkvManager` session context
-   Fallback to `localhost` if no context exists

When multiple servers are targeted, the cmdlet can execute:

-   **Sequentially (default)**
-   **In parallel** using `-Parallel`

------------------------------------------------------------------------

# Syntax

### Single Instance

``` powershell
New-DkvStore -Name <string> -InstanceName <string> -ClustronPort <int> -ClientPort <int>
```

### Multi Instance

``` powershell
New-DkvStore -Name <string> -Instances <InstanceDefinition[]>
```

Optional execution parameters inherited from `DkvCmdletBase`:

``` powershell
[-Servers <string[]>] [-Port <int>] [-TimeoutSec <int>] [-Parallel] [-FailFast]
```

------------------------------------------------------------------------

# Parameters

## -Name

Name of the distributed store.

Example:

    OrdersStore

Required: **Yes**

------------------------------------------------------------------------

## -InstanceName

Name of the instance when creating a **single‑instance store**.

Example:

    orders-node-1

Required: **Yes** (SingleInstance parameter set)

------------------------------------------------------------------------

## -ClustronPort

Internal **cluster communication port** used by the instance.

Example:

    7001

Required: **Yes** (SingleInstance parameter set)

------------------------------------------------------------------------

## -ClientPort

Port exposed for **client applications** to connect to the store.

Example:

    7101

Required: **Yes** (SingleInstance parameter set)

------------------------------------------------------------------------

## -Instances

Defines multiple store instances when creating a **multi‑instance
store**.

Each instance contains:

  Property       Description
  -------------- ----------------------------
  InstanceName   Instance identifier
  ClustronPort   Cluster communication port
  ClientPort     Client access port

Required: **Yes** (MultiInstance parameter set)

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

Maximum time allowed for the administrative request.

Default:

    30 seconds

------------------------------------------------------------------------

## -Parallel

Executes the operation against multiple servers **concurrently**.

Useful for large clusters.

------------------------------------------------------------------------

## -FailFast

Stops execution immediately if a failure occurs on any server.

------------------------------------------------------------------------

# Examples

## Example 1 --- Create a single‑instance store

``` powershell
New-DkvStore `
    -Name OrdersStore `
    -InstanceName orders-node-1 `
    -ClustronPort 7001 `
    -ClientPort 7101
```

------------------------------------------------------------------------

## Example 2 --- Create a store on specific manager servers

``` powershell
New-DkvStore `
    -Name OrdersStore `
    -InstanceName orders-node-1 `
    -ClustronPort 7001 `
    -ClientPort 7101 `
    -Servers 10.0.0.11,10.0.0.12
```

------------------------------------------------------------------------

## Example 3 --- Create store using a connected manager session

``` powershell
Connect-DkvManager -Servers 10.0.0.11,10.0.0.12

New-DkvStore `
    -Name OrdersStore `
    -InstanceName orders-node-1 `
    -ClustronPort 7001 `
    -ClientPort 7101
```

------------------------------------------------------------------------

## Example 4 --- Create a multi‑instance store

``` powershell
$instances = @(
    [InstanceDefinition]@{
        InstanceName = "orders-node-1"
        ClustronPort = 7001
        ClientPort   = 7101
    },
    [InstanceDefinition]@{
        InstanceName = "orders-node-2"
        ClustronPort = 7002
        ClientPort   = 7102
    },
    [InstanceDefinition]@{
        InstanceName = "orders-node-3"
        ClustronPort = 7003
        ClientPort   = 7103
    }
)

New-DkvStore -Name OrdersStore -Instances $instances
```

------------------------------------------------------------------------

## Example 5 --- Execute store creation in parallel across servers

``` powershell
New-DkvStore `
    -Name OrdersStore `
    -InstanceName orders-node-1 `
    -ClustronPort 7001 `
    -ClientPort 7101 `
    -Servers 10.0.0.11,10.0.0.12,10.0.0.13 `
    -Parallel
```

------------------------------------------------------------------------

## Example 6 --- Stop execution on first failure

``` powershell
New-DkvStore `
    -Name OrdersStore `
    -InstanceName orders-node-1 `
    -ClustronPort 7001 `
    -ClientPort 7101 `
    -FailFast
```

------------------------------------------------------------------------

# Output

The cmdlet returns a **DkvAdminResult** object describing the result of
the administrative operation.

Typical properties include:

  Property     Description
  ------------ --------------------------
  Server       Target manager server
  Operation    Operation name
  Success      Indicates success
  StatusCode   HTTP status code
  Message      Operation result message

Example output:

    Server      : http://10.0.0.11:7800
    Operation   : CreateStore
    Success     : True
    StatusCode  : 200
    Message     : Store created successfully

------------------------------------------------------------------------

# Notes

### Instance Host Resolution

When instances are created, the instance host is automatically set to
the **target server host**.

Example:

    Server: 10.0.0.11
    Instance Host: 10.0.0.11

------------------------------------------------------------------------

### Metrics Push Target

Each store instance automatically configures a **metrics push target**
using the manager server URL.

Example:

    PushTargetUrl = http://10.0.0.11:7800

This allows runtime metrics to be streamed to the management layer.

------------------------------------------------------------------------

### Recommended Production Layout

For production environments, a store typically runs with **multiple
instances** across cluster nodes.

Example cluster:

  Node        Instance        ClustronPort   ClientPort
  ----------- --------------- -------------- ------------
  10.0.0.11   orders-node-1   7001           7101
  10.0.0.12   orders-node-2   7002           7102
  10.0.0.13   orders-node-3   7003           7103

------------------------------------------------------------------------

# Related Cmdlets

-   Connect-DkvManager
-   Add-DkvInstance
-   Start-DkvStore
-   Stop-DkvStore
-   Get-DkvStore
-   Watch-DkvStoreMetrics

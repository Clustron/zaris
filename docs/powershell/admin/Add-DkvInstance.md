# Add-DkvInstance

## Synopsis

Adds one or more **instances (nodes)** to an existing Clustron
Distributed Key‑Value (DKV) store.

## Description

`Add-DkvInstance` expands a running or configured store by adding
additional instances to it.

Each instance represents a **store node** running on a cluster machine.
Adding instances allows the store to scale horizontally and distribute
workload across multiple servers.

The cmdlet supports two modes:

1.  **Single Instance Mode** -- Add one instance.
2.  **Multi Instance Mode** -- Add multiple instances in a single
    command.

The operation is executed against one or more **manager servers**
resolved through:

-   `-Servers` parameter
-   Active `Connect-DkvManager` session context
-   Fallback to `localhost`

For each target server the cmdlet sends the following API request:

    POST /admin/v1/stores/{StoreName}/instances

------------------------------------------------------------------------

# Syntax

### Add a single instance

``` powershell
Add-DkvInstance -StoreName <string> -InstanceName <string> -ClustronPort <int> -ClientPort <int>
```

### Add multiple instances

``` powershell
Add-DkvInstance -StoreName <string> -Nodes <InstanceDefinition[]>
```

Optional execution parameters inherited from `DkvCmdletBase`:

``` powershell
[-Servers <string[]>] [-Port <int>] [-TimeoutSec <int>] [-Parallel] [-FailFast]
```

------------------------------------------------------------------------

# Parameters

## -StoreName

Name of the store to which the instance(s) should be added.

Example:

    OrdersStore

Required: **Yes**

------------------------------------------------------------------------

## -InstanceName

Name of the instance being added when using **Single Instance mode**.

Example:

    orders-node-2

Required: **Yes** (Single parameter set)

------------------------------------------------------------------------

## -ClustronPort

Internal **cluster communication port** used by the instance.

Example:

    7002

Required: **Yes** (Single parameter set)

------------------------------------------------------------------------

## -ClientPort

Port exposed for **client applications** to connect to the instance.

Example:

    7102

Required: **Yes** (Single parameter set)

------------------------------------------------------------------------

## -Nodes

Defines multiple instance definitions when using **Multi Instance
mode**.

Each node definition includes:

  Property       Description
  -------------- -----------------------
  InstanceName   Instance identifier
  ClustronPort   Internal cluster port
  ClientPort     Client access port

Required: **Yes** (Multi parameter set)

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

Maximum allowed time for the operation.

Default:

    30 seconds

------------------------------------------------------------------------

## -Parallel

Executes the operation across manager servers concurrently.

------------------------------------------------------------------------

## -FailFast

Stops execution immediately when a failure occurs.

------------------------------------------------------------------------

# Examples

## Example 1 --- Add a single instance

``` powershell
Add-DkvInstance `
    -StoreName OrdersStore `
    -InstanceName orders-node-2 `
    -ClustronPort 7002 `
    -ClientPort 7102
```

------------------------------------------------------------------------

## Example 2 --- Add instance using explicit manager servers

``` powershell
Add-DkvInstance `
    -StoreName OrdersStore `
    -InstanceName orders-node-2 `
    -ClustronPort 7002 `
    -ClientPort 7102 `
    -Servers 10.0.0.11,10.0.0.12
```

------------------------------------------------------------------------

## Example 3 --- Add instance using connected manager session

``` powershell
Connect-DkvManager -Servers 10.0.0.11,10.0.0.12

Add-DkvInstance `
    -StoreName OrdersStore `
    -InstanceName orders-node-2 `
    -ClustronPort 7002 `
    -ClientPort 7102
```

------------------------------------------------------------------------

## Example 4 --- Add multiple instances

``` powershell
$nodes = @(
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

Add-DkvInstance -StoreName OrdersStore -Nodes $nodes
```

------------------------------------------------------------------------

## Example 5 --- Add instances across multiple manager servers

``` powershell
Add-DkvInstance `
    -StoreName OrdersStore `
    -InstanceName orders-node-2 `
    -ClustronPort 7002 `
    -ClientPort 7102 `
    -Servers 10.0.0.11,10.0.0.12,10.0.0.13
```

------------------------------------------------------------------------

## Example 6 --- Stop execution on first failure

``` powershell
Add-DkvInstance `
    -StoreName OrdersStore `
    -InstanceName orders-node-2 `
    -ClustronPort 7002 `
    -ClientPort 7102 `
    -FailFast
```

------------------------------------------------------------------------

# Output

The cmdlet returns a **DkvAdminResult** object describing the outcome of
the operation.

Typical properties include:

  Property     Description
  ------------ -----------------------
  Server       Target manager server
  Operation    Operation name
  Success      Indicates success
  StatusCode   HTTP response code
  Message      Result message

Example output:

    Server      : http://10.0.0.11:7800
    Operation   : AddInstance
    Success     : True
    StatusCode  : 200
    Message     : Instance added successfully

------------------------------------------------------------------------

# Notes

### Scaling a Store

Adding instances allows the store to scale across additional nodes.

Example cluster:

  Node        Instance        ClustronPort   ClientPort
  ----------- --------------- -------------- ------------
  10.0.0.11   orders-node-1   7001           7101
  10.0.0.12   orders-node-2   7002           7102
  10.0.0.13   orders-node-3   7003           7103

------------------------------------------------------------------------

### Manager Context Requirement

If neither `-Servers` nor a connected manager session exists, the cmdlet
uses the fallback server:

    http://localhost:7800

------------------------------------------------------------------------

# Related Cmdlets

-   Connect-DkvManager
-   New-DkvStore
-   Start-DkvStore
-   Stop-DkvStore
-   Get-DkvStore
-   Watch-DkvStoreMetrics

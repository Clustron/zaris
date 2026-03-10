# Connect-DkvManager

## Synopsis

Connects the current PowerShell session to one or more **Clustron DKV
management servers**.

## Description

`Connect-DkvManager` establishes a management context used by all other
Clustron administrative cmdlets.

Once connected, the selected manager servers become the **default
target** for subsequent administrative operations such as:

-   Creating stores
-   Starting and stopping stores
-   Adding instances
-   Monitoring metrics

The connection information is stored in the **PowerShell session
context**, meaning you only need to connect once per session.

If a connection already exists, the cmdlet will fail unless `-Force` is
specified.

------------------------------------------------------------------------

# Syntax

``` powershell
Connect-DkvManager -Servers <string[]> [-Port <int>] [-Force]
```

------------------------------------------------------------------------

# Parameters

## -Servers

One or more Clustron management servers to connect to.

Servers may be specified in multiple formats:

  Format        Example
  ------------- -----------------------
  Hostname      node1
  Host + port   node1:7800
  IP address    10.0.0.11
  Full URI      http://10.0.0.11:7800

Multiple servers can be specified to allow **manager failover**.

Example:

``` powershell
-Servers 10.0.0.11,10.0.0.12,10.0.0.13
```

Required: **Yes**

------------------------------------------------------------------------

## -Port

Default management port used when the port is not explicitly specified
in `-Servers`.

Default value:

    7800

Example:

``` powershell
-Port 7800
```

Required: **No**

------------------------------------------------------------------------

## -Force

Forces replacement of an existing manager connection.

If a connection already exists, the cmdlet normally fails to prevent
accidental context replacement.

Using `-Force` clears the current context and replaces it with the new
manager list.

Required: **No**

------------------------------------------------------------------------

# Examples

## Example 1 --- Connect to a single manager node

``` powershell
Connect-DkvManager -Servers 10.0.0.11
```

Output:

    Servers         : http://10.0.0.11:7800
    ConnectedAtUtc  : 2026-03-08T12:30:10Z
    Success         : True

------------------------------------------------------------------------

## Example 2 --- Connect to multiple manager nodes (recommended)

``` powershell
Connect-DkvManager -Servers 10.0.0.11,10.0.0.12,10.0.0.13
```

This configuration allows automatic failover if one manager node becomes
unavailable.

------------------------------------------------------------------------

## Example 3 --- Specify servers with explicit ports

``` powershell
Connect-DkvManager -Servers 10.0.0.11:7800,10.0.0.12:7800
```

------------------------------------------------------------------------

## Example 4 --- Use full HTTP URIs

``` powershell
Connect-DkvManager -Servers http://10.0.0.11:7800,http://10.0.0.12:7800
```

------------------------------------------------------------------------

## Example 5 --- Override the default port

``` powershell
Connect-DkvManager -Servers 10.0.0.11,10.0.0.12 -Port 7800
```

When a port is not specified per server, the value provided via `-Port`
is used.

------------------------------------------------------------------------

## Example 6 --- Replace an existing connection

``` powershell
Connect-DkvManager -Servers 10.0.0.11,10.0.0.12 -Force
```

This clears the existing manager context and replaces it.

------------------------------------------------------------------------

## Example 7 --- Connect using hostnames

``` powershell
Connect-DkvManager -Servers node1,node2,node3
```

Hostnames must resolve to reachable cluster nodes.

------------------------------------------------------------------------

# Output

The cmdlet returns an object containing the connection information.

  -----------------------------------------------------------------------
  Property                            Description
  ----------------------------------- -----------------------------------
  Servers                             Normalized server URIs used for the
                                      manager connection

  ConnectedAtUtc                      UTC timestamp when the connection
                                      was established

  Success                             Indicates whether the connection
                                      was successfully established
  -----------------------------------------------------------------------

Example output:

    Servers         : http://10.0.0.11:7800
    ConnectedAtUtc  : 2026-03-08T12:30:10Z
    Success         : True

------------------------------------------------------------------------

# Notes

### Manager Context

Once connected, the manager servers are stored in the **session manager
context**.\
Subsequent administrative cmdlets automatically use this context.

Example:

``` powershell
Connect-DkvManager -Servers 10.0.0.11,10.0.0.12

New-DkvStore -Name OrdersStore
```

The store creation command will automatically target the connected
manager servers.

------------------------------------------------------------------------

### Invalid Addresses

The following addresses are **not allowed** as connection targets:

    0.0.0.0
    ::
    ::0

These represent unspecified bind addresses and cannot be used to
initiate connections.

------------------------------------------------------------------------

### Recommended Production Configuration

For production environments it is recommended to connect to **multiple
manager nodes**.

Example:

``` powershell
Connect-DkvManager -Servers 10.0.0.11,10.0.0.12,10.0.0.13
```

This ensures the PowerShell session remains functional even if a manager
node becomes unavailable.

------------------------------------------------------------------------

# Related Cmdlets

-   Add-DkvInstance
-   New-DkvStore
-   Start-DkvStore
-   Stop-DkvStore
-   Get-DkvStore
-   Watch-DkvStoreMetrics

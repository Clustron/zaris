# Watch-DkvPrefix

## Synopsis

Streams **live change events for all keys with a specific prefix** in a
Clustron Distributed Key-Value (DKV) store.

------------------------------------------------------------------------

# Syntax

``` powershell
Watch-DkvPrefix [-Prefix] <string> [-IncludeSnapshot]
```

------------------------------------------------------------------------

# Description

`Watch-DkvPrefix` subscribes to change events for all keys that share a
common prefix.

Whenever a key matching the prefix changes, the command emits an event
describing the change.

This allows administrators and developers to observe activity in a
namespace of keys in real time.

Typical use cases include:

-   monitoring application sessions
-   debugging distributed coordination
-   observing cache updates
-   building reactive automation pipelines
-   tracking leader election or locks

The command runs continuously until **Ctrl+C** is pressed.

------------------------------------------------------------------------

# Parameters

## -Prefix

Specifies the key prefix to watch.

All keys that begin with this prefix will generate events.

``` powershell
Type: String
Mandatory: True
Position: 0
```

Example:

``` powershell
Watch-DkvPrefix -Prefix session:
```

------------------------------------------------------------------------

## -IncludeSnapshot

When specified, the command first emits the **current state of all keys
matching the prefix** before streaming live updates.

``` powershell
Type: SwitchParameter
Mandatory: False
```

Example:

``` powershell
Watch-DkvPrefix -Prefix config: -IncludeSnapshot
```

------------------------------------------------------------------------

# Output

The command emits objects representing change events.

  Property    Description
  ----------- -------------------------------------------
  Event       Type of event (Put, Update, Delete, etc.)
  Key         Key that triggered the event
  Revision    Revision number of the change
  Value       Value associated with the change
  Timestamp   Local timestamp of the event

Example output:

    Event     : Put
    Key       : session:user123
    Revision  : 41
    Value     : { ... }
    Timestamp : 14:32:11.456

------------------------------------------------------------------------

# Examples

## Watch all session keys

``` powershell
Watch-DkvPrefix -Prefix session:
```

------------------------------------------------------------------------

## Watch configuration changes with snapshot

``` powershell
Watch-DkvPrefix -Prefix config: -IncludeSnapshot
```

------------------------------------------------------------------------

# Notes

-   The command streams events continuously until stopped.
-   Press **Ctrl+C** to cancel the watch.
-   Values are automatically deserialized when possible.
-   Binary values are returned as byte arrays if deserialization fails.

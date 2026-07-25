# Watch-ZrKey

## Synopsis

Streams **live change events** for a specific key in a Clustron
Distributed Key‑Value (Zaris) store.

------------------------------------------------------------------------

# Syntax

``` powershell
Watch-ZrKey [-Key] <string> [-IncludeSnapshot]
```

------------------------------------------------------------------------

# Description

`Watch-ZrKey` subscribes to change events for a single key and
continuously outputs events as they occur.

The command keeps running and prints events such as:

-   Put / Update
-   Delete
-   Lease expiration
-   Snapshot (optional)

This command is useful for:

-   debugging distributed state
-   observing coordination primitives
-   building reactive automation scripts
-   monitoring leader election / locks

The command runs until **Ctrl+C** is pressed.

------------------------------------------------------------------------

# Parameters

## -Key

Specifies the key to watch.

``` powershell
Type: String
Mandatory: True
Position: 0
```

Example:

``` powershell
Watch-ZrKey -Key session:user123
```

------------------------------------------------------------------------

## -IncludeSnapshot

When specified, the command first emits the current value of the key
before streaming live events.

``` powershell
Type: SwitchParameter
Mandatory: False
```

Example:

``` powershell
Watch-ZrKey -Key config:featureX -IncludeSnapshot
```

------------------------------------------------------------------------

# Output

The command emits objects describing each change event.

  Property   Description
  ---------- ------------------------
  Event      Event type
  Key        Key being watched
  Revision   Revision of the change
  Value      Value after change

Example output:

    Event    : Put
    Key      : session:user123
    Revision : 14
    Value    : { ... }

------------------------------------------------------------------------

# Examples

## Watch a key

``` powershell
Watch-ZrKey -Key session:user123
```

------------------------------------------------------------------------

## Watch a key including the current value

``` powershell
Watch-ZrKey -Key config:featureX -IncludeSnapshot
```

------------------------------------------------------------------------

# Notes

-   The command streams events until cancelled.
-   Press **Ctrl+C** to stop watching.
-   Values are automatically deserialized when possible.
-   Binary values are returned as byte arrays if deserialization fails.

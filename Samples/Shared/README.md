# Clustron DKV -- Samples Shared Project

This project contains shared infrastructure used by all Clustron DKV
samples.

It is **not a standalone sample**.

Instead, it provides reusable components that keep all sample projects:

-   Clean
-   Consistent
-   Isolated
-   Easy to understand

------------------------------------------------------------------------

## What This Project Contains

The Shared project provides:

-   `ConsoleHelper` → Structured console output helpers
-   `DkvOptions` → Configuration binding model
-   `SampleContext` → Automatic key prefix isolation
-   `SampleCleanup` → Automatic cleanup of created keys
-   Shared models (e.g., `Customer`)

------------------------------------------------------------------------

## Why This Project Exists

Without this shared layer:

-   Each sample would duplicate configuration logic
-   Key naming could conflict between samples
-   Cleanup logic would need to be rewritten
-   Console output formatting would be inconsistent

This project ensures:

-   Every sample runs independently
-   Keys are isolated per session
-   All created data is cleaned up safely

------------------------------------------------------------------------

## Key Components Explained

### ConsoleHelper

Provides structured console sections:

-   Header
-   Section
-   Info
-   Success
-   Error

This makes sample output readable and consistent.

------------------------------------------------------------------------

### DkvOptions

Binds configuration from `appsettings.json`:

``` json
{
  "Dkv": {
    "ClusterId": "sample-cluster",
    "Mode": "InProc",
    "RemoteHost": "127.0.0.1",
    "RemotePort": 4100,
    "LogFilePath": "logs/dkv.log"
  }
}
```

It also resolves client mode (InProc or Remote).

------------------------------------------------------------------------

### SampleContext

Automatically generates a unique session prefix.

Example:

`basic-20240220-12345-`

All keys created by a sample are prefixed to avoid collisions across
runs.

------------------------------------------------------------------------

### SampleCleanup

Tracks all keys created during execution and removes them at the end of
the sample.

This ensures:

-   No leftover test data
-   Clean repeated executions
-   Predictable sample behavior

------------------------------------------------------------------------

## Project Structure Role

The architecture looks like this:

Samples → Shared → DKV Client → DKV Server

The Shared project sits between samples and the client SDK, providing:

-   Configuration binding
-   Logging helpers
-   Isolation utilities
-   Cleanup safety

------------------------------------------------------------------------

## Important Notes

-   This project should not be executed directly.
-   It has no `Main()` entry point.
-   It exists purely to support other samples.

------------------------------------------------------------------------

## Summary

  Purpose                Provided
  ---------------------- ----------
  Shared Configuration   Yes
  Console Formatting     Yes
  Key Isolation          Yes
  Cleanup Automation     Yes
  Standalone Execution   No

------------------------------------------------------------------------

This project ensures that all Clustron DKV samples remain clean,
isolated, and production-style in structure.

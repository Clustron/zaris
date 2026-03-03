# 🚀 Clustron DKV --- Samples Shared Project

This project contains shared infrastructure used by **all Clustron DKV
samples**.

⚠️ This is **not a standalone sample** and does not contain its own
`appsettings.json`.

Instead, it provides reusable components that keep every sample:

-   Clean
-   Consistent
-   Isolated
-   Easy to understand
-   Production-structured

------------------------------------------------------------------------

# 📌 Purpose of This Project

Without this shared layer:

-   Each sample would duplicate configuration logic
-   Key naming could conflict between samples
-   Cleanup logic would need to be rewritten repeatedly
-   Console output formatting would be inconsistent

This project ensures:

-   Every sample runs independently
-   Keys are isolated per session
-   All created data is cleaned up safely
-   Console output remains structured and readable

------------------------------------------------------------------------

# 🧩 What This Project Contains

The Shared project provides:

-   `ConsoleHelper` → Structured console output helpers
-   `DkvOptions` → Configuration binding model (used by all samples)
-   `SampleContext` → Automatic key prefix isolation
-   Shared models (e.g., `Customer`, `JobItem`, etc.)

It does **not** contain:

-   A `Main()` method
-   An `appsettings.json`
-   Executable logic

------------------------------------------------------------------------

# 🧠 Key Components Explained

## 🔹 ConsoleHelper

Provides structured console sections:

-   Header
-   Section
-   Info
-   Success
-   Error

This ensures all samples produce consistent and readable output.

------------------------------------------------------------------------

## 🔹 DkvOptions

Binds configuration from each sample's `appsettings.json`.

All samples follow the unified configuration schema:

``` json
{
  "Dkv": {
    "ClusterId": "string",
    "Mode": "InProc | Remote",
    "Seeds": [
      {
        "Host": "string",
        "Port": number
      }
    ],
    "LogFilePath": "string | null"
  }
}
```

The Shared project only defines the model --- actual configuration lives
inside each sample project.

------------------------------------------------------------------------

## 🔹 SampleContext

Automatically generates a unique session prefix.

Example:

`basic-20240303-123456-`

All keys created by a sample are prefixed to avoid collisions across:

-   Multiple runs
-   Different samples
-   Parallel executions

------------------------------------------------------------------------

# 🏗 Architectural Role

The architecture flow looks like this:

    Samples → Shared → DKV Client → DKV Server

The Shared project sits between samples and the client SDK, providing:

-   Configuration binding
-   Logging helpers
-   Key isolation utilities
-   Shared data models

------------------------------------------------------------------------

# 📦 Summary

  Purpose                      Provided
  ---------------------------- ----------
  Shared Configuration Model   Yes
  Console Formatting           Yes
  Key Isolation                Yes
  Shared Models                Yes
  Standalone Execution         No

------------------------------------------------------------------------

This project ensures that all Clustron DKV samples remain:

-   Clean\
-   Isolated\
-   Consistent\
-   Production-style in structure\
-   Easy to maintain and extend

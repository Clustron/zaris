# Clustron DKV -- Samples

This folder contains official usage samples for the Clustron DKV .NET
Client.

Each sample demonstrates a specific feature of DKV and follows a
consistent structure:

-   Supports both InProc and Remote modes
-   Uses appsettings.json for configuration
-   Automatically isolates keys per session
-   Cleans up created data after execution

------------------------------------------------------------------------

## Available Samples

### 1. Basic Sample

Demonstrates: - PUT / GET - Metadata (TTL, labels, content type) -
Counters - TTL expiration - Cleanup

Project: Clustron.Dkv.Sample.Basic

------------------------------------------------------------------------

### 2. Counters Sample

Demonstrates: - Atomic increment - Get counter value - Set counter
value - Min / Max bounds - TTL on counters

Project: Clustron.Dkv.Sample.Counters

------------------------------------------------------------------------

### 3. Lease Sample

Demonstrates: - Grant lease - Attach keys to lease - Automatic expiry -
KeepAlive - Revoke

Project: Clustron.Dkv.Sample.Lease

------------------------------------------------------------------------

### 4. TTL Sample (Placeholder)

Reserved for demonstrating advanced TTL scenarios. Not implemented yet.

Project: Clustron.Dkv.Sample.Ttl

------------------------------------------------------------------------

### 5. Watch Sample (Placeholder)

Reserved for demonstrating watch / streaming APIs. Not implemented yet.

Project: Clustron.Dkv.Sample.Watch

------------------------------------------------------------------------

## Shared Infrastructure

All samples depend on:

Clustron.Dkv.Samples.Shared

This project provides:

-   Console formatting helpers
-   Configuration binding
-   Automatic key prefix isolation
-   Cleanup tracking utilities
-   Shared models

------------------------------------------------------------------------

## Running a Sample

1.  Navigate to the desired sample project.
2.  Edit appsettings.json.
3.  Run:

dotnet run

------------------------------------------------------------------------

## Configuration Modes

### InProc (Default)

Runs an embedded in-memory store. No server setup required.

Example:

{ "Dkv": { "ClusterId": "sample-cluster", "Mode": "InProc",
"LogFilePath": "logs/dkv.log" } }

------------------------------------------------------------------------

### Remote Mode

Connects to a running DKV cluster.

{ "Dkv": { "ClusterId": "my-store", "Mode": "Remote", "RemoteHost":
"127.0.0.1", "RemotePort": 4100, "LogFilePath": "logs/dkv.log" } }

-   ClusterId → Store ID you created
-   RemoteHost → IP of DKV server (seed node)
-   RemotePort → ClientPort selected during store creation

------------------------------------------------------------------------

## Recommended Learning Order

1.  Basic
2.  Counters
3.  Lease
4.  TTL (when implemented)
5.  Watch (when implemented)

------------------------------------------------------------------------

These samples establish the baseline developer experience for Clustron
DKV usage and demonstrate production-style client interaction patterns.

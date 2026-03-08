# Clustron DKV

**Clustron DKV is a high-performance distributed key-value store for
.NET designed for modern microservices and cloud-native applications.**

It provides a scalable data platform that combines **distributed
storage, distributed coordination primitives, and multi-key
transactions** in a single system.

Clustron is designed for developers building **high-throughput .NET
services** that require:

-   distributed data storage
-   low-latency operations
-   horizontal scalability
-   strong consistency
-   operational simplicity

Clustron DKV can serve as a **distributed key-value store for .NET
applications and as an alternative to Redis for many workloads**,
especially when **transactions and distributed primitives** are
required.

Unlike many distributed data stores that focus purely on caching,
Clustron provides a **distributed data foundation** capable of
supporting application state, coordination, and real-time data systems.

------------------------------------------------------------------------

# What is Clustron DKV?

Clustron DKV is a **distributed key-value database built specifically
for the .NET ecosystem**.

It enables applications to store and manage data across multiple nodes
while providing built-in support for **distributed coordination
primitives and transactional updates**.

Clustron combines several capabilities typically spread across multiple
infrastructure systems:

-   distributed key-value storage
-   distributed locking
-   distributed counters
-   distributed watches
-   transactional state updates

This allows developers to build **reliable distributed systems without
introducing additional infrastructure components**.

------------------------------------------------------------------------

# Install Clustron Server

To run a local cluster, follow the **server installation guide**:

📖 Server Setup Guide\
https://github.com/zeroheartbeat/clustron-dkv/blob/main/docs/getting-started.md

The guide walks you through:

-   Installing the Clustron server
-   Starting cluster nodes
-   Creating a store
-   Connecting client applications

------------------------------------------------------------------------

# Try It Now

### Install .NET Client

Install the Clustron client from NuGet:

``` bash
dotnet add package Clustron.DKV.Client
```

Using Clustron from a .NET application is simple.

### Connect to a cluster

``` csharp
using Clustron.DKV.Client;

var client = await DKVClient.InitializeRemote(
    clusterId: "teststore",
    remoteHost: "127.0.0.1",
    remotePort: 7070);
```

### Store and retrieve data

``` csharp
await client.PutAsync("user:1", "John");

var value = await client.GetAsync<string>("user:1");

Console.WriteLine(value);
```

------------------------------------------------------------------------

### Atomic counters

``` csharp
await client.IncrementAsync("global:counter");
```

------------------------------------------------------------------------

### Distributed locking

``` csharp
using var lease = await client.AcquireLeaseAsync(
    "order-lock",
    TimeSpan.FromSeconds(10));

if (lease != null)
{
    Console.WriteLine("Lock acquired");
}
```

------------------------------------------------------------------------

### Distributed transactions

Clustron supports **multi-key transactional updates across the
cluster**.

``` csharp
await using var tx = await client.BeginTransactionAsync();

await tx.PutAsync("account:A", 900);
await tx.PutAsync("account:B", 1100);

await tx.CommitAsync();
```

------------------------------------------------------------------------

### Watch for changes

``` csharp
await client.WatchPrefixAsync("config:", change =>
{
    Console.WriteLine($"Key changed: {change.Key}");
});
```

------------------------------------------------------------------------

# ⭐ Key Feature: Distributed Transactions

One of the strongest capabilities of Clustron DKV is **transaction
support in a distributed key-value store**.

Many distributed KV systems either:

-   do not support transactions
-   only support single-key atomic operations
-   provide weak transactional guarantees

Clustron provides **client-coordinated distributed transactions**
enabling:

-   multi-key reads and writes
-   atomic commit across nodes
-   conflict detection
-   consistent state updates

This makes Clustron suitable for workloads such as:

-   financial transaction processing
-   distributed state management
-   inventory updates
-   multi-entity consistency workflows

------------------------------------------------------------------------

# Distributed Primitives Built In

Clustron DKV provides a distributed systems foundation with support for:

-   Distributed key-value storage
-   Native clustering
-   Prefix-based watch subscriptions
-   Lease and distributed locking primitives
-   Counters and atomic operations
-   TTL expiration via TimeWheel scheduler
-   Equality and range indexing
-   Query execution engine
-   Horizontal scaling support
-   Multi-instance per host deployment

These primitives allow developers to build distributed systems without
additional infrastructure.

------------------------------------------------------------------------

# Typical Use Cases

Clustron DKV can be used in many distributed system scenarios:

-   Distributed caching for .NET applications
-   Redis alternative for .NET workloads
-   Distributed job queues
-   Global rate limiting
-   Distributed leader election
-   Configuration distribution
-   Transactional application state storage
-   Service coordination between microservices

Because Clustron supports **multi-key transactions**, it can also
support **operational data workloads**, not just caching.

------------------------------------------------------------------------

# 📂 Samples

The repository contains working examples demonstrating real distributed
system patterns.

Explore them under:

/samples

Examples include:

-   Distributed leader election
-   Distributed job queue
-   Distributed rate limiter
-   Transactional money transfer
-   Cluster coordination examples

------------------------------------------------------------------------

# 📊 Performance (Early Benchmarks)

Initial internal benchmarking shows:

  Scenario                       Throughput
  ------------------------------ ----------------
  Single client instance         \~100K ops/sec
  Multiple client applications   \~300K ops/sec

Clustron is designed with:

-   efficient async networking
-   GC-aware memory design
-   low CPU overhead under heavy load

Detailed benchmark methodology will be published under `/benchmarks`.

------------------------------------------------------------------------

# 🏗 Architecture Philosophy

Clustron DKV is designed around:

-   Clear separation of Control Plane and Data Plane
-   Instance-level isolation
-   Efficient async networking pipelines
-   GC-aware memory management
-   Observability-first architecture
-   Extensible internal engine

Full architecture documentation is available under:

/docs

------------------------------------------------------------------------

# 🛣 Roadmap

### Phase 1

-   Core KV engine stabilization
-   Clustering refinement
-   Watch & TTL improvements
-   Transaction engine refinement

### Phase 2

-   Public preview release
-   Advanced indexing enhancements
-   Management tooling
-   Observability improvements

### Phase 3

-   Enterprise feature expansion
-   Multi-region replication
-   Vector search
-   Semantic caching

------------------------------------------------------------------------

# 📂 Repository Structure

/docs\
/benchmarks\
/roadmap\
/samples

------------------------------------------------------------------------

# 📣 Early Access & Partnerships

Clustron DKV is currently in active development.

If you are interested in:

-   Early access
-   Design partnerships
-   Performance testing
-   Commercial collaboration

Contact: support@clustron.io

------------------------------------------------------------------------

# 🔒 Licensing

The Clustron DKV core engine will be released under the **Business
Source License (BSL)**.

A public open-source release of the core engine is planned for **2026**.

See the `LICENSE` file for details.

------------------------------------------------------------------------

# 🌍 Vision

Clustron DKV aims to become a **modern distributed data foundation for
.NET applications**, combining:

-   performance
-   distributed primitives
-   strong transactional guarantees
-   operational clarity

into a single platform.

# 🚀 Clustron DKV --- Search Sample

This sample demonstrates how to use **Clustron DKV Search APIs** to
query data using entities, labels, filtering, sorting, and projection.

It showcases structured search capabilities across distributed data.

------------------------------------------------------------------------

# 📌 What This Sample Demonstrates

This sample performs the following operations:

-   Connect to a DKV cluster (InProc or Remote)
-   Store entities with labels
-   Execute equality queries
-   Execute range queries
-   Perform AND conditions
-   Perform prefix searches
-   Apply sorting and limits
-   Use field projection
-   Clean up created keys

------------------------------------------------------------------------

# ⚙️ Configuration

All samples use a unified configuration structure via
`appsettings.json`.

## 🧠 Configuration Schema

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

------------------------------------------------------------------------

## 🔹 ClusterId

The **Store ID** created in DKV.

This must match the store name defined when creating your cluster store.

Example:

``` json
"ClusterId": "teststore"
```

------------------------------------------------------------------------

## 🔹 Mode

  Value    Description
  -------- -----------------------------------------------
  InProc   Embedded in-memory store (no server required)
  Remote   Connects to external DKV server nodes

------------------------------------------------------------------------

## 🔹 Seeds

A list of one or more DKV server nodes.

Only one seed is required. After connecting, the client automatically:

-   Discovers cluster topology\
-   Connects to all nodes\
-   Handles failover\
-   Manages topology updates

Example:

``` json
"Seeds": [
  { "Host": "127.0.0.1", "Port": 7070 }
]
```

------------------------------------------------------------------------

## 🔹 LogFilePath (Optional)

Specifies where client logs are written.

``` json
"LogFilePath": "logs/dkv.log"
```

Use `null` to disable file logging.

------------------------------------------------------------------------

# 🏃 Running the Sample

``` bash
dotnet run
```

------------------------------------------------------------------------

# 🧪 Running in InProc Mode (No Server Required)

``` json
{
  "Dkv": {
    "ClusterId": "sample-cluster",
    "Mode": "InProc",
    "Seeds": [],
    "LogFilePath": null
  }
}
```

------------------------------------------------------------------------

# 🌐 Running in Remote Mode (Real Cluster)

``` json
{
  "Dkv": {
    "ClusterId": "teststore",
    "Mode": "Remote",
    "Seeds": [
      { "Host": "127.0.0.1", "Port": 7070 }
    ],
    "LogFilePath": null
  }
}
```

Before running:

-   Ensure DKV servers are running\
-   Ensure the store exists\
-   Ensure the port matches the configured `ClientPort`

------------------------------------------------------------------------

# 🧠 Search Capabilities Demonstrated

##  Equality Search

``` csharp
SearchQuery.For(Entity).Eq("city", "London");
```

------------------------------------------------------------------------

##  Range Query

``` csharp
SearchQuery.For(Entity).Range("age", 28, 32);
```

------------------------------------------------------------------------

## AND Conditions

``` csharp
SearchQuery.For(Entity)
    .And(new EqClause("city", "Berlin"),
         new EqClause("age", "32"));
```

------------------------------------------------------------------------

##  Prefix Search

``` csharp
SearchQuery.For(Entity)
    .LikePrefix("email", "user1");
```

------------------------------------------------------------------------

##  Sorting + Limit

``` csharp
SearchQuery.For(Entity)
    .OrderBy("age", ascending: false)
    .Limit(5);
```

------------------------------------------------------------------------

##  Projection

``` csharp
SearchQuery.For(Entity)
    .Select("email");
```

------------------------------------------------------------------------

# 📊 Key DKV Features Used

  Feature           Purpose
  ----------------- -----------------------------
  Entities          Logical grouping of records
  Labels            Queryable metadata
  Scan/Search API   Distributed search
  Sorting           Ordered results
  Limit             Result size control
  Projection        Partial field selection
  Prefix cleanup    Safe sample isolation

------------------------------------------------------------------------

# 📦 Summary

This sample demonstrates how Clustron DKV enables:

-   Structured search across distributed data
-   Metadata-based querying
-   Range filtering
-   Compound query logic
-   Efficient result projection
-   Controlled pagination and sorting

It models real-world search patterns such as:

-   Filtering users by attributes
-   Building dashboards
-   Reporting queries
-   Query-driven workflows

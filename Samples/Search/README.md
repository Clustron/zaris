# 🚀 Clustron Zaris — Search Sample

This sample demonstrates how to use **Clustron Zaris Search APIs** to query data using entities, labels, filtering, sorting, and projection.

It showcases how you can perform **structured queries over distributed data**.

---

# 📌 What This Sample Demonstrates

This sample shows how to:

- Store entities with labels
- Run equality queries
- Run range queries
- Combine conditions (AND)
- Perform prefix searches
- Apply sorting and limits
- Use projection (select fields)
- Clean up created data

---

# 🚀 Quick Start (Recommended)

The fastest way to run this sample is using **InProc mode**.

```json
{
  "Zaris": {
    "Stores": {
      "teststore": {
        "Mode": "InProc"
      }
    }
  }
}
```

Run:

```bash
dotnet run
```

✅ No setup required — runs instantly.

---

# 🧠 How the Sample Works

This sample uses Clustron’s **provider-based model**:

```csharp
var client = await _provider.GetAsync("teststore");
```

- You request a client for a store  
- Configuration is applied automatically  
- Connections are handled internally  

---

# 🌐 Run with Real Cluster (Next Step)

To run against a real distributed cluster, switch to **Remote mode**:

```json
{
  "Zaris": {
    "Stores": {
      "teststore": {
        "Mode": "Remote",
        "Seeds": [
          { "Host": "127.0.0.1", "Port": 7681 }
        ]
      }
    }
  }
}
```

Before running:

- Ensure Zaris servers are running  
- Ensure the store exists  
- Ensure ports match  

👉 Full setup guide:  
https://clustron.io/docs/clustron/zaris/getting-started/overview/

---

# 💡 Learning Path

- Start with **InProc** → explore queries quickly  
- Move to **Remote** → run distributed search  

---

# 🧠 Search Capabilities Demonstrated

## Equality Search

```csharp
SearchQuery.For(Entity).Eq("city", "London");
```

---

## Range Query

```csharp
SearchQuery.For(Entity).Range("age", 28, 32);
```

---

## AND Conditions

```csharp
SearchQuery.For(Entity)
    .And(new EqClause("city", "Berlin"),
         new EqClause("age", "32"));
```

---

## Prefix Search

```csharp
SearchQuery.For(Entity)
    .LikePrefix("email", "user1");
```

---

## Sorting + Limit

```csharp
SearchQuery.For(Entity)
    .OrderBy("age", ascending: false)
    .Limit(5);
```

---

## Projection

```csharp
SearchQuery.For(Entity)
    .Select("email");
```

---

# 📊 Key Features

- Entity-based organization  
- Label-based querying  
- Distributed search execution  
- Sorting and pagination  
- Field projection  

---

# 📦 Summary

This sample demonstrates how Clustron Zaris enables:

- Structured querying across distributed data  
- Metadata-driven filtering  
- Efficient search workflows  

Use these capabilities for:

- User filtering  
- Dashboards  
- Reporting  
- Query-driven applications  

Clustron Zaris provides **powerful search capabilities built directly into your distributed data layer**.

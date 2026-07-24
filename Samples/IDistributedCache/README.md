# 🚀 Clustron Dictus — IDistributedCache Sample

This sample demonstrates how to use **Clustron Dictus as an IDistributedCache provider**.

It shows how you can plug Clustron into existing .NET applications using the familiar
`IDistributedCache` interface.

---

# 📌 What This Sample Demonstrates

This sample shows how to:

- Use `IDistributedCache` with Clustron Dictus
- Store and retrieve string values
- Overwrite existing values
- Remove cached entries
- Use TTL (expiration)
- Validate cache expiry behavior
- Clean up created keys

---

# 🚀 Quick Start (Recommended)

Run instantly using **InProc mode**:

```json
{
  "Dictus": {
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

✅ No setup required — works out of the box.

---

# 🧠 How the Sample Works

This sample integrates Clustron with .NET’s caching abstraction:

```csharp
IDistributedCache cache
```

Internally, Clustron is resolved using the **provider model**:

```csharp
var client = await _provider.GetAsync("teststore");
```

- Clustron handles connection and configuration  
- Your application works with standard `IDistributedCache`  

---

# 🌐 Run with Real Cluster (Next Step)

Switch to **Remote mode** for a real distributed setup:

```json
{
  "Dictus": {
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

- Ensure Dictus servers are running  
- Ensure the store exists  
- Ensure ports match  

👉 Full setup guide:  
https://clustron.io/docs/clustron/dictus/getting-started/overview/

---

# 💡 Learning Path

- Start with **InProc** → understand integration quickly  
- Move to **Remote** → use as real distributed cache  

---

# 🧠 What is IDistributedCache?

`IDistributedCache` is a standard .NET abstraction for distributed caching.

Clustron implements this interface, allowing you to:

- Replace existing cache providers easily  
- Use familiar APIs  
- Plug into ASP.NET Core and other frameworks  

---

# 🔄 Sample Flow

## Set Value

```csharp
await cache.SetStringAsync(key, "John Doe");
```

---

## Get Value

```csharp
var value = await cache.GetStringAsync(key);
```

---

## Overwrite

```csharp
await cache.SetStringAsync(key, "Jane Doe");
```

---

## Remove

```csharp
await cache.RemoveAsync(key);
```

---

## TTL Expiry

- Set value with expiration  
- Wait for expiry  
- Validate removal  

---

# 📊 Key Features

- Standard .NET interface  
- Distributed storage  
- TTL support  
- Seamless integration  
- Minimal code changes  

---

# 📦 Summary

This sample demonstrates how Clustron Dictus enables:

- Drop-in distributed caching  
- Easy migration from other providers  
- Standardized cache APIs  
- Scalable caching infrastructure  

Use this for:

- ASP.NET Core caching  
- Session storage  
- Application-level caching  
- Performance optimization  

Clustron Dictus lets you use **distributed caching with zero friction using familiar .NET APIs**.

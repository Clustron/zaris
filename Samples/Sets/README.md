# Sets

Demonstrates the Zaris native **set** data structure (`ISetClient`) — an unordered
collection of unique string members, the native equivalent of a Redis set.

The sample models the tags on an article and walks through the full surface:

| Operation | API |
|---|---|
| Add members | `sets.AddAsync(key, members)` |
| Duplicate handling | `AddAsync` returns the count of *newly* added members |
| List members | `sets.MembersAsync(key)` |
| Membership test | `sets.ContainsAsync(key, member)` |
| Cardinality | `sets.CountAsync(key)` |
| Random sample | `sets.RandomMembersAsync(key, count)` |
| Remove members | `sets.RemoveAsync(key, members)` |

Set members are plain `string` values (unlike hash/list values, which are `byte[]`).

## Run

```bash
dotnet run
```

By default the sample runs against an in-process store (`zaris://inproc/teststore`
in `appsettings.json`), so no external cluster is required. Point it at a real
cluster by changing the `teststore` connection string to `zaris://host:7861/store`.

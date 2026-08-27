# Hashes

Demonstrates the native Zaris **hash** data structure (`IZaris.Hashes` / `IHashClient`) — a
single key holding many named fields, ideal for records such as a user profile.

Shows: setting fields (single and many), `SetIfMissing`, reading the whole hash with
`GetAll`, reading a single field, `Exists`, `Length`, an atomic numeric field via
`IncrementBy`, and deleting a field.

Field values are raw `byte[]`, so strings are UTF-8 encoded on write and decoded on read.

Run:

```bash
dotnet run --project Hashes
```

By default it connects to an in-process store (`zaris://inproc/teststore`), so no cluster
is required.

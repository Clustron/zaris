# Distributed Locks

Demonstrates the Zaris distributed lock primitive (`ILocksClient`, reached via
`((IZaris)client).Locks`): a cluster-wide mutex keyed by a string, with a TTL so a
crashed holder can't wedge the lock forever.

This is distinct from the **Lease** sample (TTL-bound key ownership) and the
**LeaderElection** recipe — it is the dedicated "only one holder at a time" API.

## What it shows

- **Acquire** — `AcquireAsync(key, ttl)` returns an `IAsyncDisposableLock`, or `null`
  when the lock is already held.
- **Contention** — a second acquire on the same key is refused while the lock is held.
- **Renew** — `RenewAsync(ttl)` extends the lease while you still hold it.
- **Release + re-acquire** — after `ReleaseAsync()` (or disposal), the key is free again.
- **Mutual exclusion under concurrency** — three workers race for the same lock; a shared
  counter proves at most one is ever inside the critical section.

## Run

```bash
dotnet run --project Locks
```

By default it connects to an in-process store (`zaris://inproc/teststore` from
`appsettings.json`), so no cluster is required. Point `ConnectionStrings:teststore` at a
real cluster (`zaris://host:7861/store`) to exercise the lock across nodes.

The lock and lease primitives are correctness-meaningful even in-process; their
cross-node contention story is what a real cluster adds.

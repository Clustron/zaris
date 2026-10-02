# Zaris Solutions — real-world problems solved with Clustron Zaris

Six complete, runnable solutions (not toy samples) that each solve a genuinely hard distributed-systems problem on **Clustron Zaris 2.0.1**. Every solution is self-contained (runs on embedded **InProc** Zaris — no external cluster or ports), test-first (TDD), has its own thorough `README.md` with Mermaid architecture diagrams, and ships a runnable demo that prints evidence of its guarantees.

A recurring theme: the Zaris .NET client exposes one powerful primitive — **optimistic versioned CAS** (`GetAsync`→`.Version`, `PutAsync` with `IfMatchVersion`/`IfAbsent`, retry on `Conflict`) — and these solutions show how far correct, race-free distributed coordination goes on top of it (plus native sorted sets, streams, and write-time TTL where useful).

| # | Solution | Problem it solves | Tests | Highlights |
|---|----------|-------------------|:---:|------------|
| 1 | [`rate-limiter-gateway`](./rate-limiter-gateway/) | Distributed rate limiting at an API gateway | 12 | Sliding-window + token-bucket, race-free via CAS; **global** limit enforced across 3 gateway instances sharing one store |
| 2 | [`leaderboard-presence`](./leaderboard-presence/) | Real-time leaderboard + presence at scale | 28 | Native sorted sets, windowed boards, heartbeat presence, **SignalR live push** (4000/4000 events) |
| 3 | [`saga-outbox`](./saga-outbox/) | Reliable distributed transactions across microservices | 24 | 4-service saga + compensation, transactional outbox, crash-recovery, at-least-once + dedupe |
| 4 | [`live-events-betting`](./live-events-betting/) | In-play betting engine (money-critical, low-latency) | 25 | Race-free wallet debit (1000 concurrent → exact, never negative), odds-staleness guard, market suspension, **exactly-once settlement** |
| 5 | [`job-queue`](./job-queue/) | Reliable distributed background job queue | 16 | Visibility-lease claiming (100 workers → one winner), lease-expiry requeue, retry/backoff, dead-letter queue |
| 6 | [`feature-flags`](./feature-flags/) | Feature flags + dynamic config without redeploys | 40 | Sticky % rollouts (20% over 50k = 19.82%, 0 disagreements), live propagation (~182ms), CAS no-lost-updates, audit trail |

**145 tests, all green.**

## Running any solution

```
cd <solution-folder>
dotnet test        # all green
dotnet run --project src/<...>.Demo    # (or ./run-demo.ps1) — end-to-end demonstration
```

Each solution's `nuget.config` resolves the `Clustron.Zaris.*` 2.0.1 packages (and everything else) from nuget.org. Every demo runs against an embedded in-process Zaris; swap the connection string to `zaris://host:port/<store>` to run against a real networked cluster with no code change.

## A note on honesty

These were built to be real and to tell the truth about Zaris. Where the client lacks a capability (no server-side increment, no prefix-scan, native pub/sub is RESP-only), each solution documents the mechanism it used instead (CAS loops, own index keys, version-polling for propagation). One genuine InProc bug was surfaced along the way — `ExpireAsync` on an existing key fails on a standalone/embedded node; the solutions use write-time TTL (`PutOptions.Metadata.Ttl`), which works, and say so.

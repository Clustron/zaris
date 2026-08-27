# Sorted Sets

Demonstrates the native **sorted-set** data structure (`ISortedSetClient`, reached via
`((IZaris)client).SortedSets`) by building a game **leaderboard**: members ranked by a
numeric score, kept ordered by Zaris.

## What it shows

- `AddAsync` — add members with scores (`ScoredMember(member, score)`)
- `ScoreAsync` — read one member's score
- `IncrementAsync` — atomically bump a member's score
- `RangeByRankAsync(..., reverse: true)` — the ranking, highest score first
- `RankAsync(..., reverse: true)` — a single member's position
- `RangeByScoreAsync` / `CountByScoreAsync` — members within a score band
- `CountAsync` — total members
- `RemoveAsync` — drop a member

## Run

```bash
dotnet run
```

By default it connects to an in-process store (`zaris://inproc/teststore` from
`appsettings.json`), so no cluster is required. Point `ConnectionStrings:teststore`
at a real cluster (`zaris://host:7861/store`) to run it against a live deployment.

using Clustron.Zaris.Abstractions;
using Clustron.Zaris.Client;

namespace LeaderboardPresence.Core;

/// <inheritdoc />
public sealed class LeaderboardService : ILeaderboardService
{
    private readonly IZaris _z;
    private readonly IClock _clock;
    private readonly LeaderboardOptions _opts;

    public LeaderboardService(IZaris zaris, IClock clock, LeaderboardOptions? options = null)
    {
        _z = zaris ?? throw new ArgumentNullException(nameof(zaris));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _opts = options ?? new LeaderboardOptions();
    }

    private ISortedSetClient Ss => _z.SortedSets;

    public async Task<SubmitResult> SubmitScoreAsync(string board, string player, double delta, CancellationToken ct = default)
    {
        var now = _clock.UtcNow;
        var inc = await Ss.IncrementAsync(Keys.Board(board, LeaderboardWindow.AllTime, now), player, delta, ct);
        Ensure(inc, "increment all-time");
        var newScore = inc.Value;

        if (_opts.WriteToRollingWindows)
        {
            await Ss.IncrementAsync(Keys.Board(board, LeaderboardWindow.Daily, now), player, delta, ct);
            await Ss.IncrementAsync(Keys.Board(board, LeaderboardWindow.Weekly, now), player, delta, ct);
        }

        var rank = await RankOneBasedAsync(board, player, LeaderboardWindow.AllTime, now, ct) ?? 0;
        var result = new SubmitResult(player, newScore, rank, delta);

        if (_opts.PublishChanges)
            await PublishChangeAsync(board, LeaderboardWindow.AllTime, player, newScore, rank, delta, now, ct);

        return result;
    }

    public async Task<SubmitResult> SetScoreAsync(string board, string player, double score, CancellationToken ct = default)
    {
        var now = _clock.UtcNow;
        var add = await Ss.AddAsync(Keys.Board(board, LeaderboardWindow.AllTime, now), new[] { new ScoredMember(player, score) }, ct);
        Ensure(add, "set all-time");

        if (_opts.WriteToRollingWindows)
        {
            await Ss.AddAsync(Keys.Board(board, LeaderboardWindow.Daily, now), new[] { new ScoredMember(player, score) }, ct);
            await Ss.AddAsync(Keys.Board(board, LeaderboardWindow.Weekly, now), new[] { new ScoredMember(player, score) }, ct);
        }

        var rank = await RankOneBasedAsync(board, player, LeaderboardWindow.AllTime, now, ct) ?? 0;
        var result = new SubmitResult(player, score, rank, score);

        if (_opts.PublishChanges)
            await PublishChangeAsync(board, LeaderboardWindow.AllTime, player, score, rank, score, now, ct);

        return result;
    }

    public async Task<IReadOnlyList<RankedEntry>> GetTopAsync(string board, int count, LeaderboardWindow window = LeaderboardWindow.AllTime, CancellationToken ct = default)
    {
        if (count <= 0) return Array.Empty<RankedEntry>();
        var key = Keys.Board(board, window, _clock.UtcNow);
        var range = await Ss.RangeByRankAsync(key, 0, count - 1, reverse: true, ct);
        Ensure(range, "top");
        var list = new List<RankedEntry>(range.Value!.Count);
        for (var i = 0; i < range.Value!.Count; i++)
        {
            var m = range.Value[i];
            list.Add(new RankedEntry(m.Member, m.Score, i + 1));
        }
        return list;
    }

    public async Task<RankedEntry?> GetRankAsync(string board, string player, LeaderboardWindow window = LeaderboardWindow.AllTime, CancellationToken ct = default)
    {
        var now = _clock.UtcNow;
        var key = Keys.Board(board, window, now);
        var score = await Ss.ScoreAsync(key, player, ct);
        Ensure(score, "score");
        if (score.Value is null) return null;
        var rank = await RankOneBasedAsync(board, player, window, now, ct);
        return rank is null ? null : new RankedEntry(player, score.Value.Value, rank.Value);
    }

    public async Task<IReadOnlyList<RankedEntry>> GetNeighborsAsync(string board, string player, int radius, LeaderboardWindow window = LeaderboardWindow.AllTime, CancellationToken ct = default)
    {
        if (radius < 0) radius = 0;
        var now = _clock.UtcNow;
        var key = Keys.Board(board, window, now);
        var revRank = await Ss.RankAsync(key, player, reverse: true, ct);
        Ensure(revRank, "neighbors rank");
        if (revRank.Value is null) return Array.Empty<RankedEntry>();

        var center = revRank.Value.Value;          // 0-based (descending)
        var start = Math.Max(0, center - radius);
        var stop = center + radius;
        var range = await Ss.RangeByRankAsync(key, start, stop, reverse: true, ct);
        Ensure(range, "neighbors range");

        var list = new List<RankedEntry>(range.Value!.Count);
        for (var i = 0; i < range.Value!.Count; i++)
        {
            var m = range.Value[i];
            list.Add(new RankedEntry(m.Member, m.Score, start + i + 1)); // 1-based
        }
        return list;
    }

    public async Task<long> GetCountAsync(string board, LeaderboardWindow window = LeaderboardWindow.AllTime, CancellationToken ct = default)
    {
        var res = await Ss.CountAsync(Keys.Board(board, window, _clock.UtcNow), ct);
        Ensure(res, "count");
        return res.Value;
    }

    public async Task<int> ReapExpiredWindowsAsync(string board, CancellationToken ct = default)
    {
        var now = _clock.UtcNow;
        var removed = 0;

        // Daily: delete day-stamped keys older than retention (scan a lookback window; deletes are idempotent).
        var dailyCutoffDays = (int)Math.Ceiling(_opts.DailyRetention.TotalDays);
        for (var d = dailyCutoffDays + 1; d <= dailyCutoffDays + 400; d++)
        {
            var stamp = Keys.DayStamp(now.AddDays(-d));
            var del = await _z.DeleteAsync(Keys.DailyBoardForStamp(board, stamp), null, ct);
            if (del.IsSuccess && del.Mutated) removed++;
        }

        // Weekly: same idea on ISO-week-stamped keys.
        var weeklyCutoffWeeks = (int)Math.Ceiling(_opts.WeeklyRetention.TotalDays / 7.0);
        for (var w = weeklyCutoffWeeks + 1; w <= weeklyCutoffWeeks + 160; w++)
        {
            var stamp = Keys.WeekStamp(now.AddDays(-7 * w));
            var del = await _z.DeleteAsync(Keys.WeeklyBoardForStamp(board, stamp), null, ct);
            if (del.IsSuccess && del.Mutated) removed++;
        }

        return removed;
    }

    public async Task<IAsyncDisposable> SubscribeChangesAsync(Func<LeaderboardChange, Task> handler, string? board = null, CancellationToken ct = default)
    {
        Task OnMessage(ChannelMessage m)
        {
            var change = Json.From<LeaderboardChange>(m.Payload);
            return change is null ? Task.CompletedTask : handler(change);
        }

        return board is null
            ? await _z.PubSub.PSubscribeAsync(new[] { Keys.LeaderboardChannelPattern }, OnMessage, ct)
            : await _z.PubSub.SubscribeAsync(new[] { Keys.LeaderboardChannel(board) }, OnMessage, ct);
    }

    public async Task<IReadOnlyList<LeaderboardChange>> ReadAuditAsync(string board, int count, CancellationToken ct = default)
    {
        var range = await _z.Streams.RangeAsync(Keys.LeaderboardStream(board), "-", "+", count, reverse: true, ct);
        if (!range.IsSuccess || range.Value is null) return Array.Empty<LeaderboardChange>();
        var list = new List<LeaderboardChange>(range.Value.Count);
        foreach (var e in range.Value)
        {
            foreach (var f in e.Fields)
            {
                if (f.Key == "data")
                {
                    var c = Json.From<LeaderboardChange>(f.Value);
                    if (c is not null) list.Add(c);
                }
            }
        }
        return list;
    }

    // ── helpers ──

    private async Task<long?> RankOneBasedAsync(string board, string player, LeaderboardWindow window, DateTimeOffset now, CancellationToken ct)
    {
        var rev = await Ss.RankAsync(Keys.Board(board, window, now), player, reverse: true, ct);
        Ensure(rev, "rank");
        return rev.Value is null ? null : rev.Value.Value + 1; // 0-based desc -> 1-based
    }

    private async Task PublishChangeAsync(string board, LeaderboardWindow window, string player, double score, long rank, double delta, DateTimeOffset now, CancellationToken ct)
    {
        var change = new LeaderboardChange(board, window.ToString(), player, score, rank, delta, now);
        var bytes = Json.Bytes(change);
        try
        {
            await _z.PubSub.PublishAsync(Keys.LeaderboardChannel(board), bytes, ct);
            long? maxLen = _opts.AuditStreamMaxLen > 0 ? _opts.AuditStreamMaxLen : null;
            await _z.Streams.AddAsync(Keys.LeaderboardStream(board),
                new[] { new KeyValuePair<string, byte[]>("data", bytes) }, "*", maxLen, null, false, ct);
        }
        catch
        {
            // Change propagation is best-effort; a publish/stream hiccup must not fail the submit.
        }
    }

    private static void Ensure(KvResult r, string what)
    {
        if (!r.IsSuccess)
            throw new LeaderboardException($"Zaris {what} failed: {r.Status} {r.Error}");
    }
}

/// <summary>Thrown when an underlying Zaris operation fails unexpectedly.</summary>
public sealed class LeaderboardException : Exception
{
    public LeaderboardException(string message) : base(message) { }
}

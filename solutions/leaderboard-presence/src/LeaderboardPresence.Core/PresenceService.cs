using Clustron.Zaris.Abstractions;
using Clustron.Zaris.Client;

namespace LeaderboardPresence.Core;

/// <inheritdoc />
public sealed class PresenceService : IPresenceService
{
    private readonly IZaris _z;
    private readonly IClock _clock;
    private readonly PresenceOptions _opts;

    public PresenceService(IZaris zaris, IClock clock, PresenceOptions? options = null)
    {
        _z = zaris ?? throw new ArgumentNullException(nameof(zaris));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _opts = options ?? new PresenceOptions();
    }

    private long NowMs => _clock.UtcNow.ToUnixTimeMilliseconds();

    public async Task<PresenceSnapshot> HeartbeatAsync(string player, string? detail = null, CancellationToken ct = default)
    {
        var (prevScore, prevRecord) = await ReadStateAsync(player, ct);
        var prev = Derive(prevScore, prevRecord, NowMs);

        var now = NowMs;
        var record = new PresenceRecord
        {
            LastSeenMs = now,
            Detail = detail ?? prevRecord?.Detail,
            ExplicitStatus = null,
            ExplicitAtMs = 0
        };
        await WriteStateAsync(player, now, record, ct);

        var snap = new PresenceSnapshot(player, PresenceStatus.Online, _clock.UtcNow, record.Detail);
        if (prev != PresenceStatus.Online)
            await PublishAsync(new PresenceChange(player, PresenceStatus.Online, prev, record.Detail, _clock.UtcNow), ct);
        return snap;
    }

    public async Task<PresenceSnapshot> SetStatusAsync(string player, PresenceStatus status, string? detail = null, CancellationToken ct = default)
    {
        var (prevScore, prevRecord) = await ReadStateAsync(player, ct);
        var prev = Derive(prevScore, prevRecord, NowMs);
        var now = NowMs;

        if (status == PresenceStatus.Offline)
        {
            await _z.SortedSets.RemoveAsync(Keys.PresenceIndex, new[] { player }, ct);
            await _z.DeleteAsync(Keys.PresenceKey(player), null, ct);
            var offSnap = new PresenceSnapshot(player, PresenceStatus.Offline, _clock.UtcNow, detail ?? prevRecord?.Detail);
            if (prev != PresenceStatus.Offline)
                await PublishAsync(new PresenceChange(player, PresenceStatus.Offline, prev, offSnap.Detail, _clock.UtcNow), ct);
            return offSnap;
        }

        var record = new PresenceRecord
        {
            LastSeenMs = now,
            Detail = detail ?? prevRecord?.Detail,
            ExplicitStatus = status == PresenceStatus.Away ? PresenceStatus.Away : null,
            ExplicitAtMs = now
        };
        await WriteStateAsync(player, now, record, ct);

        var snap = new PresenceSnapshot(player, status, _clock.UtcNow, record.Detail);
        if (prev != status)
            await PublishAsync(new PresenceChange(player, status, prev, record.Detail, _clock.UtcNow), ct);
        return snap;
    }

    public async Task<PresenceSnapshot> GetAsync(string player, CancellationToken ct = default)
    {
        var (score, record) = await ReadStateAsync(player, ct);
        var status = Derive(score, record, NowMs);
        var lastSeenMs = score ?? record?.LastSeenMs ?? 0;
        var lastSeen = lastSeenMs == 0 ? DateTimeOffset.MinValue : DateTimeOffset.FromUnixTimeMilliseconds(lastSeenMs);
        return new PresenceSnapshot(player, status, lastSeen, record?.Detail);
    }

    public async Task<IReadOnlyList<PresenceSnapshot>> ListByStatusAsync(PresenceStatus status, CancellationToken ct = default)
    {
        var now = NowMs;
        // Everyone still indexed; classify each by heartbeat age. (Offline-but-indexed = awaiting sweep.)
        var all = await _z.SortedSets.RangeByScoreAsync(Keys.PresenceIndex, double.MinValue, double.MaxValue, reverse: true, ct);
        if (!all.IsSuccess || all.Value is null) return Array.Empty<PresenceSnapshot>();

        var list = new List<PresenceSnapshot>();
        foreach (var m in all.Value)
        {
            var s = DeriveFromScore((long)m.Score, now);
            if (s == status)
                list.Add(new PresenceSnapshot(m.Member, s, DateTimeOffset.FromUnixTimeMilliseconds((long)m.Score), null));
        }
        return list;
    }

    public Task<IReadOnlyList<PresenceSnapshot>> ListOnlineAsync(CancellationToken ct = default)
        => ListByStatusAsync(PresenceStatus.Online, ct);

    public async Task<long> OnlineCountAsync(CancellationToken ct = default)
    {
        var onlineCut = NowMs - (long)_opts.OnlineWindow.TotalMilliseconds;
        var res = await _z.SortedSets.CountByScoreAsync(Keys.PresenceIndex, onlineCut, double.MaxValue, ct);
        return res.IsSuccess ? res.Value : 0;
    }

    public async Task<IReadOnlyList<string>> SweepAsync(CancellationToken ct = default)
    {
        var now = NowMs;
        var offlineCut = now - (long)_opts.AwayWindow.TotalMilliseconds; // score strictly below => offline
        var stale = await _z.SortedSets.RangeByScoreAsync(Keys.PresenceIndex, double.MinValue, offlineCut - 1, reverse: false, ct);
        if (!stale.IsSuccess || stale.Value is null || stale.Value.Count == 0) return Array.Empty<string>();

        var reaped = new List<string>(stale.Value.Count);
        foreach (var m in stale.Value) reaped.Add(m.Member);

        await _z.SortedSets.RemoveAsync(Keys.PresenceIndex, reaped, ct);
        foreach (var player in reaped)
        {
            await _z.DeleteAsync(Keys.PresenceKey(player), null, ct);
            await PublishAsync(new PresenceChange(player, PresenceStatus.Offline, PresenceStatus.Away, null, _clock.UtcNow), ct);
        }
        return reaped;
    }

    public async Task<IAsyncDisposable> SubscribeAsync(Func<PresenceChange, Task> handler, CancellationToken ct = default)
    {
        Task OnMessage(ChannelMessage m)
        {
            var change = Json.From<PresenceChange>(m.Payload);
            return change is null ? Task.CompletedTask : handler(change);
        }
        return await _z.PubSub.SubscribeAsync(new[] { Keys.PresenceChannel }, OnMessage, ct);
    }

    public async Task<IReadOnlyList<PresenceChange>> ReadAuditAsync(int count, CancellationToken ct = default)
    {
        var range = await _z.Streams.RangeAsync(Keys.PresenceStream, "-", "+", count, reverse: true, ct);
        if (!range.IsSuccess || range.Value is null) return Array.Empty<PresenceChange>();
        var list = new List<PresenceChange>(range.Value.Count);
        foreach (var e in range.Value)
            foreach (var f in e.Fields)
                if (f.Key == "data")
                {
                    var c = Json.From<PresenceChange>(f.Value);
                    if (c is not null) list.Add(c);
                }
        return list;
    }

    // ── helpers ──

    private async Task<(long? score, PresenceRecord? record)> ReadStateAsync(string player, CancellationToken ct)
    {
        var score = await _z.SortedSets.ScoreAsync(Keys.PresenceIndex, player, ct);
        var rec = await _z.GetAsync<byte[]>(Keys.PresenceKey(player), null, ct);
        PresenceRecord? record = null;
        if (rec.IsSuccess && rec.Value is not null)
            record = Json.From<PresenceRecord>(rec.Value);
        long? s = score.IsSuccess && score.Value is not null ? (long)score.Value.Value : null;
        return (s, record);
    }

    private async Task WriteStateAsync(string player, long nowMs, PresenceRecord record, CancellationToken ct)
    {
        await _z.SortedSets.AddAsync(Keys.PresenceIndex, new[] { new ScoredMember(player, nowMs) }, ct);
        var opts = new PutOptions { Metadata = new EntryMetadata { Ttl = _opts.KeyTtl } };
        await _z.PutAsync<byte[]>(Keys.PresenceKey(player), Json.Bytes(record), opts, ct);
    }

    private PresenceStatus Derive(long? score, PresenceRecord? record, long nowMs)
    {
        if (score is null && record is null) return PresenceStatus.Offline;
        var lastSeen = score ?? record?.LastSeenMs ?? 0;
        var derived = DeriveFromScore(lastSeen, nowMs);
        if (derived == PresenceStatus.Offline) return PresenceStatus.Offline;
        // Honour a manual override that is at least as recent as the last heartbeat.
        if (record?.ExplicitStatus is { } ex && record.ExplicitAtMs >= lastSeen)
            return ex;
        return derived;
    }

    private PresenceStatus DeriveFromScore(long lastSeenMs, long nowMs)
    {
        var age = nowMs - lastSeenMs;
        if (age <= (long)_opts.OnlineWindow.TotalMilliseconds) return PresenceStatus.Online;
        if (age <= (long)_opts.AwayWindow.TotalMilliseconds) return PresenceStatus.Away;
        return PresenceStatus.Offline;
    }

    private async Task PublishAsync(PresenceChange change, CancellationToken ct)
    {
        if (!_opts.PublishChanges) return;
        var bytes = Json.Bytes(change);
        try
        {
            await _z.PubSub.PublishAsync(Keys.PresenceChannel, bytes, ct);
            await _z.Streams.AddAsync(Keys.PresenceStream,
                new[] { new KeyValuePair<string, byte[]>("data", bytes) }, "*", 10_000, null, false, ct);
        }
        catch
        {
            // best-effort
        }
    }
}

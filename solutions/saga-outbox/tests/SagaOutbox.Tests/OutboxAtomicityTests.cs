using SagaOutbox.Services;
using Xunit;

namespace SagaOutbox.Tests;

/// <summary>Proves the dual-write problem is gone: state change and event are one atomic document write.</summary>
public class OutboxAtomicityTests
{
    [Fact]
    public async Task Charge_writes_state_and_event_in_a_single_document()
    {
        var w = await World.NewAsync();
        await w.Payments.EnsureAccountAsync("c1", 10_000);

        var before = await w.Store.ReadAsync<Account>(PaymentService.Key("c1"));
        var r = await w.Payments.ChargeAsync("c1", "cmd-1", "o1", 3_000);
        var after = await w.Store.ReadAsync<Account>(PaymentService.Key("c1"));

        Assert.True(r.Charged);
        Assert.Equal(7_000, after.Value!.BalanceCents);                  // state changed
        Assert.Contains(after.Value.Outbox, e => e.Type == "PaymentCharged"); // event present
        // Exactly one write committed both: version advanced by exactly one.
        Assert.Equal(before.Version + 1, after.Version);
    }

    [Fact]
    public async Task Concurrent_charges_on_one_account_lose_no_state_and_no_events()
    {
        var w = await World.NewAsync();
        await w.Payments.EnsureAccountAsync("c1", 100_000);
        const int n = 40;

        var tasks = Enumerable.Range(0, n).Select(i =>
            w.Payments.ChargeAsync("c1", $"cmd-{i}", "o1", 1_000));
        await Task.WhenAll(tasks);

        var doc = await w.Store.ReadAsync<Account>(PaymentService.Key("c1"));
        Assert.Equal(100_000 - n * 1_000, doc.Value!.BalanceCents);          // every debit applied
        Assert.Equal(n, doc.Value.Outbox.Count(e => e.Type == "PaymentCharged")); // every event kept
        Assert.Equal(n, doc.Value.OutboxSeq);                                // sequence is gap-free
    }
}

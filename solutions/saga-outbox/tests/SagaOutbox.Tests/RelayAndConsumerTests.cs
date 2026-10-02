using SagaOutbox.Outbox;
using SagaOutbox.Services;
using Xunit;

namespace SagaOutbox.Tests;

/// <summary>Relay delivery (at-least-once) + consumer dedupe (effectively-once).</summary>
public class RelayAndConsumerTests
{
    [Fact]
    public async Task Relay_delivers_undispatched_events_then_marks_them()
    {
        var w = await World.NewAsync();
        await w.Payments.EnsureAccountAsync("c1", 10_000);
        await w.Payments.ChargeAsync("c1", "cmd-1", "o1", 1_000);

        var delivered = await w.Relay.PumpAsync(w.Payments.Registry, "payment");
        Assert.Equal(1, delivered);
        Assert.Equal(1, await w.Bus.CountAsync(World.Topic));

        // Nothing left to deliver on a second cycle.
        Assert.Equal(0, await w.Relay.PumpAsync(w.Payments.Registry, "payment"));
        Assert.Equal(1, await w.Bus.CountAsync(World.Topic));
    }

    [Fact]
    public async Task Relay_crash_between_publish_and_mark_causes_at_least_once_redelivery()
    {
        var w = await World.NewAsync();
        await w.Payments.EnsureAccountAsync("c1", 10_000);
        var r = await w.Payments.ChargeAsync("c1", "cmd-1", "o1", 1_000);
        var key = PaymentService.Key("c1");

        // Crash right after publishing, before the dispatched flag is persisted.
        w.Relay.CrashAfterPublishing = _ => true;
        await Assert.ThrowsAsync<RelayCrashException>(() => w.Relay.PumpKeyAsync(key, "payment"));
        Assert.Equal(1, await w.Bus.CountAsync(World.Topic)); // published once

        // Recover: the event is still undispatched, so it is delivered again -> a DUPLICATE on the bus.
        w.Relay.CrashAfterPublishing = null;
        await w.Relay.PumpKeyAsync(key, "payment");
        Assert.Equal(2, await w.Bus.CountAsync(World.Topic));

        var recs = await w.Bus.ReadAsync(World.Topic, 0);
        Assert.Equal(2, recs.Count);
        Assert.Single(recs.Select(x => x.EventId).Distinct()); // same event id twice (at-least-once)
    }

    [Fact]
    public async Task Consumer_applies_each_event_id_exactly_once_despite_duplicates()
    {
        var w = await World.NewAsync();
        // Two bus records with the SAME event id (a duplicate the relay could have produced on crash).
        await w.Bus.AppendAsync(World.Topic, "ev-1", "PaymentCharged", "payment", "{}");
        await w.Bus.AppendAsync(World.Topic, "ev-1", "PaymentCharged", "payment", "{}");
        await w.Bus.AppendAsync(World.Topic, "ev-2", "ShipmentCreated", "shipping", "{}");

        var consumer = new BusConsumer(w.Store, w.Bus, World.Topic, "audit");
        var applied = new List<string>();
        await consumer.PollAsync(rec => { applied.Add(rec.EventId); return Task.CompletedTask; });

        Assert.Equal(new[] { "ev-1", "ev-2" }, applied); // ev-1 handled once, not twice
    }

    [Fact]
    public async Task Consumer_resumes_from_its_cursor_and_never_reapplies()
    {
        var w = await World.NewAsync();
        var consumer = new BusConsumer(w.Store, w.Bus, World.Topic, "audit");
        var count = 0;

        await w.Bus.AppendAsync(World.Topic, "ev-1", "T", "s", "{}");
        Assert.Equal(1, await consumer.PollAsync(_ => { count++; return Task.CompletedTask; }));

        await w.Bus.AppendAsync(World.Topic, "ev-2", "T", "s", "{}");
        Assert.Equal(1, await consumer.PollAsync(_ => { count++; return Task.CompletedTask; }));

        // Re-poll with nothing new: no effects re-applied.
        Assert.Equal(0, await consumer.PollAsync(_ => { count++; return Task.CompletedTask; }));
        Assert.Equal(2, count);
    }
}

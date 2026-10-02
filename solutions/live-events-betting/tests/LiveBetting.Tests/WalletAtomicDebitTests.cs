using LiveBetting.Core.Domain;
using LiveBetting.Core.Services;
using Xunit;

namespace LiveBetting.Tests;

/// <summary>
/// Proves the money-safety core: the atomic, versioned-CAS wallet debit never overdraws, never
/// double-spends under heavy concurrency, and applies each transaction exactly once.
/// </summary>
public class WalletAtomicDebitTests
{
    [Fact]
    public async Task Debit_reduces_balance_and_credit_restores_it()
    {
        await using var e = await TestHarness.NewAsync();
        await e.Wallets.EnsureWalletAsync("u", 1_000);

        var d = await e.Wallets.DebitAsync("u", "t1", 300);
        Assert.Equal(TxnOutcome.Applied, d.Outcome);
        Assert.Equal(700, d.BalanceMinor);

        var c = await e.Wallets.CreditAsync("u", "c1", 200);
        Assert.Equal(TxnOutcome.Applied, c.Outcome);
        Assert.Equal(900, c.BalanceMinor);
    }

    [Fact]
    public async Task Debit_is_idempotent_by_transaction_id()
    {
        await using var e = await TestHarness.NewAsync();
        await e.Wallets.EnsureWalletAsync("u", 1_000);

        var first = await e.Wallets.DebitAsync("u", "same-txn", 400);
        var again = await e.Wallets.DebitAsync("u", "same-txn", 400);

        Assert.Equal(TxnOutcome.Applied, first.Outcome);
        Assert.Equal(TxnOutcome.AlreadyApplied, again.Outcome); // no second charge
        Assert.Equal(600, await e.Wallets.GetBalanceAsync("u"));
    }

    [Fact]
    public async Task Debit_never_overdraws()
    {
        await using var e = await TestHarness.NewAsync();
        await e.Wallets.EnsureWalletAsync("u", 500);

        var d = await e.Wallets.DebitAsync("u", "t", 501);
        Assert.Equal(TxnOutcome.InsufficientFunds, d.Outcome);
        Assert.Equal(500, await e.Wallets.GetBalanceAsync("u")); // unchanged, never negative
    }

    [Fact]
    public async Task One_thousand_concurrent_distinct_debits_are_exact_and_never_negative()
    {
        await using var e = await TestHarness.NewAsync();
        const long opening = 1_000_00; // 1000.00
        await e.Wallets.EnsureWalletAsync("u", opening);

        const int n = 1000;
        const long stake = 150; // 1.50 each; total demand 1500.00 >> balance → most must be refused

        var results = await Task.WhenAll(Enumerable.Range(0, n).Select(i =>
            e.Wallets.DebitAsync("u", $"txn-{i}", stake)));

        var applied = results.Count(r => r.Outcome == TxnOutcome.Applied);
        var refused = results.Count(r => r.Outcome == TxnOutcome.InsufficientFunds);

        Assert.Equal(n, applied + refused);
        // Exactly as many debits as the balance can fund, and not one more.
        Assert.Equal((int)(opening / stake), applied);

        var balance = await e.Wallets.GetBalanceAsync("u");
        Assert.True(balance >= 0, "balance must never go negative");
        Assert.Equal(opening - applied * stake, balance);          // exact to the cent
        Assert.True(balance < stake, "no further debit should have been fundable");
    }

    [Fact]
    public async Task Concurrent_duplicate_txn_ids_apply_at_most_once_each()
    {
        await using var e = await TestHarness.NewAsync();
        await e.Wallets.EnsureWalletAsync("u", 10_000);

        // 50 distinct txns, each submitted 20 times concurrently (client retries).
        var tasks = new List<Task<TxnResult>>();
        for (var i = 0; i < 50; i++)
            for (var r = 0; r < 20; r++)
                tasks.Add(e.Wallets.DebitAsync("u", $"txn-{i}", 100));

        await Task.WhenAll(tasks);

        // Each txn id moved the balance exactly once → 50 * 100 = 5000 debited.
        Assert.Equal(10_000 - 50 * 100, await e.Wallets.GetBalanceAsync("u"));
    }
}

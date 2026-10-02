using LiveBetting.Core.Domain;
using LiveBetting.Core.Infrastructure;

namespace LiveBetting.Core.Services;

public enum TxnOutcome
{
    /// <summary>The balance was moved by this call.</summary>
    Applied,
    /// <summary>This transaction id was already applied; this call was a no-op (idempotent replay).</summary>
    AlreadyApplied,
    /// <summary>A debit was refused because the balance would have gone negative; nothing was written.</summary>
    InsufficientFunds,
    /// <summary>The wallet does not exist.</summary>
    NoWallet
}

public readonly record struct TxnResult(TxnOutcome Outcome, long BalanceMinor)
{
    public bool Moved => Outcome == TxnOutcome.Applied;
    public bool OkForPlacement => Outcome is TxnOutcome.Applied or TxnOutcome.AlreadyApplied;
}

/// <summary>
/// The money-safe account service. Every balance change is an <b>idempotent, versioned CAS</b> keyed
/// by a unique transaction id:
/// <list type="bullet">
///   <item><b>No overdraw / never negative</b> — a debit that would take the balance below zero returns
///         <see cref="TxnOutcome.InsufficientFunds"/> and writes nothing.</item>
///   <item><b>No double-spend under concurrency</b> — thousands of concurrent debits each read-modify-write
///         the single wallet document under CAS; a loser re-reads the committed balance and retries, so
///         every accepted debit is applied against a fresh balance exactly once.</item>
///   <item><b>Exactly-once across retries / crashes / re-settlement</b> — the txn id is recorded in the
///         same write that moves the balance, so replaying a debit or a payout credit is a no-op.</item>
/// </list>
/// </summary>
public sealed class WalletService
{
    private readonly DocumentStore _store;

    public WalletService(DocumentStore store) => _store = store;

    /// <summary>Creates a wallet with a starting balance if it does not already exist; returns the current balance.</summary>
    public async Task<long> EnsureWalletAsync(string userId, long openingBalanceMinor, CancellationToken ct = default)
    {
        var w = await _store.MutateAsync<Wallet>(
            Keys.Wallet(userId),
            mutate: existing => null,                // exists already → no change
            create: () => new Wallet { UserId = userId, BalanceMinor = openingBalanceMinor },
            ct: ct);
        return w.BalanceMinor;
    }

    public async Task<long> GetBalanceAsync(string userId, CancellationToken ct = default)
    {
        var d = await _store.ReadAsync<Wallet>(Keys.Wallet(userId), ct);
        return d.Found ? d.Value!.BalanceMinor : 0;
    }

    public async Task<Wallet?> GetAsync(string userId, CancellationToken ct = default)
    {
        var d = await _store.ReadAsync<Wallet>(Keys.Wallet(userId), ct);
        return d.Found ? d.Value : null;
    }

    /// <summary>
    /// Atomically debits <paramref name="amountMinor"/> under transaction id <paramref name="txnId"/>.
    /// Never lets the balance go negative; applies the debit exactly once for a given txn id.
    /// </summary>
    public Task<TxnResult> DebitAsync(string userId, string txnId, long amountMinor, CancellationToken ct = default)
        => ApplyAsync(userId, txnId, -amountMinor, allowNegative: false, ct);

    /// <summary>Atomically credits <paramref name="amountMinor"/> under transaction id <paramref name="txnId"/>, exactly once.</summary>
    public Task<TxnResult> CreditAsync(string userId, string txnId, long amountMinor, CancellationToken ct = default)
        => ApplyAsync(userId, txnId, amountMinor, allowNegative: true, ct);

    private async Task<TxnResult> ApplyAsync(string userId, string txnId, long deltaMinor, bool allowNegative, CancellationToken ct)
    {
        var key = Keys.Wallet(userId);

        // Hand-rolled CAS loop (rather than DocumentStore.MutateAsync) so we can surface three distinct
        // outcomes — applied, already-applied (idempotent), insufficient-funds — without writing on the
        // last two. The retry budget matches the store's default.
        for (var attempt = 0; attempt < 128; attempt++)
        {
            var cur = await _store.ReadAsync<Wallet>(key, ct);
            if (!cur.Found)
                return new TxnResult(TxnOutcome.NoWallet, 0);

            var w = cur.Value!;
            if (w.AppliedTxns.Contains(txnId))
                return new TxnResult(TxnOutcome.AlreadyApplied, w.BalanceMinor); // idempotent: already done.

            var next = w.BalanceMinor + deltaMinor;
            if (!allowNegative && next < 0)
                return new TxnResult(TxnOutcome.InsufficientFunds, w.BalanceMinor); // never go negative; no write.

            var updated = new Wallet
            {
                UserId = w.UserId,
                BalanceMinor = next,
                AppliedTxns = new HashSet<string>(w.AppliedTxns) { txnId }
            };

            if (await _store.CompareAndSwapAsync(key, cur.Version, updated, null, ct))
                return new TxnResult(TxnOutcome.Applied, next);
            // lost the race — another txn committed; re-read and retry.
        }
        throw new BettingStoreException($"Wallet CAS retry budget exhausted for '{userId}' (txn {txnId}).");
    }
}

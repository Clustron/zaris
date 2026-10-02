using SagaOutbox.Infrastructure;
using SagaOutbox.Outbox;

namespace SagaOutbox.Services;

public enum PaymentOutcome { Charged, Declined }

/// <summary>Result of a charge attempt (the same value is returned for a retried/duplicate command).</summary>
public readonly record struct ChargeResult(PaymentOutcome Outcome, long AmountCents, long BalanceAfter)
{
    public bool Charged => Outcome == PaymentOutcome.Charged;
}

/// <summary>A charge recorded in the account's ledger, so a later refund can find its amount.</summary>
public sealed class ChargeRecord
{
    public string CommandId { get; set; } = "";
    public string OrderId { get; set; } = "";
    public long AmountCents { get; set; }
    public bool Refunded { get; set; }
}

/// <summary>Payment account aggregate: a balance, a charge ledger, plus the inherited idempotency + outbox.</summary>
public sealed class Account : OutboxAggregate
{
    public string CustomerId { get; set; } = "";
    public long BalanceCents { get; set; }
    public List<ChargeRecord> Charges { get; set; } = new();
}

/// <summary>
/// Owns the payment account keyspace (<c>payment:account:&lt;customer&gt;</c>). Charging debits the
/// balance, records the charge in the ledger and emits a <c>PaymentCharged</c> (or
/// <c>PaymentDeclined</c>) event — all in one atomic document write. Refunding (the compensation)
/// credits the balance back and emits <c>PaymentRefunded</c>. Every operation is idempotent on its
/// command id, so the saga can safely retry after a crash without double-charging.
/// </summary>
public sealed class PaymentService
{
    private readonly ZarisDocumentStore _store;
    private readonly OutboxRegistry _registry;

    public PaymentService(ZarisDocumentStore store)
    {
        _store = store;
        _registry = new OutboxRegistry(store, "payment");
    }

    public OutboxRegistry Registry => _registry;
    public static string Key(string customerId) => $"payment:account:{customerId}";

    /// <summary>Seeds an account with a starting balance (idempotent) and registers it for the relay.</summary>
    public async Task EnsureAccountAsync(string customerId, long openingBalanceCents, CancellationToken ct = default)
    {
        var created = await _store.CreateAsync(Key(customerId),
            new Account { CustomerId = customerId, BalanceCents = openingBalanceCents }, ct).ConfigureAwait(false);
        await _registry.RegisterAsync(Key(customerId), ct).ConfigureAwait(false);
        _ = created;
    }

    public async Task<long> GetBalanceAsync(string customerId, CancellationToken ct = default)
    {
        var doc = await _store.ReadAsync<Account>(Key(customerId), ct).ConfigureAwait(false);
        return doc.Found && doc.Value is not null ? doc.Value.BalanceCents : 0;
    }

    /// <summary>Charges <paramref name="amountCents"/>. Idempotent on <paramref name="commandId"/>.</summary>
    public async Task<ChargeResult> ChargeAsync(
        string customerId, string commandId, string orderId, long amountCents, CancellationToken ct = default)
    {
        ChargeResult result = default;
        await _store.MutateAsync<Account>(
            Key(customerId),
            acct =>
            {
                if (acct.AlreadyProcessed(commandId))
                {
                    var prior = acct.Charges.FirstOrDefault(c => c.CommandId == commandId);
                    result = prior is not null
                        ? new ChargeResult(PaymentOutcome.Charged, prior.AmountCents, acct.BalanceCents)
                        : new ChargeResult(PaymentOutcome.Declined, amountCents, acct.BalanceCents);
                    return null; // idempotent no-op
                }

                acct.MarkProcessed(commandId);
                if (acct.BalanceCents < amountCents)
                {
                    acct.Emit("PaymentDeclined", new { commandId, orderId, amountCents, reason = "insufficient_funds" });
                    result = new ChargeResult(PaymentOutcome.Declined, amountCents, acct.BalanceCents);
                    return acct;
                }

                acct.BalanceCents -= amountCents;
                acct.Charges.Add(new ChargeRecord { CommandId = commandId, OrderId = orderId, AmountCents = amountCents });
                acct.Emit("PaymentCharged", new { commandId, orderId, amountCents, balanceAfter = acct.BalanceCents });
                result = new ChargeResult(PaymentOutcome.Charged, amountCents, acct.BalanceCents);
                return acct;
            },
            create: () => new Account { CustomerId = customerId, BalanceCents = 0 },
            ct: ct).ConfigureAwait(false);
        return result;
    }

    /// <summary>
    /// Compensation for a prior charge: credits the charged amount back. Idempotent on
    /// <paramref name="commandId"/>; a no-op if the original charge was never applied or already refunded.
    /// </summary>
    public async Task<long> RefundAsync(
        string customerId, string commandId, string originalChargeCommandId, CancellationToken ct = default)
    {
        long balanceAfter = 0;
        await _store.MutateAsync<Account>(
            Key(customerId),
            acct =>
            {
                balanceAfter = acct.BalanceCents;
                if (acct.AlreadyProcessed(commandId)) return null;

                acct.MarkProcessed(commandId);
                var charge = acct.Charges.FirstOrDefault(c => c.CommandId == originalChargeCommandId && !c.Refunded);
                if (charge is null)
                {
                    // Nothing to refund (charge was declined / never happened). Record a no-effect compensation.
                    acct.Emit("PaymentRefundNoop", new { commandId, originalChargeCommandId });
                    return acct;
                }

                charge.Refunded = true;
                acct.BalanceCents += charge.AmountCents;
                balanceAfter = acct.BalanceCents;
                acct.Emit("PaymentRefunded", new { commandId, originalChargeCommandId, amountCents = charge.AmountCents, balanceAfter });
                return acct;
            },
            create: () => new Account { CustomerId = customerId, BalanceCents = 0 },
            ct: ct).ConfigureAwait(false);
        return balanceAfter;
    }
}

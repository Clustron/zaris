namespace SagaOutbox.Saga;

public enum SagaStatus { Running, Completed, Compensating, Compensated, Failed }

public enum StepStatus { Pending, Done, Failed, Compensated }

/// <summary>The persisted record of one step in the saga log.</summary>
public sealed class StepRecord
{
    public string Name { get; set; } = "";
    public StepStatus Status { get; set; } = StepStatus.Pending;
    public string? Detail { get; set; }
}

/// <summary>
/// The durable saga log. Everything the orchestrator needs to resume after a crash lives here and is
/// persisted (via CAS) after every state transition: the business inputs, the current position in the
/// pipeline, and the status of every step. A restarted orchestrator rehydrates this document and
/// continues forward — or compensates — from exactly where it left off.
/// </summary>
public sealed class SagaDocument
{
    public string SagaId { get; set; } = "";
    public string OrderId { get; set; } = "";
    public string CustomerId { get; set; } = "";
    public string Sku { get; set; } = "";
    public int Quantity { get; set; }
    public long AmountCents { get; set; }

    public SagaStatus Status { get; set; } = SagaStatus.Running;

    /// <summary>Index of the next forward step to execute.</summary>
    public int CurrentStep { get; set; }

    /// <summary>One entry per forward step, in pipeline order.</summary>
    public List<StepRecord> Steps { get; set; } = new();

    public string? FailureReason { get; set; }

    public StepRecord StepFor(string name)
    {
        var s = Steps.FirstOrDefault(x => x.Name == name);
        if (s is null) { s = new StepRecord { Name = name }; Steps.Add(s); }
        return s;
    }
}

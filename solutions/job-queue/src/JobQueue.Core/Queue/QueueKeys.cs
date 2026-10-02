namespace JobQueue.Queue;

/// <summary>
/// Centralises every Zaris key the queue uses. Because the Zaris client has <b>no prefix scan</b>, the
/// queue maintains its own index documents (<see cref="ActiveIndex"/>, <see cref="DeadLetterIndex"/>)
/// to discover work — the store is otherwise a flat key/value space.
///
/// Keys are namespaced by queue name so many independent queues can share one store.
/// </summary>
public sealed class QueueKeys
{
    public string Queue { get; }

    public QueueKeys(string queue) => Queue = queue;

    /// <summary>The durable job document (authoritative state + lease). Mutated only via CAS.</summary>
    public string Job(string id) => $"job:{Queue}:{id}";

    /// <summary>
    /// The in-flight visibility-lease marker. Written at claim time with a <b>write-time Zaris TTL</b>
    /// equal to the lease duration, so it self-reclaims if the worker crashes and never acks. It is an
    /// auxiliary, self-cleaning representation of the "in-flight set" used for monitoring; the
    /// authoritative lease is <see cref="Job.LeaseExpiresUtc"/> on the job document itself.
    /// </summary>
    public string Lease(string id) => $"lease:{Queue}:{id}";

    /// <summary>Set of job ids that are not yet terminal (Ready or Claimed). The worker's work-discovery index.</summary>
    public string ActiveIndex => $"idx:{Queue}:active";

    /// <summary>Set of dead-lettered job ids (for inspection / replay).</summary>
    public string DeadLetterIndex => $"idx:{Queue}:dlq";
}

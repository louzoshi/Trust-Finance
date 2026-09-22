namespace TrustFinance.Domain.Entities;

/// <summary>
/// One line of the audit trail: who touched what, when, and what changed.
///
/// <see cref="Changes"/> is a readable sentence ("Valor: 100,00 -> 120,00") rather than a
/// JSON diff on purpose. The trail exists to be read by a person asking why a figure moved,
/// and a format that needs parsing before it can be read fails at the one job it has.
/// </summary>
public sealed class AuditEntry
{
    public const int MaxChangesLength = 1000;

    private AuditEntry() { }

    public AuditEntry(int userId, string entity, int entityId, AuditAction action, DateTimeOffset occurredAt, string? changes = null)
    {
        UserId = userId;
        Entity = entity;
        EntityId = entityId;
        Action = action;
        OccurredAt = occurredAt.UtcDateTime;
        Changes = Truncate(changes);
    }

    public int Id { get; private set; }

    /// <summary>The owner of the row that changed. The trail is read per user, like everything else.</summary>
    public int UserId { get; private set; }

    /// <summary>
    /// UTC, and a <see cref="DateTime"/> rather than a <see cref="DateTimeOffset"/>: SQLite
    /// stores the offset form as text it cannot sort, and the trail is read newest first.
    /// The offset is not information that is lost — every line is written in UTC.
    /// </summary>
    public DateTime OccurredAt { get; private set; }

    /// <summary>The entity type name, as the domain calls it: "Transaction", "Trade", "Payout".</summary>
    public string Entity { get; private set; } = string.Empty;

    public int EntityId { get; private set; }

    public AuditAction Action { get; private set; }

    /// <summary>Field by field, old to new. Null for a creation and a deletion, where the action is the whole story.</summary>
    public string? Changes { get; private set; }

    // A single correction touching every column of a fixed-income paper can run long, and
    // the trail is worth more complete-but-clipped than refused.
    private static string? Truncate(string? changes) => changes switch
    {
        null or "" => null,
        { Length: <= MaxChangesLength } => changes,
        _ => string.Concat(changes.AsSpan(0, MaxChangesLength - 1), "…")
    };
}

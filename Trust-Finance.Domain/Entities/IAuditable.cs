namespace TrustFinance.Domain.Entities;

/// <summary>
/// A row whose creation, correction and deletion are recorded in the audit trail.
///
/// The owner is part of the interface because the trail is written by the database
/// context, which has the changed entity and nothing else — no HTTP request, no signed-in
/// principal. Reading the owner off the row itself is what keeps the trail correct for
/// writes that happen outside a request, such as a recurrence posting its occurrences on
/// startup.
/// </summary>
public interface IAuditable
{
    int UserId { get; }
}

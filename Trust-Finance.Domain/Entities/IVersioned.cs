namespace TrustFinance.Domain.Entities;

/// <summary>
/// A row that carries the number of times it has been written, so an edit can say which
/// version it was made against.
///
/// Without it the last write wins in silence: two tabs open on the same transaction, both
/// showing R$ 100, one corrects it to R$ 120 and the other to R$ 90, and whoever saves
/// last erases the other's correction with no trace that it happened. Money-bearing rows
/// are exactly where that is unacceptable, so they carry a version and a stale edit is
/// refused rather than applied.
///
/// The version is stamped by the database context on every save; nothing else assigns it.
/// </summary>
public interface IVersioned
{
    int Version { get; }
}

using TrustFinance.Domain.Notifications;

namespace TrustFinance.App.Services;

/// <summary>
/// The one answer every service gives to a correction made against a row that has moved on.
///
/// Two guards produce it. The first compares the version the form was opened on with the
/// one in the database, which is what catches the tab left open since this morning. The
/// second is the concurrency token EF puts in the WHERE clause, which catches the writes
/// that land between that comparison and the save. A refused write says so; it never wins
/// by being last.
/// </summary>
public static class Concurrency
{
    public const string Message =
        "Este registro foi alterado em outra janela. Recarregue a página e refaça a correção.";

    public static Result Conflict() => Result.Fail("Version", Message);
}

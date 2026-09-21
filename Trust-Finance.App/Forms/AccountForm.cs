using TrustFinance.Domain.Entities;

namespace TrustFinance.App.Forms;

public class AccountForm
{
    public string Name { get; set; } = string.Empty;
    public AccountKind Kind { get; set; } = AccountKind.Checking;
    public int? ClosingDay { get; set; }
    public int? DueDay { get; set; }

    public static AccountForm From(Account a) => new()
    {
        Name = a.Name,
        Kind = a.Kind,
        ClosingDay = a.ClosingDay,
        DueDay = a.DueDay
    };

    public Account ToAccount(int userId) => new(Name, Kind, userId, ClosingDay, DueDay);
}

using TrustFinance.Domain.Notifications;
using TrustFinance.Domain.ValueObjects;

namespace TrustFinance.Domain.Entities;

public enum PayoutKind
{
    /// <summary>Dividendo: paid out of taxed profit, exempt in the holder's hands.</summary>
    Dividend = 1,

    /// <summary>Juros sobre capital próprio: 15% withheld at source, declared as exclusive taxation.</summary>
    InterestOnEquity = 2,

    /// <summary>Rendimento de FII: exempt for an individual holding under 10% of the fund, on an exchange-traded fund with 50+ holders.</summary>
    FundIncome = 3
}

public static class PayoutKinds
{
    /// <summary>The rate the payer withholds. Only JCP is taxed at source; the others reach the account whole.</summary>
    public static decimal WithholdingRate(this PayoutKind kind)
        => kind == PayoutKind.InterestOnEquity ? 0.15m : 0m;
}

/// <summary>
/// Cash a holding paid out. Stored as a total rather than per share so a statement
/// line can be copied as is; the per-share figure is derived for display.
///
/// Gross and withheld are both kept because the annual return asks for them
/// separately: dividends and FII income go under exempt income, JCP under income
/// taxed exclusively at source, each at the gross figure with the tax shown alongside.
/// </summary>
public class Payout : Notifiable, IVersioned, IAuditable
{
    public const int MaxNoteLength = 200;

    private Payout() { }

    public Payout(
        string ticker,
        PayoutKind kind,
        DateOnly paymentDate,
        decimal quantity,
        decimal grossAmount,
        decimal withheldTax,
        int userId,
        string? note = null)
    {
        var symbol = new Ticker(ticker);
        AddNotifications(symbol);

        AddNotificationIf(!Enum.IsDefined(kind), nameof(Kind), "Tipo de provento inválido");
        AddNotificationIf(paymentDate == default, nameof(PaymentDate), "Informe a data de pagamento");
        AddNotificationIf(quantity <= 0, nameof(Quantity), "A quantidade precisa ser maior que zero");
        AddNotificationIf(grossAmount <= 0, nameof(GrossAmount), "O valor bruto precisa ser maior que zero");
        AddNotificationIf(withheldTax < 0, nameof(WithheldTax), "O imposto retido não pode ser negativo");
        AddNotificationIf(withheldTax > grossAmount, nameof(WithheldTax), "O imposto retido não pode passar do valor bruto");
        AddNotificationIf(userId <= 0, nameof(UserId), "Provento sem dono");

        note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        AddNotificationIf(note?.Length > MaxNoteLength, nameof(Note),
            $"A observação deve ter no máximo {MaxNoteLength} caracteres");

        Ticker = symbol.Value;
        Kind = kind;
        PaymentDate = paymentDate;
        Quantity = quantity;
        GrossAmount = grossAmount;
        WithheldTax = withheldTax;
        UserId = userId;
        Note = note;
    }

    public int Id { get; private set; }

    /// <summary>Stamped by the database on every write. See <see cref="IVersioned"/>.</summary>
    public int Version { get; private set; }

    public string Ticker { get; private set; } = string.Empty;
    public PayoutKind Kind { get; private set; }
    public DateOnly PaymentDate { get; private set; }

    /// <summary>How many shares earned it — what was held on the record date, not today.</summary>
    public decimal Quantity { get; private set; }
    public decimal GrossAmount { get; private set; }
    public decimal WithheldTax { get; private set; }
    public string? Note { get; private set; }

    public int UserId { get; private set; }
    public User User { get; private set; } = null!;

    /// <summary>What actually landed in the account.</summary>
    public decimal NetAmount => GrossAmount - WithheldTax;

    public decimal GrossPerShare => Quantity > 0 ? GrossAmount / Quantity : 0m;

    public void CorrectTo(Payout corrected)
    {
        ClearNotifications();
        AddNotifications(corrected);

        if (IsInvalid)
            return;

        Ticker = corrected.Ticker;
        Kind = corrected.Kind;
        PaymentDate = corrected.PaymentDate;
        Quantity = corrected.Quantity;
        GrossAmount = corrected.GrossAmount;
        WithheldTax = corrected.WithheldTax;
        Note = corrected.Note;
    }
}

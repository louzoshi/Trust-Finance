using TrustFinance.Domain.Notifications;
using TrustFinance.Domain.ValueObjects;

namespace TrustFinance.Domain.Entities;

public enum CorporateActionKind
{
    /// <summary>Desdobramento: each share becomes several; the average price divides by the same factor.</summary>
    Split = 1,

    /// <summary>Grupamento: several shares become one; the average price multiplies.</summary>
    ReverseSplit = 2,

    /// <summary>
    /// Bonificação: new shares handed out for free, at a cost per share the company
    /// declares. The cost is what the tax authority accepts as basis — it is not zero.
    /// </summary>
    Bonus = 3
}

/// <summary>
/// Something the issuer did to a ticker that changes how many shares exist without a
/// trade. Without these a ledger silently breaks on the first split: quantities are
/// off by a factor of ten and the average price says a stock lost 90% overnight.
///
/// The ratio is stored as a plain factor. A 1:10 split is 10; a 10:1 reverse split is
/// 0.1; a 10% bonus is 0.1 (new shares per share held). One number, no
/// numerator/denominator pair to get backwards.
/// </summary>
public class CorporateAction : Notifiable, IVersioned, IAuditable
{
    private CorporateAction() { }

    public CorporateAction(
        string ticker,
        CorporateActionKind kind,
        DateOnly date,
        decimal factor,
        decimal? bonusUnitCost,
        int userId)
    {
        var symbol = new Ticker(ticker);
        AddNotifications(symbol);

        AddNotificationIf(!Enum.IsDefined(kind), nameof(Kind), "Tipo de evento inválido");
        AddNotificationIf(date == default, nameof(Date), "Informe a data");
        AddNotificationIf(userId <= 0, nameof(UserId), "Evento sem dono");

        switch (kind)
        {
            case CorporateActionKind.Split:
                AddNotificationIf(factor <= 1, nameof(Factor), "Num desdobramento cada ação vira mais de uma — o fator precisa ser maior que 1");
                break;
            case CorporateActionKind.ReverseSplit:
                AddNotificationIf(factor <= 0 || factor >= 1, nameof(Factor), "Num grupamento várias ações viram uma — o fator precisa ficar entre 0 e 1");
                break;
            case CorporateActionKind.Bonus:
                AddNotificationIf(factor <= 0, nameof(Factor), "A bonificação precisa ser maior que zero");
                AddNotificationIf(bonusUnitCost is null or < 0, nameof(BonusUnitCost), "Informe o custo unitário atribuído pela empresa (pode ser zero)");
                break;
        }

        Ticker = symbol.Value;
        Kind = kind;
        Date = date;
        Factor = factor;
        BonusUnitCost = kind == CorporateActionKind.Bonus ? bonusUnitCost : null;
        UserId = userId;
    }

    public int Id { get; private set; }

    /// <summary>Stamped by the database on every write. See <see cref="IVersioned"/>.</summary>
    public int Version { get; private set; }

    public string Ticker { get; private set; } = string.Empty;
    public CorporateActionKind Kind { get; private set; }
    public DateOnly Date { get; private set; }

    /// <summary>Split/reverse: what one share becomes. Bonus: new shares per share held.</summary>
    public decimal Factor { get; private set; }

    /// <summary>Bonus only: the cost per new share the issuer declared.</summary>
    public decimal? BonusUnitCost { get; private set; }

    public int UserId { get; private set; }
    public User User { get; private set; } = null!;

    public void CorrectTo(CorporateAction corrected)
    {
        ClearNotifications();
        AddNotifications(corrected);

        if (IsInvalid)
            return;

        Ticker = corrected.Ticker;
        Kind = corrected.Kind;
        Date = corrected.Date;
        Factor = corrected.Factor;
        BonusUnitCost = corrected.BonusUnitCost;
    }
}

using TrustFinance.Domain.Notifications;

namespace TrustFinance.Domain.Entities;

/// <summary>What the paper pays. Decides how it accrues and whether it can be marked to market.</summary>
public enum IndexKind
{
    /// <summary>Prefixado: a rate agreed up front. The only kind whose future is known today.</summary>
    Fixed = 1,

    /// <summary>A percentage of the CDI — "110% do CDI".</summary>
    PercentOfCdi = 2,

    /// <summary>CDI plus a spread — "CDI + 2%".</summary>
    CdiPlus = 3,

    /// <summary>IPCA plus a real rate — "IPCA + 6%".</summary>
    IpcaPlus = 4,

    /// <summary>The SELIC plus a spread, which is what Tesouro Selic pays.</summary>
    SelicPlus = 5
}

/// <summary>What the paper is. Decides the tax treatment and whether the FGC is behind it.</summary>
public enum FixedIncomeKind
{
    Cdb = 1,
    Lci = 2,
    Lca = 3,
    TesouroSelic = 4,
    TesouroPrefixado = 5,
    TesouroIpca = 6,
    Debenture = 7,
    DebentureIncentivada = 8,
    Cri = 9,
    Cra = 10,
    Lc = 11,
    Poupanca = 12
}

public static class FixedIncomeKinds
{
    /// <summary>
    /// Exempt from income tax for an individual. The reason a 90% CDI LCI can beat a
    /// 105% CDI CDB: the CDB pays tax on the gain and the LCI does not.
    /// </summary>
    public static bool IsTaxExempt(this FixedIncomeKind kind) => kind is
        FixedIncomeKind.Lci or FixedIncomeKind.Lca or FixedIncomeKind.Cri or
        FixedIncomeKind.Cra or FixedIncomeKind.DebentureIncentivada or FixedIncomeKind.Poupanca;

    /// <summary>
    /// Covered by the Fundo Garantidor de Créditos. Treasury paper is not — it is the
    /// sovereign itself, which is better, not worse. Debentures and securitised paper
    /// carry the issuer's risk alone.
    /// </summary>
    public static bool IsFgcCovered(this FixedIncomeKind kind) => kind is
        FixedIncomeKind.Cdb or FixedIncomeKind.Lci or FixedIncomeKind.Lca or
        FixedIncomeKind.Lc or FixedIncomeKind.Poupanca;

    public static bool IsTreasury(this FixedIncomeKind kind) => kind is
        FixedIncomeKind.TesouroSelic or FixedIncomeKind.TesouroPrefixado or FixedIncomeKind.TesouroIpca;
}

/// <summary>
/// One fixed-income position: a CDB, an LCI, a Tesouro paper. Unlike a share, it is not
/// a quantity at a price — it is an amount lent to an issuer, at a rate, until a date.
/// Its worth today is a calculation, not a quote, which is why it needs its own entity
/// rather than another <see cref="Trade"/> with an odd asset class.
/// </summary>
public class FixedIncomeInvestment : Notifiable, IVersioned, IAuditable
{
    public const int MinIssuerLength = 2;
    public const int MaxIssuerLength = 60;

    private FixedIncomeInvestment() { }

    public FixedIncomeInvestment(
        string issuer,
        FixedIncomeKind kind,
        IndexKind index,
        decimal rate,
        decimal principal,
        DateOnly purchaseDate,
        DateOnly maturityDate,
        int userId,
        bool hasDailyLiquidity = false,
        string? note = null)
    {
        var trimmedIssuer = (issuer ?? string.Empty).Trim();

        AddNotificationIf(trimmedIssuer.Length is < MinIssuerLength or > MaxIssuerLength, nameof(Issuer),
            $"O emissor deve ter entre {MinIssuerLength} e {MaxIssuerLength} caracteres");
        AddNotificationIf(!Enum.IsDefined(kind), nameof(Kind), "Tipo de papel inválido");
        AddNotificationIf(!Enum.IsDefined(index), nameof(Index), "Indexador inválido");
        AddNotificationIf(principal <= 0, nameof(Principal), "O valor aplicado precisa ser maior que zero");
        AddNotificationIf(purchaseDate == default, nameof(PurchaseDate), "Informe a data da aplicação");
        AddNotificationIf(maturityDate <= purchaseDate, nameof(MaturityDate), "O vencimento precisa ser depois da aplicação");
        AddNotificationIf(userId <= 0, nameof(UserId), "Aplicação sem dona");

        // A percentage of the CDI is a percentage: 110 means 110%, and a zero would be
        // a paper that pays nothing. Every other index is a rate and may legitimately
        // be zero — "IPCA + 0%" is a real, if poor, offer.
        if (index == IndexKind.PercentOfCdi)
            AddNotificationIf(rate <= 0, nameof(Rate), "O percentual do CDI precisa ser maior que zero");
        else
            AddNotificationIf(rate < 0, nameof(Rate), "A taxa não pode ser negativa");

        note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        AddNotificationIf(note?.Length > 200, nameof(Note), "A observação deve ter no máximo 200 caracteres");

        Issuer = trimmedIssuer;
        Kind = kind;
        Index = index;
        Rate = rate;
        Principal = principal;
        PurchaseDate = purchaseDate;
        MaturityDate = maturityDate;
        HasDailyLiquidity = hasDailyLiquidity;
        UserId = userId;
        Note = note;
    }

    public int Id { get; private set; }

    /// <summary>Stamped by the database on every write. See <see cref="IVersioned"/>.</summary>
    public int Version { get; private set; }

    public string Issuer { get; private set; } = string.Empty;
    public FixedIncomeKind Kind { get; private set; }
    public IndexKind Index { get; private set; }

    /// <summary>A percentage: 110 for "110% do CDI", 12.5 for a prefixado at 12,5% a.a., 6 for "IPCA + 6%".</summary>
    public decimal Rate { get; private set; }

    /// <summary>What was handed over on the purchase date.</summary>
    public decimal Principal { get; private set; }

    public DateOnly PurchaseDate { get; private set; }
    public DateOnly MaturityDate { get; private set; }

    /// <summary>Redeemable any day. Decides whether the mark-to-market number is actionable or academic.</summary>
    public bool HasDailyLiquidity { get; private set; }

    public string? Note { get; private set; }

    /// <summary>Set when the paper was redeemed or sold early; it stops accruing on that day.</summary>
    public DateOnly? RedeemedOn { get; private set; }

    public int UserId { get; private set; }
    public User User { get; private set; } = null!;

    public bool IsTaxExempt => Kind.IsTaxExempt();
    public bool IsFgcCovered => Kind.IsFgcCovered();
    public bool IsOpen => RedeemedOn is null;

    /// <summary>
    /// Only a prefixado is marked to market here.
    ///
    /// Paper tied to the CDI or the SELIC resets daily, so it has no duration to lose
    /// and its curve value already is its market value. An IPCA+ paper does move with
    /// rates, but its price comes from the <i>real</i> curve — discounting it at a
    /// nominal rate compares a real cash flow to a nominal one and produces a number
    /// that is not wrong by a little, it is meaningless. Until a real curve is
    /// available it stays on its curve, which is honest, rather than marked against
    /// the wrong one.
    /// </summary>
    public bool IsMarkedToMarket => Index is IndexKind.Fixed;

    /// <summary>The day accrual stops: the redemption, the maturity, or today — whichever comes first.</summary>
    public DateOnly AccrualEnd(DateOnly today)
    {
        var end = RedeemedOn ?? today;
        return end > MaturityDate ? MaturityDate : end;
    }

    public bool IsMatured(DateOnly today) => today >= MaturityDate;

    public void Redeem(DateOnly date)
    {
        AddNotificationIf(date < PurchaseDate, nameof(RedeemedOn), "O resgate não pode ser antes da aplicação");

        if (IsValid)
            RedeemedOn = date;
    }

    public void CorrectTo(FixedIncomeInvestment corrected)
    {
        ClearNotifications();
        AddNotifications(corrected);

        if (IsInvalid)
            return;

        Issuer = corrected.Issuer;
        Kind = corrected.Kind;
        Index = corrected.Index;
        Rate = corrected.Rate;
        Principal = corrected.Principal;
        PurchaseDate = corrected.PurchaseDate;
        MaturityDate = corrected.MaturityDate;
        HasDailyLiquidity = corrected.HasDailyLiquidity;
        Note = corrected.Note;
        RedeemedOn = corrected.RedeemedOn;
    }
}

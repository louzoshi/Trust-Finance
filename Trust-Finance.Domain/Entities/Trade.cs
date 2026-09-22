using TrustFinance.Domain.Notifications;
using TrustFinance.Domain.ValueObjects;

namespace TrustFinance.Domain.Entities;

public enum TradeSide
{
    Buy = 1,
    Sell = 2
}

/// <summary>
/// One purchase or sale. The ledger of trades is the source of truth for a position:
/// quantity and average price are always recomputed from it rather than stored, so a
/// corrected trade cannot leave a stale balance behind.
///
/// A Trade cannot exist in a state the portfolio maths would choke on. Every way in runs
/// the same rules, and an invalid one carries its notifications instead of throwing.
/// </summary>
public class Trade : Notifiable, IVersioned, IAuditable
{
    public const int MaxNoteLength = 200;

    /// <summary>For EF materialization only. Rows already in the table were validated on the way in.</summary>
    private Trade() { }

    public Trade(
        string ticker,
        AssetClass assetClass,
        TradeSide side,
        decimal quantity,
        decimal unitPrice,
        decimal fees,
        DateOnly date,
        int userId,
        string? note = null)
    {
        var symbol = new Ticker(ticker);
        AddNotifications(symbol);

        AddNotificationIf(quantity <= 0, nameof(Quantity), "A quantidade precisa ser maior que zero");
        AddNotificationIf(unitPrice <= 0, nameof(UnitPrice), "O preço precisa ser maior que zero");
        AddNotificationIf(fees < 0, nameof(Fees), "As taxas não podem ser negativas");
        AddNotificationIf(date == default, nameof(Date), "Informe a data");
        AddNotificationIf(userId <= 0, nameof(UserId), "Operação sem dono");

        note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        AddNotificationIf(note?.Length > MaxNoteLength, nameof(Note),
            $"A observação deve ter no máximo {MaxNoteLength} caracteres");

        Ticker = symbol.Value;
        Class = assetClass;
        Side = side;
        Quantity = quantity;
        UnitPrice = unitPrice;
        Fees = fees;
        Date = date;
        UserId = userId;
        Note = note;
    }

    public int Id { get; private set; }

    /// <summary>Stamped by the database on every write. See <see cref="IVersioned"/>.</summary>
    public int Version { get; private set; }

    public string Ticker { get; private set; } = string.Empty;
    public AssetClass Class { get; private set; }
    public TradeSide Side { get; private set; }

    /// <summary>Fractional for crypto, whole numbers everywhere else.</summary>
    public decimal Quantity { get; private set; }
    public decimal UnitPrice { get; private set; }

    /// <summary>Brokerage and exchange costs. Part of the cost basis on a buy, deducted from proceeds on a sell.</summary>
    public decimal Fees { get; private set; }

    public DateOnly Date { get; private set; }
    public string? Note { get; private set; }

    public int UserId { get; private set; }
    public User User { get; private set; } = null!;

    public decimal Gross => Quantity * UnitPrice;

    /// <summary>What left the account on a buy, or landed in it on a sell.</summary>
    public decimal NetCashFlow => Side == TradeSide.Buy ? Gross + Fees : Gross - Fees;

    /// <summary>
    /// Rewrites this trade from a corrected one. Going through a replacement rather than
    /// exposing setters means a correction is validated by exactly the same rules that
    /// govern creation — there is no second door into an invalid state.
    /// </summary>
    public void CorrectTo(Trade corrected)
    {
        ClearNotifications();
        AddNotifications(corrected);

        if (IsInvalid)
            return;

        Ticker = corrected.Ticker;
        Class = corrected.Class;
        Side = corrected.Side;
        Quantity = corrected.Quantity;
        UnitPrice = corrected.UnitPrice;
        Fees = corrected.Fees;
        Date = corrected.Date;
        Note = corrected.Note;
    }
}

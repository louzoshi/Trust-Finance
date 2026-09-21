using TrustFinance.Domain.Entities;

namespace TrustFinance.App.Forms;

/// <summary>What the payouts screen collects. The rules live on <see cref="Payout"/>.</summary>
public class PayoutForm
{
    public string Ticker { get; set; } = string.Empty;
    public PayoutKind Kind { get; set; } = PayoutKind.Dividend;
    public DateOnly? PaymentDate { get; set; }
    public decimal? Quantity { get; set; }
    public decimal? GrossAmount { get; set; }
    public decimal? WithheldTax { get; set; }
    public string? Note { get; set; }

    public static PayoutForm Empty(DateOnly today) => new() { PaymentDate = today };

    public static PayoutForm From(Payout p) => new()
    {
        Ticker = p.Ticker,
        Kind = p.Kind,
        PaymentDate = p.PaymentDate,
        Quantity = p.Quantity,
        GrossAmount = p.GrossAmount,
        WithheldTax = p.WithheldTax,
        Note = p.Note
    };

    /// <summary>JCP is withheld at 15%; the form fills it in so the user only overrides when the statement disagrees.</summary>
    public decimal SuggestedTax => Math.Round((GrossAmount ?? 0m) * Kind.WithholdingRate(), 2);

    public Payout ToPayout(int userId) => new(
        Ticker,
        Kind,
        PaymentDate ?? default,
        Quantity ?? 0m,
        GrossAmount ?? 0m,
        WithheldTax ?? SuggestedTax,
        userId,
        Note);
}

using TrustFinance.Domain.Notifications;

namespace TrustFinance.Domain.ValueObjects;

/// <summary>
/// A tradable symbol, always upper-case.
///
/// Normalization is the whole point: "petr4", "PETR4 " and "Petr4" are one asset, and
/// before this existed every caller had to remember to trim and upper-case. One of them
/// eventually would not, and the user would end up with two positions in the same stock.
/// </summary>
public sealed class Ticker : Notifiable, IEquatable<Ticker>
{
    public const int MinLength = 2;
    public const int MaxLength = 20;

    public string Value { get; } = string.Empty;

    public Ticker(string? value)
    {
        var normalized = (value ?? string.Empty).Trim().ToUpperInvariant();

        if (normalized.Length == 0)
        {
            AddNotification(nameof(Ticker), "Informe o ticker");
            return;
        }

        if (normalized.Length is < MinLength or > MaxLength)
            AddNotification(nameof(Ticker), $"O ticker deve ter entre {MinLength} e {MaxLength} caracteres");

        // Letters, digits and the dash that Tesouro Direto codes use. Nothing else: a
        // stray quote or space here would travel straight into a market-data URL.
        if (!normalized.All(c => char.IsAsciiLetterOrDigit(c) || c == '-'))
            AddNotification(nameof(Ticker), "O ticker aceita apenas letras, números e hífen");

        Value = normalized;
    }

    public override string ToString() => Value;

    public bool Equals(Ticker? other) => other is not null && Value == other.Value;
    public override bool Equals(object? obj) => Equals(obj as Ticker);
    public override int GetHashCode() => Value.GetHashCode();

    public static implicit operator string(Ticker ticker) => ticker.Value;
}

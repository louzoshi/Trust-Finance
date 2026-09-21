using System.Text.RegularExpressions;
using TrustFinance.Domain.Notifications;

namespace TrustFinance.Domain.ValueObjects;

/// <summary>
/// An address, trimmed and lower-cased, so "Ana@Example.com " and "ana@example.com"
/// cannot become two accounts.
/// </summary>
public sealed partial class Email : Notifiable, IEquatable<Email>
{
    public const int MaxLength = 100;

    public string Value { get; } = string.Empty;

    public Email(string? value)
    {
        var normalized = (value ?? string.Empty).Trim().ToLowerInvariant();

        if (normalized.Length == 0)
        {
            AddNotification(nameof(Email), "Informe o e-mail");
            return;
        }

        if (normalized.Length > MaxLength)
            AddNotification(nameof(Email), $"O e-mail deve ter no máximo {MaxLength} caracteres");

        if (!Pattern().IsMatch(normalized))
            AddNotification(nameof(Email), "E-mail inválido");

        Value = normalized;
    }

    public override string ToString() => Value;

    public bool Equals(Email? other) => other is not null && Value == other.Value;
    public override bool Equals(object? obj) => Equals(obj as Email);
    public override int GetHashCode() => Value.GetHashCode();

    public static implicit operator string(Email email) => email.Value;

    // Deliberately permissive: the only address proven to exist is one that answered a
    // message, so the job here is catching typos, not policing the RFC.
    [GeneratedRegex(@"^[^@\s]+@[^@\s.]+(\.[^@\s.]+)+$")]
    private static partial Regex Pattern();
}

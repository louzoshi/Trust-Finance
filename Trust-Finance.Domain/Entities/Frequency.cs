namespace TrustFinance.Domain.Entities;

/// <summary>
/// How often a recurring transaction repeats. Numbered from 1 for the same reason as
/// <see cref="TransactionType"/>: an unset form field must fail, not default to weekly.
/// </summary>
public enum Frequency
{
    Weekly = 1,
    Monthly = 2,
    Yearly = 3
}

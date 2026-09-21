namespace TrustFinance.Domain.Entities;

/// <summary>
/// Whether a transaction adds to or subtracts from the balance. Amount is always
/// stored positive — the sign lives here, not in the number, so a typo cannot
/// silently turn an expense into income.
/// </summary>
/// <remarks>
/// Numbered from 1 so that the default 0 is not a valid value: a form that
/// leaves the type unset fails validation instead of quietly becoming income.
/// </remarks>
public enum TransactionType
{
    Income = 1,
    Expense = 2
}

using System.Text.Json.Serialization;

namespace TF.Models;

/// <summary>
/// Whether a transaction adds to or subtracts from the balance. Amount is always
/// stored positive — the sign lives here, not in the number, so a typo cannot
/// silently turn an expense into income.
/// </summary>
/// <remarks>
/// Numbered from 1 so that the default 0 is not a valid value: a payload that
/// omits the type fails validation instead of quietly becoming income.
/// </remarks>
[JsonConverter(typeof(JsonStringEnumConverter<TransactionType>))]
public enum TransactionType
{
    Income = 1,
    Expense = 2
}

using TrustFinance.Domain.Entities;

namespace TrustFinance.Domain.Finance;

/// <summary>One card statement: the purchases between two closings, and what was paid against it.</summary>
public sealed record CardStatement(
    DateOnly Closing,
    DateOnly Due,
    decimal Purchases,
    decimal Payments,
    IReadOnlyList<Transaction> Lines)
{
    /// <summary>What is still owed on this statement. Negative means overpaid or refunded.</summary>
    public decimal Balance => Purchases - Payments;

    public bool IsOpen(DateOnly today) => today <= Closing;
    public bool IsPaid => Purchases > 0 && Payments >= Purchases;
    public bool IsOverdue(DateOnly today) => !IsPaid && Balance > 0 && today > Due;
}

public sealed record AccountBalance(Account Account, decimal Balance);

/// <summary>
/// Groups a card's transactions into statements. A purchase lands on the statement
/// closing on or after its date; a refund on the card does the same with the opposite
/// sign; a payment (a transfer into the card) settles the most recent statement that
/// had already closed when it was made — paying on the due date pays the statement
/// that just closed, not the one still open.
/// </summary>
public static class CardStatements
{
    public static IReadOnlyList<CardStatement> Build(Account card, IEnumerable<Transaction> cardTransactions, DateOnly today)
    {
        if (!card.IsCreditCard)
            return [];

        var byClosing = new SortedDictionary<DateOnly, (decimal Purchases, decimal Payments, List<Transaction> Lines)>();

        (decimal, decimal, List<Transaction>) At(DateOnly closing)
            => byClosing.TryGetValue(closing, out var s) ? s : (0m, 0m, []);

        var lines = cardTransactions.Where(t => t.AccountId == card.Id).OrderBy(t => t.Date).ThenBy(t => t.Id).ToList();

        foreach (var t in lines.Where(t => !t.IsTransfer))
        {
            var closing = card.StatementClosingFor(t.Date);
            var s = At(closing);
            s.Item1 += t.Type == TransactionType.Expense ? t.Amount : -t.Amount;
            s.Item3.Add(t);
            byClosing[closing] = s;
        }

        // The statement still open today exists even before its first purchase, so the
        // screen always has a "current statement" to show.
        var current = card.StatementClosingFor(today);
        if (!byClosing.ContainsKey(current))
            byClosing[current] = (0m, 0m, []);

        foreach (var payment in lines.Where(t => t.IsTransfer && t.Type == TransactionType.Income))
        {
            var settled = byClosing.Keys.Where(c => c < payment.Date).DefaultIfEmpty(byClosing.Keys.First()).Last();
            var s = At(settled);
            s.Item2 += payment.Amount;
            s.Item3.Add(payment);
            byClosing[settled] = s;
        }

        return [.. byClosing
            .Select(kv => new CardStatement(kv.Key, card.DueDateFor(kv.Key), kv.Value.Purchases, kv.Value.Payments, kv.Value.Lines))
            .OrderByDescending(s => s.Closing)];
    }

    /// <summary>Every account's balance from its own rows: cash on hand for cash accounts, what is owed (as a negative) for cards.</summary>
    public static IReadOnlyList<AccountBalance> Balances(IEnumerable<Account> accounts, IEnumerable<Transaction> transactions)
    {
        var sums = transactions
            .GroupBy(t => t.AccountId)
            .ToDictionary(g => g.Key, g => g.Sum(t => t.SignedAmount));

        return [.. accounts.Select(a => new AccountBalance(a, sums.GetValueOrDefault(a.Id)))];
    }
}

using TrustFinance.Domain.Entities;

namespace TrustFinance.Domain.Planning;

public enum NoticeKind
{
    PriceAlertAbove = 1,
    PriceAlertBelow = 2,
    BudgetOver = 3,
    BudgetNear = 4,
    ContributionGoal = 5
}

public enum NoticeLevel
{
    Info = 1,
    Success = 2,
    Warning = 3
}

/// <summary>
/// One line in the notification tray.
///
/// Derived state, not a stored record: the only things persisted are whether an alert
/// already fired and whether it was dismissed, so the tray cannot drift out of sync with
/// the data it describes.
///
/// It carries values, not sentences. Deciding that R$ 38,40 is written with a comma, or
/// that the message reads "subiu para", is the screen's job — the domain only knows
/// which ticker crossed which target.
/// </summary>
public sealed record Notice(
    string Id,
    NoticeKind Kind,
    NoticeLevel Level,
    string? Link,
    DateTime At,
    /// <summary>What the notice is about: a ticker, or a category name.</summary>
    string Subject,
    /// <summary>What happened — the price that fired, or the amount spent.</summary>
    decimal Value,
    /// <summary>What it is measured against — the target price, or the budget ceiling.</summary>
    decimal Reference);

public static class Notices
{
    /// <summary>
    /// Everything worth telling the user about right now, most urgent first. Each source
    /// is switched off independently, because a person who wants price alerts does not
    /// necessarily want to hear about groceries.
    /// </summary>
    public static IReadOnlyList<Notice> Build(
        IEnumerable<PriceAlert> alerts,
        IReadOnlyList<BudgetStatus> budgets,
        GoalProgress contribution,
        UserSettings settings,
        DateTime now)
    {
        var notices = new List<Notice>();

        if (settings.NotifyPriceAlerts)
        {
            foreach (var a in alerts.Where(a => a.TriggeredAt is not null && !a.Dismissed))
            {
                notices.Add(new Notice(
                    Id: $"alert-{a.Id}",
                    Kind: a.Direction == AlertDirection.Above ? NoticeKind.PriceAlertAbove : NoticeKind.PriceAlertBelow,
                    Level: a.Direction == AlertDirection.Above ? NoticeLevel.Success : NoticeLevel.Info,
                    Link: "/mercado",
                    At: a.TriggeredAt!.Value,
                    Subject: a.Ticker,
                    Value: a.TriggeredPrice ?? a.TargetPrice,
                    Reference: a.TargetPrice));
            }
        }

        if (settings.NotifyBudgetOverruns)
        {
            foreach (var b in budgets.Where(b => b.IsOver || b.IsNear))
            {
                notices.Add(new Notice(
                    Id: $"budget-{b.CategoryId}",
                    Kind: b.IsOver ? NoticeKind.BudgetOver : NoticeKind.BudgetNear,
                    Level: b.IsOver ? NoticeLevel.Warning : NoticeLevel.Info,
                    Link: "/metas",
                    At: now,
                    Subject: b.Category,
                    Value: b.Spent,
                    Reference: b.Limit));
            }
        }

        // Only worth raising once the month is far enough along that it is actionable.
        if (settings.NotifyContributionGoal && contribution.HasTarget && !contribution.IsMet && now.Day >= NudgeFromDay)
        {
            notices.Add(new Notice(
                Id: "contribution",
                Kind: NoticeKind.ContributionGoal,
                Level: NoticeLevel.Info,
                Link: "/metas",
                At: now,
                Subject: string.Empty,
                Value: contribution.Current,
                Reference: contribution.Target));
        }

        return [.. notices.OrderByDescending(n => n.Level).ThenByDescending(n => n.At)];
    }

    /// <summary>Day of the month from which an unmet contribution goal is worth mentioning.</summary>
    public const int NudgeFromDay = 20;
}

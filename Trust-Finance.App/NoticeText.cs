using TrustFinance.Domain.Planning;

namespace TrustFinance.App;

/// <summary>
/// Turns a <see cref="Notice"/> into the sentence the tray shows. This is the screen's
/// job, not the domain's: the domain knows PETR4 crossed 38, and this knows that reads
/// as "PETR4 subiu para R$ 38,48" in Brazilian Portuguese.
/// </summary>
public static class NoticeText
{
    public static string Title(this Notice n) => n.Kind switch
    {
        NoticeKind.PriceAlertAbove => $"{n.Subject} subiu para {Money.Format(n.Value)}",
        NoticeKind.PriceAlertBelow => $"{n.Subject} caiu para {Money.Format(n.Value)}",
        NoticeKind.BudgetOver => $"{n.Subject} estourou o orçamento",
        NoticeKind.BudgetNear => $"{n.Subject} está perto do limite",
        NoticeKind.ContributionGoal => "Meta de aporte ainda não batida",
        _ => n.Subject
    };

    public static string Detail(this Notice n) => n.Kind switch
    {
        NoticeKind.PriceAlertAbove or NoticeKind.PriceAlertBelow =>
            $"Seu alvo era {Money.Format(n.Reference)}.",
        NoticeKind.BudgetOver =>
            $"{Money.Format(n.Value)} de {Money.Format(n.Reference)} — {Money.Format(n.Value - n.Reference)} acima.",
        NoticeKind.BudgetNear =>
            $"{Money.Format(n.Value)} de {Money.Format(n.Reference)} usados.",
        NoticeKind.ContributionGoal =>
            $"Faltam {Money.Format(n.Reference - n.Value)} para os {Money.Format(n.Reference)} do mês.",
        _ => string.Empty
    };

    public static string LevelClass(this Notice n) => n.Level switch
    {
        NoticeLevel.Warning => "notice-warning",
        NoticeLevel.Success => "notice-success",
        _ => "notice-info"
    };
}

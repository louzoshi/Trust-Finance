using TrustFinance.Domain.Entities;

namespace TrustFinance.App.Services;

/// <summary>
/// Turns the trail's stored names into the words the rest of the app uses.
///
/// The trail stores what the domain calls things — "Transaction", "MaturityDate" — because
/// that is the name that stays true when a screen is renamed, and because a row written by
/// a background post has no user interface to borrow a label from. Translation belongs
/// here, at the edge, where the reader is.
/// </summary>
public static class AuditLabels
{
    private static readonly Dictionary<string, string> Entities = new()
    {
        ["Transaction"] = "Lançamento",
        ["RecurringTransaction"] = "Recorrência",
        ["Trade"] = "Operação",
        ["FixedIncomeInvestment"] = "Renda fixa",
        ["Payout"] = "Provento",
        ["CorporateAction"] = "Evento societário",
        ["Budget"] = "Orçamento",
        ["Account"] = "Conta",
        ["Category"] = "Categoria"
    };

    private static readonly Dictionary<string, string> Fields = new()
    {
        ["AccountId"] = "Conta",
        ["Amount"] = "Valor",
        ["BonusUnitCost"] = "Custo da bonificação",
        ["CategoryId"] = "Categoria",
        ["Class"] = "Classe",
        ["ClosingDay"] = "Fechamento",
        ["Date"] = "Data",
        ["Description"] = "Descrição",
        ["DueDay"] = "Vencimento da fatura",
        ["EndDate"] = "Fim",
        ["ExternalId"] = "Identificador do banco",
        ["Factor"] = "Fator",
        ["Fees"] = "Taxas",
        ["Frequency"] = "Frequência",
        ["GrossAmount"] = "Valor bruto",
        ["HasDailyLiquidity"] = "Liquidez diária",
        ["Index"] = "Índice",
        ["InstallmentCount"] = "Parcelas",
        ["InstallmentNumber"] = "Parcela",
        ["Issuer"] = "Emissor",
        ["Kind"] = "Tipo",
        ["MaturityDate"] = "Vencimento",
        ["MonthlyLimit"] = "Limite mensal",
        ["Name"] = "Nome",
        ["NextDate"] = "Próxima",
        ["Note"] = "Observação",
        ["PaymentDate"] = "Pagamento",
        ["Principal"] = "Principal",
        ["PurchaseDate"] = "Aplicação",
        ["Quantity"] = "Quantidade",
        ["Rate"] = "Taxa",
        ["RedeemedOn"] = "Resgate",
        ["Side"] = "Operação",
        ["Slug"] = "Identificador",
        ["StartDate"] = "Início",
        ["Ticker"] = "Ativo",
        ["Type"] = "Tipo",
        ["UnitPrice"] = "Preço unitário",
        ["WithheldTax"] = "IR retido"
    };

    public static string Entity(string name) => Entities.GetValueOrDefault(name, name);

    public static string Action(AuditAction action) => action switch
    {
        AuditAction.Created => "Criado",
        AuditAction.Updated => "Alterado",
        AuditAction.Deleted => "Excluído",
        _ => action.ToString()
    };

    /// <summary>"MaturityDate: 01/01/2027 -> 01/06/2027" as "Vencimento: 01/01/2027 → 01/06/2027".</summary>
    public static string Changes(string? changes)
    {
        if (string.IsNullOrEmpty(changes))
            return string.Empty;

        var parts = changes.Split("; ", StringSplitOptions.RemoveEmptyEntries).Select(part =>
        {
            var separator = part.IndexOf(':');
            if (separator <= 0)
                return part;

            var field = part[..separator];
            return $"{Fields.GetValueOrDefault(field, field)}{part[separator..]}";
        });

        return string.Join("; ", parts).Replace(" -> ", " → ");
    }
}

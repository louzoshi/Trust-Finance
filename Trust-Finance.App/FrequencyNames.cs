using TrustFinance.Domain.Entities;

namespace TrustFinance.App;

/// <summary>Screen names for <see cref="Frequency"/>, kept out of the domain like the asset class labels.</summary>
public static class FrequencyNames
{
    public static string Label(this Frequency f) => f switch
    {
        Frequency.Weekly => "Semanal",
        Frequency.Monthly => "Mensal",
        Frequency.Yearly => "Anual",
        _ => "—"
    };

    public static string Label(this AccountKind k) => k switch
    {
        AccountKind.Checking => "Conta corrente",
        AccountKind.Savings => "Poupança",
        AccountKind.Cash => "Dinheiro",
        AccountKind.CreditCard => "Cartão de crédito",
        _ => "—"
    };
}

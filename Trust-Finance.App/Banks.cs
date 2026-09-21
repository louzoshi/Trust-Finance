namespace TrustFinance.App;

/// <summary>The COMPE codes a person is likely to meet on a boleto, by name. Anything else shows as its number.</summary>
public static class Banks
{
    private static readonly Dictionary<string, string> Names = new()
    {
        ["001"] = "Banco do Brasil",
        ["033"] = "Santander",
        ["041"] = "Banrisul",
        ["070"] = "BRB",
        ["077"] = "Inter",
        ["104"] = "Caixa",
        ["208"] = "BTG Pactual",
        ["212"] = "Original",
        ["237"] = "Bradesco",
        ["260"] = "Nubank",
        ["290"] = "PagBank",
        ["323"] = "Mercado Pago",
        ["336"] = "C6",
        ["341"] = "Itaú",
        ["380"] = "PicPay",
        ["389"] = "Mercantil",
        ["422"] = "Safra",
        ["655"] = "Neon",
        ["748"] = "Sicredi",
        ["756"] = "Sicoob"
    };

    public static string Name(string? code)
        => code is not null && Names.TryGetValue(code, out var name) ? name : $"código {code}";
}

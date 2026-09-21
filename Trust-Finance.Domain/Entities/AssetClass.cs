namespace TrustFinance.Domain.Entities;

/// <summary>
/// What kind of thing an asset is. Drives allocation and which quote fields are
/// meaningful — a fixed-income paper has no dividend yield, a FII's yield is the number
/// people actually buy it for.
/// </summary>
/// <remarks>Numbered from 1 so an unset value is never silently a valid class.</remarks>
public enum AssetClass
{
    Stock = 1,
    Fii = 2,
    FixedIncome = 3,
    Etf = 4,
    Bdr = 5,
    Crypto = 6
}

public static class AssetClasses
{
    public static IReadOnlyList<AssetClass> All { get; } = Enum.GetValues<AssetClass>();

    /// <summary>
    /// Crypto is bought in fractions; a share of PETR4 is not. This decides how many
    /// decimals a quantity is stored and shown with.
    /// </summary>
    /// <remarks>
    /// The human-readable names for these live in the app layer. The domain has no
    /// opinion about what language the screen is in.
    /// </remarks>
    public static int QuantityDecimals(this AssetClass c) => c == AssetClass.Crypto ? 8 : 0;
}

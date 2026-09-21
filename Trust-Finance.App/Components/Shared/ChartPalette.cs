using ApexCharts;

namespace TrustFinance.App.Components.Shared;

/// <summary>
/// The colours of one theme, in concrete hex, and the single place they are written down.
/// ApexCharts draws to SVG attributes and cannot read CSS variables, so the charts need
/// real values; the page needs the same values as custom properties. Rather than keeping
/// two lists in step, the stylesheet's <c>:root</c> block is generated from this one by
/// <see cref="CssTokens"/> and emitted in the document head.
///
/// Income is blue and expense red on purpose: that pair survives red-green colour
/// blindness, and the +/− sign carries the meaning for anyone the colour fails.
/// </summary>
public sealed record ChartPalette
{
    public required string Page { get; init; }
    public required string Surface { get; init; }
    public required string TextPrimary { get; init; }
    public required string Secondary { get; init; }
    public required string Muted { get; init; }
    public required string Grid { get; init; }
    public required string Axis { get; init; }
    public required string Border { get; init; }
    public required string HoverWash { get; init; }
    public required string Income { get; init; }
    public required string IncomeStrong { get; init; }
    public required string Expense { get; init; }
    public required bool Dark { get; init; }

    /// <summary>
    /// Series colours for charts with no inherent order, such as the allocation donut.
    /// Chosen to stay apart for the common colour-vision deficiencies: no red/green pair
    /// carries meaning on its own, and each step also differs in lightness, so the set
    /// survives greyscale.
    /// </summary>
    public required IReadOnlyList<string> Categorical { get; init; }

    public static readonly ChartPalette Light = new()
    {
        Page = "#f9f9f7",
        Surface = "#fcfcfb",
        TextPrimary = "#0b0b0b",
        Secondary = "#52514e",
        Muted = "#898781",
        Grid = "#e1e0d9",
        Axis = "#c3c2b7",
        Border = "rgba(11, 11, 11, 0.1)",
        HoverWash = "rgba(11, 11, 11, 0.04)",
        Income = "#2a78d6",
        IncomeStrong = "#1c5cab",
        Expense = "#d03b3b",
        Dark = false,
        Categorical = ["#2a78d6", "#d98324", "#2e8b78", "#8256b5", "#b5566e", "#6b7a8f"]
    };

    public static readonly ChartPalette DarkTheme = new()
    {
        Page = "#0d0d0d",
        Surface = "#1a1a19",
        TextPrimary = "#ffffff",
        Secondary = "#c3c2b7",
        Muted = "#898781",
        Grid = "#2c2c2a",
        Axis = "#383835",
        Border = "rgba(255, 255, 255, 0.1)",
        HoverWash = "rgba(255, 255, 255, 0.06)",
        Income = "#3987e5",
        IncomeStrong = "#6da7ec",
        Expense = "#e66767",
        Dark = true,
        Categorical = ["#3987e5", "#e5a04b", "#45a795", "#a077d1", "#d1798f", "#8f9cad"]
    };

    public static ChartPalette For(bool dark) => dark ? DarkTheme : Light;

    /// <summary>
    /// Both themes as CSS custom properties, for a &lt;style&gt; block in the head. Dark is an
    /// explicit choice stamped on &lt;html&gt; by theme.js, not a system preference, so the
    /// override follows the attribute rather than a media query.
    /// </summary>
    public static string CssTokens() =>
        Light.CssBlock(":root") + Environment.NewLine + Environment.NewLine +
        DarkTheme.CssBlock(":root[data-theme=\"dark\"]");

    private string CssBlock(string selector) => $$"""
        {{selector}} {
          color-scheme: {{(Dark ? "dark" : "light")}};

          --page: {{Page}};
          --surface-1: {{Surface}};
          --text-primary: {{TextPrimary}};
          --text-secondary: {{Secondary}};
          --text-muted: {{Muted}};
          --grid: {{Grid}};
          --axis: {{Axis}};
          --border: {{Border}};
          --hover-wash: {{HoverWash}};

          --series-1: {{Income}};
          --series-1-strong: {{IncomeStrong}};
          --danger: {{Expense}};

          --color-income: var(--series-1);
          --color-expense: var(--danger);
        }
        """;

    public const string CurrencyFormatter =
        "function (v) { return new Intl.NumberFormat('pt-BR', { style: 'currency', currency: 'BRL' }).format(v); }";

    public const string CompactCurrencyFormatter =
        "function (v) { return new Intl.NumberFormat('pt-BR', { style: 'currency', currency: 'BRL', notation: 'compact', maximumFractionDigits: 1 }).format(v); }";

    /// <summary>Shared chrome: no toolbar, no animation on a page that re-renders, theme-aware text and grid.</summary>
    public ApexChartOptions<T> BaseOptions<T>() where T : class => new()
    {
        Chart = new Chart
        {
            Toolbar = new Toolbar { Show = false },
            Background = "transparent",
            Animations = new Animations { Enabled = false },
            FontFamily = "inherit",
            ForeColor = Secondary
        },
        Theme = new Theme { Mode = Dark ? Mode.Dark : Mode.Light },
        Grid = new Grid
        {
            BorderColor = Grid,
            Xaxis = new GridXAxis { Lines = new Lines { Show = false } }
        },
        DataLabels = new DataLabels { Enabled = false },
        Tooltip = new Tooltip
        {
            Theme = Dark ? Mode.Dark : Mode.Light,
            Y = new TooltipY { Formatter = CurrencyFormatter }
        },
        States = new States
        {
            Hover = new StatesHover { Filter = new StatesFilter { Type = StatesFilterType.none } },
            Active = new StatesActive { Filter = new StatesFilter { Type = StatesFilterType.none } }
        }
    };
}

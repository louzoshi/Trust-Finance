using System.Text.RegularExpressions;

namespace TrustFinance.IntegrationTests.Infrastructure;

/// <summary>Just enough HTML poking to assert on a server-rendered page.</summary>
public static partial class Html
{
    /// <summary>The antiforgery token a static form posts back.</summary>
    public static string? AntiforgeryToken(string html) =>
        TokenPattern().Match(html) is { Success: true } m ? m.Groups[1].Value : null;

    /// <summary>The page's visible text, collapsed, for "does it say this" assertions.</summary>
    public static string Text(string html)
    {
        var withoutScripts = ScriptPattern().Replace(html, " ");
        var withoutTags = TagPattern().Replace(withoutScripts, " ");
        return WhitespacePattern().Replace(System.Net.WebUtility.HtmlDecode(withoutTags), " ").Trim();
    }

    [GeneratedRegex("""name="__RequestVerificationToken"[^>]*value="([^"]+)""")]
    private static partial Regex TokenPattern();

    [GeneratedRegex("<script.*?</script>", RegexOptions.Singleline)]
    private static partial Regex ScriptPattern();

    [GeneratedRegex("<[^>]+>")]
    private static partial Regex TagPattern();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespacePattern();
}

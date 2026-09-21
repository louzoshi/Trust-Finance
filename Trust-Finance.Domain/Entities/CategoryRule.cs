using TrustFinance.Domain.Notifications;

namespace TrustFinance.Domain.Entities;

/// <summary>
/// "When the statement line contains this, file it here." Learned from what the user
/// picked on an import, so the second statement from the same bank arrives mostly
/// categorized. Matching is a case-insensitive contains on the normalized memo.
/// </summary>
public class CategoryRule : Notifiable
{
    public const int MaxPatternLength = 80;

    private CategoryRule() { }

    public CategoryRule(string pattern, int categoryId, int userId)
    {
        var normalized = Normalize(pattern);

        AddNotificationIf(normalized.Length < 3 || normalized.Length > MaxPatternLength, nameof(Pattern),
            $"O padrão deve ter entre 3 e {MaxPatternLength} caracteres");
        AddNotificationIf(categoryId <= 0, nameof(CategoryId), "Escolha uma categoria");
        AddNotificationIf(userId <= 0, nameof(UserId), "Regra sem dona");

        Pattern = normalized;
        CategoryId = categoryId;
        UserId = userId;
    }

    public int Id { get; private set; }
    public string Pattern { get; private set; } = string.Empty;

    public int CategoryId { get; private set; }
    public Category Category { get; private set; } = null!;

    public int UserId { get; private set; }
    public User User { get; private set; } = null!;

    public bool Matches(string memo) => Normalize(memo).Contains(Pattern, StringComparison.Ordinal);

    /// <summary>
    /// Upper-case, single-spaced, with digit runs removed: "PIX ENVIADO 12/03 MERCADO
    /// SILVA 0001" and "PIX ENVIADO 15/03 MERCADO SILVA 0002" are the same merchant.
    /// </summary>
    public static string Normalize(string? memo)
    {
        if (string.IsNullOrWhiteSpace(memo))
            return string.Empty;

        var chars = memo.ToUpperInvariant()
            .Where(c => !char.IsDigit(c) && c is not ('/' or '-' or '.' or ':' or '*' or '#'))
            .ToArray();

        return string.Join(' ', new string(chars).Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }
}

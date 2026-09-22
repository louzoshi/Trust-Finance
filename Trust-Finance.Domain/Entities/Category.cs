using TrustFinance.Domain.Notifications;

namespace TrustFinance.Domain.Entities;

/// <summary>
/// A grouping for transactions. The slug is derived, never supplied: it exists to make
/// "Mercado" and "mercado " collide for the same user, and a caller free to set it could
/// defeat that just by passing something else.
/// </summary>
public class Category : Notifiable, IAuditable
{
    public const int MinNameLength = 2;
    public const int MaxNameLength = 40;

    private Category() { }

    public Category(string name, int userId)
    {
        Rename(name);

        AddNotificationIf(userId <= 0, nameof(UserId), "Categoria sem dono");
        UserId = userId;
    }

    public int Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Slug { get; private set; } = string.Empty;

    public int UserId { get; private set; }
    public User User { get; private set; } = null!;

    public ICollection<Transaction> Transactions { get; private set; } = new List<Transaction>();

    public void Rename(string name)
    {
        var trimmed = (name ?? string.Empty).Trim();

        AddNotificationIf(trimmed.Length is < MinNameLength or > MaxNameLength, nameof(Name),
            $"O nome deve ter entre {MinNameLength} e {MaxNameLength} caracteres");

        if (IsInvalid)
            return;

        Name = trimmed;
        Slug = ValueObjects.Slug.From(trimmed);
    }
}

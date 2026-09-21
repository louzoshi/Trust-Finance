using TrustFinance.Domain.Notifications;

namespace TrustFinance.Domain.Entities;

/// <summary>A monthly spending ceiling for one category.</summary>
public class Budget : Notifiable
{
    private Budget() { }

    public Budget(int categoryId, decimal monthlyLimit, int userId)
    {
        AddNotificationIf(monthlyLimit <= 0, nameof(MonthlyLimit), "O limite precisa ser maior que zero");
        AddNotificationIf(categoryId <= 0, nameof(CategoryId), "Escolha uma categoria");
        AddNotificationIf(userId <= 0, nameof(UserId), "Orçamento sem dono");

        CategoryId = categoryId;
        MonthlyLimit = monthlyLimit;
        UserId = userId;
    }

    public int Id { get; private set; }
    public decimal MonthlyLimit { get; private set; }

    public int CategoryId { get; private set; }
    public Category Category { get; private set; } = null!;

    public int UserId { get; private set; }
    public User User { get; private set; } = null!;

    public void ChangeLimitTo(decimal monthlyLimit)
    {
        AddNotificationIf(monthlyLimit <= 0, nameof(MonthlyLimit), "O limite precisa ser maior que zero");

        if (IsValid)
            MonthlyLimit = monthlyLimit;
    }
}

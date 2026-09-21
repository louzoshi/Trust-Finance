namespace TrustFinance.App.Forms;

/// <summary>
/// What the categories screen collects. The name rules live on
/// <see cref="Domain.Entities.Category"/>.
/// </summary>
public class CategoryForm
{
    public string Name { get; set; } = string.Empty;
}

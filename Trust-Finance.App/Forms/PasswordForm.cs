using System.ComponentModel.DataAnnotations;

namespace TrustFinance.App.Forms;

public class PasswordForm
{
    [Required(ErrorMessage = "Informe a senha atual")]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "Informe a nova senha")]
    [StringLength(64, MinimumLength = 8, ErrorMessage = "A senha deve ter entre 8 e 64 caracteres")]
    public string NewPassword { get; set; } = string.Empty;

    // Checked here rather than in the service: a typed-twice confirmation is a property of
    // the form, not of the account. The service never sees this field.
    [Compare(nameof(NewPassword), ErrorMessage = "A confirmação não confere com a nova senha")]
    public string Confirmation { get; set; } = string.Empty;
}

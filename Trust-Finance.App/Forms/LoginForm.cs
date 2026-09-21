using System.ComponentModel.DataAnnotations;

namespace TrustFinance.App.Forms;

public class LoginForm
{
    [Required(ErrorMessage = "Informe o e-mail")]
    [EmailAddress(ErrorMessage = "E-mail inválido")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Informe a senha")]
    public string Password { get; set; } = string.Empty;
}

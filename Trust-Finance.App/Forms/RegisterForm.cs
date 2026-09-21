using System.ComponentModel.DataAnnotations;

namespace TrustFinance.App.Forms;

public class RegisterForm
{
    [Required(ErrorMessage = "Informe seu nome")]
    [StringLength(40, MinimumLength = 3, ErrorMessage = "O nome deve ter entre 3 e 40 caracteres")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Informe o e-mail")]
    [EmailAddress(ErrorMessage = "E-mail inválido")]
    [StringLength(100, ErrorMessage = "O e-mail deve ter no máximo 100 caracteres")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Informe uma senha")]
    [StringLength(64, MinimumLength = 8, ErrorMessage = "A senha deve ter entre 8 e 64 caracteres")]
    public string Password { get; set; } = string.Empty;
}

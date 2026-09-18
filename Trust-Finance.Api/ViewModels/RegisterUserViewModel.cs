using System.ComponentModel.DataAnnotations;

namespace TF.ViewModels
{
    public class RegisterUserViewModel
    {
        [Required(ErrorMessage = "Name is required")]
        [StringLength(40, MinimumLength = 3, ErrorMessage = "Name must be between 3 and 40 characters")]
        public required string Name { get; set; }

        [Required(ErrorMessage = "Email is required")]
        [EmailAddress(ErrorMessage = "Invalid email")]
        public required string Email { get; set; }

        [Required(ErrorMessage = "Password is required")]
        [StringLength(20, MinimumLength = 6, ErrorMessage = "Password must be between 6 and 20 characters")]
        public required string Password { get; set; }

        // Image and Slug are not asked for. An avatar is not something a person
        // should have to supply a URL for to open an account, and the slug is
        // derived from the name in AccountService.
    }
}

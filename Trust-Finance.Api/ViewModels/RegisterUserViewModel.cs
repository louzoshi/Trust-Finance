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

        [Required(ErrorMessage = "Image is required")]
        [Url(ErrorMessage = "Image must be a valid URL")]
        public required string Image { get; set; }

        [Required(ErrorMessage = "Slug is required")]
        public required string Slug { get; set; }
    }
}

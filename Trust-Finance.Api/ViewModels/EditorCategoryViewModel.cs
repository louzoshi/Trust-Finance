using System.ComponentModel.DataAnnotations;

namespace TF.ViewModels
{
    public class EditorCategoryViewModel
    {
        [Required(ErrorMessage = "Name is required")]
        [StringLength(40, MinimumLength = 3, ErrorMessage = "Name must be between 3 and 40 characters")]
        public required string Name { get; set; }

        [Required(ErrorMessage = "Slug is required")]
        public required string Slug { get; set; }
    }
}

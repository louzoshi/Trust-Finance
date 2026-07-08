using System.ComponentModel.DataAnnotations;

namespace TF.ViewModels
{
    public class EditorTransactionViewModel
    {
        [Required(ErrorMessage = "Description is required")]
        [StringLength(100, ErrorMessage = "Description must be between 3 and 100 characters", MinimumLength = 3)]
        public string Description { get; set; } = string.Empty;

        [Required(ErrorMessage = "Amount is required")]
        [Range(0.01, 9999999999.99, ErrorMessage = "Invalid amount")]
        public decimal Amount { get; set; }

        [Required(ErrorMessage = "Date is required")]
        public DateTime Date { get; set; }

        [Required(ErrorMessage = "Category is required")]
        public int CategoryId { get; set; }
    }
}

using System.ComponentModel.DataAnnotations;
using TF.Models;

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

        // Income = 1, Expense = 2, so an omitted type arrives as 0 and is rejected
        // here rather than defaulting to one of them.
        [EnumDataType(typeof(TransactionType), ErrorMessage = "Type must be Income or Expense")]
        public TransactionType Type { get; set; }

        [Required(ErrorMessage = "Category is required")]
        public int CategoryId { get; set; }
    }
}

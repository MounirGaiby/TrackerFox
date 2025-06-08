using System.ComponentModel.DataAnnotations;

namespace PersonalFinanceTracker.Models
{
    public enum TransactionType
    {
        Income = 1,
        Expense = 2
    }

    public class Transaction
    {
        public int Id { get; set; }
        
        [Required]
        [Range(0.01, double.MaxValue, ErrorMessage = "Amount must be greater than 0")]
        public decimal Amount { get; set; }
        
        [Required]
        [StringLength(200)]
        public string Description { get; set; } = string.Empty;
        
        public TransactionType Type { get; set; }
        
        public DateTime Date { get; set; } = DateTime.Today;

        [StringLength(500)]
        public string? Notes { get; set; }
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        
        // Foreign Keys
        public int AccountId { get; set; }
        public Account Account { get; set; } = null!;
        
        public int CategoryId { get; set; }
        public Category Category { get; set; } = null!;
    }
}

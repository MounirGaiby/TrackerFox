using System.ComponentModel.DataAnnotations;

namespace PersonalFinanceTracker.Models
{
    public enum AccountType
    {
        Checking = 1,
        Savings = 2,
        CreditCard = 3,
        Investment = 4,
        Other = 5
    }

    public class Account
    {
        public int Id { get; set; }
        
        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;
        
        public AccountType Type { get; set; }
        
        [Range(typeof(decimal), "-999999999", "999999999")]
        public decimal Balance { get; set; }

        [StringLength(500)]
        public string? Description { get; set; }
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        
        // Foreign Key
        public string UserId { get; set; } = string.Empty;
        public User User { get; set; } = null!;
        
        public ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();
    }
}

using PersonalFinanceTracker.Models;
using System.ComponentModel.DataAnnotations;

namespace PersonalFinanceTracker.ViewModels
{
    public class AccountViewModel
    {
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        [Required]
        public AccountType Type { get; set; }

        [Range(typeof(decimal), "-999999999", "999999999")]
        public decimal Balance { get; set; }

        public DateTime CreatedAt { get; set; }
        
        [StringLength(500)]
        public string? Description { get; set; } = string.Empty;
    }

    public class CreateAccountViewModel
    {
        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        [Required]
        public string Type { get; set; } = string.Empty;

        [Range(typeof(decimal), "0", "999999999")]
        [Display(Name = "Initial Balance")]
        public decimal InitialBalance { get; set; }

        [StringLength(500)]
        public string? Description { get; set; }
    }

    public class EditAccountViewModel
    {
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        [Required]
        public string Type { get; set; } = string.Empty;

        [StringLength(500)]
        public string? Description { get; set; }

        public decimal CurrentBalance { get; set; }
    }
}

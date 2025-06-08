using PersonalFinanceTracker.Models;
using System.ComponentModel.DataAnnotations;

namespace PersonalFinanceTracker.ViewModels
{
    public class TransactionViewModel
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

        [Required]
        [Display(Name = "Account")]
        public int AccountId { get; set; }

        [Required]
        [Display(Name = "Category")]
        public int CategoryId { get; set; }

        public string? AccountName { get; set; }
        public string? CategoryName { get; set; }
        public DateTime CreatedAt { get; set; }
        
        [StringLength(500)]
        public string? Notes { get; set; }
        
        // Navigation properties for views
        public Account? Account { get; set; }
        public Category? Category { get; set; }
    }

    public class CreateTransactionViewModel
    {
        [Required]
        [Range(0.01, double.MaxValue, ErrorMessage = "Amount must be greater than 0")]
        public decimal Amount { get; set; }

        [Required]
        [StringLength(200)]
        public string Description { get; set; } = string.Empty;

        [Required]
        public string Type { get; set; } = string.Empty;

        public DateTime Date { get; set; } = DateTime.Today;

        [Required]
        [Display(Name = "Account")]
        public int AccountId { get; set; }

        [Required]
        [Display(Name = "Category")]
        public int CategoryId { get; set; }

        [StringLength(500)]
        public string? Notes { get; set; }

        public List<Account> Accounts { get; set; } = new List<Account>();
        public List<Category> Categories { get; set; } = new List<Category>();
    }

    public class EditTransactionViewModel
    {
        public int Id { get; set; }

        [Required]
        [Range(0.01, double.MaxValue, ErrorMessage = "Amount must be greater than 0")]
        public decimal Amount { get; set; }

        [Required]
        [StringLength(200)]
        public string Description { get; set; } = string.Empty;

        [Required]
        public string Type { get; set; } = string.Empty;

        public DateTime Date { get; set; } = DateTime.Today;

        [Required]
        [Display(Name = "Account")]
        public int AccountId { get; set; }

        [Required]
        [Display(Name = "Category")]
        public int CategoryId { get; set; }

        [StringLength(500)]
        public string? Notes { get; set; }

        public List<Account> Accounts { get; set; } = new List<Account>();
        public List<Category> Categories { get; set; } = new List<Category>();
    }

    public class TransactionListViewModel
    {
        public List<TransactionViewModel> Transactions { get; set; } = new List<TransactionViewModel>();
        public List<Account> Accounts { get; set; } = new List<Account>();
        public List<Category> Categories { get; set; } = new List<Category>();
        
        // Filter properties
        public int? SelectedAccountId { get; set; }
        public int? SelectedCategoryId { get; set; }
        public string? SelectedType { get; set; }
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
        public string? SearchTerm { get; set; }
        public decimal? MinAmount { get; set; }
        public decimal? MaxAmount { get; set; }

        // Pagination properties
        public int CurrentPage { get; set; } = 1;
        public int PageSize { get; set; } = 20;
        public int TotalRecords { get; set; }
        public int TotalPages => (int)Math.Ceiling((double)TotalRecords / PageSize);
        public bool HasPreviousPage => CurrentPage > 1;
        public bool HasNextPage => CurrentPage < TotalPages;

        // Sorting properties
        public string? SortBy { get; set; }
        public string? SortDirection { get; set; } = "desc";

        // Summary properties
        public decimal TotalIncome { get; set; }
        public decimal TotalExpenses { get; set; }
        public decimal FilteredIncome { get; set; }
        public decimal FilteredExpenses { get; set; }
    }
}

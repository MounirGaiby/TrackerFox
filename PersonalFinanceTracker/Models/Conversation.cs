using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PersonalFinanceTracker.Models
{
    public class Conversation
    {
        public int Id { get; set; }
        
        [Required]
        public string UserId { get; set; } = string.Empty;
        
        [Required]
        [StringLength(200)]
        public string Title { get; set; } = string.Empty;
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public DateTime LastMessageAt { get; set; } = DateTime.UtcNow;
        
        public bool IsActive { get; set; } = true;
        
        public int TokensUsed { get; set; } = 0;
        public int MaxTokens { get; set; } = 8000;
        
        // Static properties for token management
        public static int TokenWarningThreshold { get; } = 7000;
        
        // Instance property for total tokens
        public int TotalTokens => TokensUsed;
        
        // Navigation properties
        public User User { get; set; } = null!;
        public ICollection<ChatMessage> Messages { get; set; } = new List<ChatMessage>();
    }

    public class FinancialCommand
    {
        public string Command { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Usage { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
    }

    public class ChatContext
    {
        public bool IncludeAccounts { get; set; } = true;
        public bool IncludeTransactions { get; set; } = true;
        public bool IncludeSpendingCategories { get; set; } = true;
        public bool IncludeInvestments { get; set; } = false;
        public bool IncludeDebts { get; set; } = false;
        public int TransactionDays { get; set; } = 30;
        public string[] SelectedCategories { get; set; } = Array.Empty<string>();
    }

    public class ChatResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public ChatMessage? ChatMessage { get; set; }
        public string? Error { get; set; }
        public List<FinancialInsight>? Insights { get; set; }
        public Dictionary<string, object>? Data { get; set; }
    }

    public class FinancialInsight
    {
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public decimal? Value { get; set; }
        public string? Recommendation { get; set; }
        public string? Icon { get; set; }
        public string? Color { get; set; }
    }
}

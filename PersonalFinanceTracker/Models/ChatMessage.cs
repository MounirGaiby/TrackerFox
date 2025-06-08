using System.ComponentModel.DataAnnotations;

namespace PersonalFinanceTracker.Models
{
    public enum MessageRole
    {
        User,
        Assistant,
        System
    }
    
    public enum ContextType
    {
        General,
        Accounts,
        Transactions,
        Spending,
        Budget,
        Savings,
        Investment
    }

    public class ChatMessage
    {
        public int Id { get; set; }
        
        public int ConversationId { get; set; }
        
        public Conversation Conversation { get; set; } = null!;
        
        public MessageRole Role { get; set; }
        
        [Required]
        public string Content { get; set; } = string.Empty;
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
        public int TokensUsed { get; set; }
        
        // Store context type for this message (e.g., accounts, transactions, etc.)
        public ContextType? ContextType { get; set; }
        
        // Store additional metadata as JSON
        public string? Metadata { get; set; }
    }
}

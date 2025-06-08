using PersonalFinanceTracker.Models;

namespace PersonalFinanceTracker.ViewModels
{
    // New streamlined Financial AI Bot ViewModels
    public class FinanceAIBotViewModel
    {
        public string UserName { get; set; } = string.Empty;
        public string WelcomeMessage { get; set; } = string.Empty;
        public List<FinancialKeyword> AvailableKeywords { get; set; } = new List<FinancialKeyword>();
        public List<QuickAction> QuickActions { get; set; } = new List<QuickAction>();
    }

    public class FinancialKeyword
    {
        public string Keyword { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Example { get; set; } = string.Empty;
    }

    public class QuickAction
    {
        public string Text { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty;
    }

    public class BotChatRequest
    {
        public string Message { get; set; } = string.Empty;
        public int? ConversationId { get; set; }
    }

    public class BotChatResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string? Error { get; set; }
        public Dictionary<string, object>? Data { get; set; }
        public int TokensUsed { get; set; }
        public bool RequiresNewConversation { get; set; }
    }

    public class ProcessedChatRequest
    {
        public string OriginalMessage { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public Models.ContextType ContextType { get; set; }
        public bool HasKeywords { get; set; }
        public FinancialContext FinancialContext { get; set; } = new FinancialContext();
    }

    public class BotAIResponse
    {
        public string Message { get; set; } = string.Empty;
        public int TokensUsed { get; set; }
        public bool RequiresNewConversation { get; set; }
        public Dictionary<string, object>? Data { get; set; }
    }

    public class FinancialContext
    {
        public BasicFinancialSummary? BasicSummary { get; set; }
        public List<Account>? Accounts { get; set; }
        public List<Transaction>? RecentTransactions { get; set; }
        public List<Transaction>? SpendingData { get; set; }
        public List<BudgetGoalModel>? BudgetGoals { get; set; }
        public List<Transaction>? CurrentMonthExpenses { get; set; }
    }

    public class BasicFinancialSummary
    {
        public decimal TotalBalance { get; set; }
        public decimal MonthlyIncome { get; set; }
        public decimal MonthlyExpenses { get; set; }
        public int AccountCount { get; set; }
    }

    // New streamlined Financial AI Assistant ViewModel
    public class FinancialAIAssistantViewModel
    {
        public string UserName { get; set; } = string.Empty;
        public int ConversationId { get; set; }
        public string ConversationTitle { get; set; } = string.Empty;
        public bool IsNewConversation { get; set; }
        public int TokensUsed { get; set; }
        public int TokensRemaining { get; set; }
        public bool IsNearTokenLimit { get; set; }
        public List<ChatMessage> Messages { get; set; } = new List<ChatMessage>();
        public string WelcomeMessage { get; set; } = string.Empty;
        public List<FinancialKeyword> AvailableKeywords { get; set; } = new List<FinancialKeyword>();
        public List<QuickAction> QuickActions { get; set; } = new List<QuickAction>();
    }
}

using PersonalFinanceTracker.Data;
using PersonalFinanceTracker.Models;
using PersonalFinanceTracker.ViewModels;
using Microsoft.EntityFrameworkCore;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace PersonalFinanceTracker.Services
{
    public interface IFinancialAIService
    {
        Task<AIResponse> ProcessChatMessageAsync(string message, string userId, int? conversationId = null);
        Task<Conversation> CreateNewConversationAsync(string userId, string title = "");
        Task<List<Conversation>> GetUserConversationsAsync(string userId, int limit = 10);
        Task<bool> CanContinueConversationAsync(int conversationId);
        Task<Conversation> GetOrCreateActiveConversationAsync(string userId);
        Task<Conversation> StartNewConversationAsync(string userId);
        Task<Conversation?> LoadConversationAsync(string userId, int conversationId);
    }

    public class FinancialAIService : IFinancialAIService
    {
        private readonly ApplicationDbContext _context;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;
        private readonly ILogger<FinancialAIService> _logger;

        private const int MAX_TOKENS_PER_CONVERSATION = 8000;
        private const int TOKEN_WARNING_THRESHOLD = 7000;

        public FinancialAIService(
            ApplicationDbContext context,
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration,
            ILogger<FinancialAIService> logger)
        {
            _context = context;
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<AIResponse> ProcessChatMessageAsync(string message, string userId, int? conversationId = null)
        {
            try
            {
                // Get or create conversation
                var conversation = conversationId.HasValue 
                    ? await _context.Conversations
                        .Include(c => c.Messages)
                        .FirstOrDefaultAsync(c => c.Id == conversationId.Value && c.UserId == userId)
                    : await CreateNewConversationAsync(userId, GenerateConversationTitle(message));

                if (conversation == null)
                {
                    return new AIResponse { Success = false, Error = "Conversation not found" };
                }

                // Check token limits
                if (conversation.TokensUsed >= MAX_TOKENS_PER_CONVERSATION)
                {
                    return new AIResponse 
                    { 
                        Success = false, 
                        RequiresNewConversation = true,
                        Message = "This conversation has reached its token limit. Please start a new conversation to continue.",
                        TokensUsed = conversation.TokensUsed
                    };
                }

                // Process user message
                var contextType = DetermineContextType(message);
                var financialContext = await GatherFinancialContextAsync(message, userId, contextType);
                
                // Save user message
                var userMessage = new ChatMessage
                {
                    ConversationId = conversation.Id,
                    Role = MessageRole.User,
                    Content = message,
                    ContextType = contextType,
                    TokensUsed = EstimateTokens(message)
                };
                _context.ChatMessages.Add(userMessage);

                // Generate AI response
                var aiResponseText = await GenerateAIResponseAsync(message, financialContext, conversation.Messages.ToList(), userId);
                var aiTokens = EstimateTokens(aiResponseText);

                // Save AI message
                var aiMessage = new ChatMessage
                {
                    ConversationId = conversation.Id,
                    Role = MessageRole.Assistant,
                    Content = aiResponseText,
                    TokensUsed = aiTokens
                };
                _context.ChatMessages.Add(aiMessage);

                // Update conversation
                conversation.TokensUsed += userMessage.TokensUsed + aiTokens;
                conversation.LastMessageAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                var warningMessage = conversation.TokensUsed > TOKEN_WARNING_THRESHOLD 
                    ? $" (Note: This conversation is approaching its token limit. {MAX_TOKENS_PER_CONVERSATION - conversation.TokensUsed} tokens remaining)"
                    : string.Empty;

                return new AIResponse
                {
                    Success = true,
                    Message = aiResponseText + warningMessage,
                    ConversationId = conversation.Id,
                    ConversationTitle = conversation.Title,
                    TokensUsed = userMessage.TokensUsed + aiTokens,
                    TotalTokens = conversation.TokensUsed,
                    TokensRemaining = Math.Max(0, MAX_TOKENS_PER_CONVERSATION - conversation.TokensUsed),
                    IsNearTokenLimit = conversation.TokensUsed >= TOKEN_WARNING_THRESHOLD,
                    RequiresNewConversation = conversation.TokensUsed >= MAX_TOKENS_PER_CONVERSATION,
                    ContextType = contextType.ToString(),
                    Timestamp = DateTime.UtcNow,
                    Data = FormatContextData(financialContext)
                };
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("AI") || ex.Message.Contains("unavailable"))
            {
                _logger.LogWarning(ex, "AI service temporarily unavailable for user {UserId}", userId);
                return new AIResponse 
                { 
                    Success = false, 
                    Error = "🤖 Our AI Assistant is currently down for maintenance. Please try again in a few minutes."
                };
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "AI service communication error for user {UserId}", userId);
                return new AIResponse 
                { 
                    Success = false, 
                    Error = "🔧 AI Assistant is experiencing connectivity issues. Please retry your request in a moment."
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error processing chat message for user {UserId}", userId);
                return new AIResponse 
                { 
                    Success = false, 
                    Error = "⚠️ Something went wrong while processing your message. Please try again later."
                };
            }
        }

        public async Task<Conversation> CreateNewConversationAsync(string userId, string title = "")
        {
            if (string.IsNullOrEmpty(title))
            {
                title = $"Chat {DateTime.Now:MMM dd, HH:mm}";
            }

            var conversation = new Conversation
            {
                UserId = userId,
                Title = title.Length > 200 ? title[..200] : title,
                CreatedAt = DateTime.UtcNow,
                LastMessageAt = DateTime.UtcNow,
                IsActive = true,
                TokensUsed = 0,
                MaxTokens = MAX_TOKENS_PER_CONVERSATION
            };

            _context.Conversations.Add(conversation);
            await _context.SaveChangesAsync();
            return conversation;
        }

        public async Task<List<Conversation>> GetUserConversationsAsync(string userId, int limit = 10)
        {
            return await _context.Conversations
                .Where(c => c.UserId == userId)
                .OrderByDescending(c => c.LastMessageAt)
                .Take(limit)
                .ToListAsync();
        }

        public async Task<bool> CanContinueConversationAsync(int conversationId)
        {
            var conversation = await _context.Conversations.FindAsync(conversationId);
            return conversation != null && conversation.TokensUsed < MAX_TOKENS_PER_CONVERSATION;
        }

        public async Task<Conversation> GetOrCreateActiveConversationAsync(string userId)
        {
            var activeConversation = await _context.Conversations
                .Include(c => c.Messages)
                .Where(c => c.UserId == userId && c.IsActive)
                .OrderByDescending(c => c.LastMessageAt)
                .FirstOrDefaultAsync();

            if (activeConversation == null || activeConversation.TokensUsed >= MAX_TOKENS_PER_CONVERSATION)
            {
                return await CreateNewConversationAsync(userId, "New Conversation");
            }

            return activeConversation;
        }

        public async Task<Conversation> StartNewConversationAsync(string userId)
        {
            return await CreateNewConversationAsync(userId, "New Conversation");
        }

        public async Task<Conversation?> LoadConversationAsync(string userId, int conversationId)
        {
            return await _context.Conversations
                .Include(c => c.Messages)
                .FirstOrDefaultAsync(c => c.Id == conversationId && c.UserId == userId);
        }

        private Models.ContextType DetermineContextType(string message)
        {
            var lowerMessage = message.ToLower();

            if (lowerMessage.Contains("@accounts") || lowerMessage.Contains("@account"))
                return Models.ContextType.Accounts;
            if (lowerMessage.Contains("@transactions") || lowerMessage.Contains("@transaction"))
                return Models.ContextType.Transactions;
            if (lowerMessage.Contains("#spending") || lowerMessage.Contains("#expenses"))
                return Models.ContextType.Spending;
            if (lowerMessage.Contains("#savings") || lowerMessage.Contains("#save"))
                return Models.ContextType.Savings;
            if (lowerMessage.Contains("#investment") || lowerMessage.Contains("#investing"))
                return Models.ContextType.Investment;

            return Models.ContextType.General;
        }

        private async Task<FinancialContext> GatherFinancialContextAsync(string message, string userId, Models.ContextType contextType)
        {
            var context = new FinancialContext();
            var lowerMessage = message.ToLower();

            // Always include basic summary
            context.BasicSummary = await CalculateBasicSummaryAsync(userId);

            // Gather specific context based on keywords
            if (contextType == ContextType.Accounts || lowerMessage.Contains("@account"))
            {
                context.Accounts = await _context.Accounts
                    .Where(a => a.UserId == userId)
                    .ToListAsync();
            }

            if (contextType == ContextType.Transactions || lowerMessage.Contains("@transaction"))
            {
                var days = ExtractDaysFromMessage(message);
                var startDate = DateTime.Now.AddDays(-days);
                
                context.RecentTransactions = await _context.Transactions
                    .Include(t => t.Account)
                    .Include(t => t.Category)
                    .Where(t => t.Account.UserId == userId && t.Date >= startDate)
                    .OrderByDescending(t => t.Date)
                    .Take(50)
                    .ToListAsync();
            }

            if (contextType == ContextType.Spending || lowerMessage.Contains("#spending"))
            {
                var category = ExtractCategoryFromMessage(message);
                var lastMonth = DateTime.Now.AddMonths(-1);
                
                var query = _context.Transactions
                    .Include(t => t.Account)
                    .Include(t => t.Category)
                    .Where(t => t.Account.UserId == userId && 
                               t.Type == TransactionType.Expense && 
                               t.Date >= lastMonth);

                if (!string.IsNullOrEmpty(category))
                {
                    query = query.Where(t => t.Category.Name.ToLower().Contains(category.ToLower()));
                }

                context.SpendingData = await query.ToListAsync();
            }

            return context;
        }

        private async Task<BasicFinancialSummary> CalculateBasicSummaryAsync(string userId)
        {
            var accounts = await _context.Accounts
                .Where(a => a.UserId == userId)
                .ToListAsync();

            var lastMonth = DateTime.Now.AddMonths(-1);
            var transactions = await _context.Transactions
                .Include(t => t.Account)
                .Where(t => t.Account.UserId == userId && t.Date >= lastMonth)
                .ToListAsync();

            return new BasicFinancialSummary
            {
                TotalBalance = accounts.Sum(a => a.Balance),
                MonthlyIncome = transactions.Where(t => t.Type == TransactionType.Income).Sum(t => t.Amount),
                MonthlyExpenses = transactions.Where(t => t.Type == TransactionType.Expense).Sum(t => t.Amount),
                AccountCount = accounts.Count
            };
        }

        private async Task<string> GenerateAIResponseAsync(string userMessage, FinancialContext context, List<ChatMessage> conversationHistory, string userId)
        {
            try
            {
                var user = await _context.Users.FindAsync(userId);
                var userName = user?.UserName ?? "User";
                
                var systemPrompt = GenerateSystemPrompt(context, userName);
                
                // Build conversation history for context
                var messages = new List<object>
                {
                    new { role = "system", content = systemPrompt }
                };

                // Add recent conversation history (last 10 messages to save tokens)
                foreach (var msg in conversationHistory.TakeLast(10))
                {
                    messages.Add(new { 
                        role = msg.Role == MessageRole.User ? "user" : "assistant", 
                        content = msg.Content 
                    });
                }

                // Add current user message
                messages.Add(new { role = "user", content = userMessage });

                var openRouterApiKey = _configuration["OpenRouter:ApiKey"] ?? "DEMO_API_KEY";
                var httpClient = _httpClientFactory.CreateClient();
                httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", openRouterApiKey);

                var requestBody = new
                {
                    model = "google/gemini-2.0-flash-exp:free",
                    messages = messages,
                    temperature = 0.7,
                    max_tokens = 500  // Keep responses concise
                };

                var content = new StringContent(
                    JsonSerializer.Serialize(requestBody),
                    Encoding.UTF8,
                    "application/json");

                var response = await httpClient.PostAsync("https://openrouter.ai/api/v1/chat/completions", content);

                if (response.IsSuccessStatusCode)
                {
                    string responseBody = await response.Content.ReadAsStringAsync();
                    _logger.LogInformation("AI API Response: {Response}", responseBody);
                    
                    var responseObject = JsonSerializer.Deserialize<OpenRouterResponse>(responseBody);
                    var aiResponse = responseObject?.Choices?.FirstOrDefault()?.Message?.Content;

                    if (string.IsNullOrEmpty(aiResponse))
                    {
                        _logger.LogWarning("AI API returned empty response content");
                        throw new InvalidOperationException("AI service returned empty response");
                    }

                    return aiResponse;
                }
                else
                {
                    string errorBody = await response.Content.ReadAsStringAsync();
                    _logger.LogError("OpenRouter API error: {StatusCode}, Response: {Response}", response.StatusCode, errorBody);
                    throw new HttpRequestException($"AI service returned {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calling OpenRouter API");
                throw new InvalidOperationException("AI service is currently unavailable", ex);
            }
        }

        private string GenerateSystemPrompt(FinancialContext context, string userName)
        {
            var prompt = new StringBuilder();
            
            prompt.AppendLine($"You are 'FinBot', a highly knowledgeable and friendly Financial AI Assistant helping {userName} with their personal finances.");
            prompt.AppendLine();
            prompt.AppendLine("CRITICAL RULES:");
            prompt.AppendLine("1. ONLY discuss financial topics: budgeting, saving, investing, expenses, debt management, financial planning, taxes, retirement, insurance, and related topics.");
            prompt.AppendLine("2. If asked about non-financial topics, politely redirect: 'I'm specialized in financial advice. Let's talk about your finances - how can I help you save, invest, or manage your money better?'");
            prompt.AppendLine("3. Keep responses concise (under 150 words) but helpful and actionable.");
            prompt.AppendLine("4. Always address the user personally and provide specific advice based on their data.");
            prompt.AppendLine("5. Be encouraging and supportive while being realistic about financial goals.");
            prompt.AppendLine();

            // Add user's financial context
            if (context.BasicSummary != null)
            {
                prompt.AppendLine($"{userName}'S FINANCIAL OVERVIEW:");
                prompt.AppendLine($"- Total Account Balance: ${context.BasicSummary.TotalBalance:N2}");
                prompt.AppendLine($"- Monthly Income: ${context.BasicSummary.MonthlyIncome:N2}");
                prompt.AppendLine($"- Monthly Expenses: ${context.BasicSummary.MonthlyExpenses:N2}");
                prompt.AppendLine($"- Number of Accounts: {context.BasicSummary.AccountCount}");
                
                if (context.BasicSummary.MonthlyIncome > 0)
                {
                    var savingsRate = ((context.BasicSummary.MonthlyIncome - context.BasicSummary.MonthlyExpenses) / context.BasicSummary.MonthlyIncome) * 100;
                    prompt.AppendLine($"- Current Savings Rate: {savingsRate:N1}%");
                }
                prompt.AppendLine();
            }

            // Add specific context based on user's query
            if (context.Accounts?.Any() == true)
            {
                prompt.AppendLine("ACCOUNT DETAILS:");
                foreach (var account in context.Accounts)
                {
                    prompt.AppendLine($"- {account.Name} ({account.Type}): ${account.Balance:N2}");
                }
                prompt.AppendLine();
            }

            if (context.SpendingData?.Any() == true)
            {
                var totalSpending = context.SpendingData.Sum(t => t.Amount);
                var categoryBreakdown = context.SpendingData
                    .GroupBy(t => t.Category.Name)
                    .Select(g => new { Category = g.Key, Amount = g.Sum(t => t.Amount) })
                    .OrderByDescending(x => x.Amount)
                    .Take(5);

                prompt.AppendLine("RECENT SPENDING ANALYSIS:");
                prompt.AppendLine($"- Total Spending: ${totalSpending:N2}");
                prompt.AppendLine("- Top Categories:");
                foreach (var item in categoryBreakdown)
                {
                    var percentage = (item.Amount / totalSpending) * 100;
                    prompt.AppendLine($"  • {item.Category}: ${item.Amount:N2} ({percentage:N1}%)");
                }
                prompt.AppendLine();
            }
        
            prompt.AppendLine("RESPONSE GUIDELINES:");
            prompt.AppendLine($"- Always address {userName} personally");
            prompt.AppendLine("- Provide specific, actionable advice based on their financial data");
            prompt.AppendLine("- Suggest practical next steps they can take");
            prompt.AppendLine("- Reference financial best practices (e.g., emergency fund, 50/30/20 rule)");
            prompt.AppendLine("- Be encouraging but realistic about their financial situation");
            prompt.AppendLine();
            prompt.AppendLine("AVAILABLE KEYWORDS for users:");
            prompt.AppendLine("- @accounts - Get account information");
            prompt.AppendLine("- @transactions [days] - Get recent transactions");
            prompt.AppendLine("- #spending [category] - Analyze spending");
            prompt.AppendLine("- #savings - Savings advice");
            prompt.AppendLine("- #investment - Investment guidance");

            return prompt.ToString();
        }

        private string GenerateConversationTitle(string message)
        {
            // Extract a meaningful title from the first message
            if (message.Length <= 50) return message;
            
            var words = message.Split(' ');
            var title = string.Join(" ", words.Take(8));
            return title.Length > 50 ? title[..47] + "..." : title;
        }

        private int EstimateTokens(string text)
        {
            // Rough estimation: 4 characters per token
            return (int)Math.Ceiling(text.Length / 4.0);
        }

        private int ExtractDaysFromMessage(string message)
        {
            var words = message.Split(' ');
            foreach (var word in words)
            {
                if (int.TryParse(word, out var days) && days > 0 && days <= 365)
                {
                    return days;
                }
            }
            return 30; // Default to 30 days
        }

        private string? ExtractCategoryFromMessage(string message)
        {
            var parts = message.Split(' ');
            var spendingIndex = -1;
            
            for (int i = 0; i < parts.Length; i++)
            {
                if (parts[i].ToLower().Contains("#spending") || parts[i].ToLower().Contains("#expenses"))
                {
                    spendingIndex = i;
                    break;
                }
            }

            if (spendingIndex >= 0 && spendingIndex + 1 < parts.Length)
            {
                return parts[spendingIndex + 1];
            }

            return null;
        }

        private Dictionary<string, object>? FormatContextData(FinancialContext context)
        {
            var data = new Dictionary<string, object>();

            if (context.BasicSummary != null)
            {
                data["summary"] = context.BasicSummary;
            }

            if (context.Accounts?.Any() == true)
            {
                data["accounts"] = context.Accounts.Select(a => new { a.Name, a.Type, a.Balance });
            }

            if (context.SpendingData?.Any() == true)
            {
                data["spending"] = context.SpendingData.GroupBy(t => t.Category.Name)
                    .Select(g => new { Category = g.Key, Amount = g.Sum(t => t.Amount) })
                    .OrderByDescending(x => x.Amount);
            }

            return data.Any() ? data : null;
        }
    }

    // Response models for the service
    public class AIResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string? Error { get; set; }
        public int ConversationId { get; set; }
        public string ConversationTitle { get; set; } = string.Empty;
        public int TokensUsed { get; set; }
        public int TotalTokens { get; set; }
        public int TokensRemaining { get; set; }
        public bool IsNearTokenLimit { get; set; }
        public bool RequiresNewConversation { get; set; }
        public string ContextType { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public Dictionary<string, object>? Data { get; set; }
    }

    // API Response Models for OpenRouter
    public class OpenRouterResponse
    {
        public List<OpenRouterChoice>? Choices { get; set; }
    }

    public class OpenRouterChoice
    {
        public OpenRouterMessage? Message { get; set; }
    }

    public class OpenRouterMessage
    {
        public string? Content { get; set; }
    }
}

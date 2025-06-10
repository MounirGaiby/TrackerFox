using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PersonalFinanceTracker.Data;
using PersonalFinanceTracker.Models;
using PersonalFinanceTracker.ViewModels;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace PersonalFinanceTracker.Controllers
{
    [Authorize]
    public class FinanceAIController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<User> _userManager;
        private readonly ILogger<FinanceAIController> _logger;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;

        public FinanceAIController(
            ApplicationDbContext context,
            UserManager<User> userManager,
            ILogger<FinanceAIController> logger,
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration)
        {
            _context = context;
            _userManager = userManager;
            _logger = logger;
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
        }

        public async Task<IActionResult> Index()
        {
            var userId = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(userId))
            {
                return RedirectToAction("Login", "Account");
            }

            var user = await _userManager.FindByIdAsync(userId);
            var userName = user?.UserName ?? "there";

            var viewModel = new FinanceAIBotViewModel
            {
                UserName = userName,
                AvailableKeywords = GetAvailableKeywords(),
                QuickActions = GetQuickActions(),
                WelcomeMessage = $"Hello {userName}! I'm your Financial AI Assistant. I can help you with spending analysis, and financial planning. Use keywords like @accounts or #spending to get specific insights, or just ask me anything about your finances!"
            };

            return View(viewModel);
        }

        [HttpPost]
        public async Task<IActionResult> Chat([FromBody] BotChatRequest request)
        {
            try
            {
                var userId = _userManager.GetUserId(User);
                if (string.IsNullOrEmpty(userId))
                {
                    return Json(new BotChatResponse { Success = false, Error = "User not authenticated" });
                }

                var user = await _userManager.FindByIdAsync(userId);
                var userName = user?.UserName ?? "User";

                // Process the message and determine context
                var processedRequest = await ProcessUserMessage(request.Message, userId, userName);

                // Generate AI response
                var aiResponse = await GenerateAIResponse(processedRequest, userId, userName);

                return Json(new BotChatResponse
                {
                    Success = true,
                    Message = aiResponse.Message,
                    Data = aiResponse.Data,
                    TokensUsed = aiResponse.TokensUsed,
                    RequiresNewConversation = aiResponse.RequiresNewConversation
                });
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("AI") || ex.Message.Contains("technical difficulties"))
            {
                _logger.LogWarning(ex, "AI service temporarily unavailable");
                return Json(new BotChatResponse
                {
                    Success = false,
                    Error = "🤖 Our AI Assistant is currently down for maintenance. Please try again in a few minutes. We apologize for the inconvenience!"
                });
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "AI service communication error");
                return Json(new BotChatResponse
                {
                    Success = false,
                    Error = "🔧 AI Assistant is experiencing connectivity issues. Please retry your request in a moment."
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error processing chat message");
                return Json(new BotChatResponse
                {
                    Success = false,
                    Error = "⚠️ Something went wrong while processing your message. Please try again later."
                });
            }
        }

        private async Task<ProcessedChatRequest> ProcessUserMessage(string message, string userId, string userName)
        {
            var processed = new ProcessedChatRequest
            {
                OriginalMessage = message,
                UserId = userId,
                UserName = userName,
                ContextType = DetermineContextType(message),
                HasKeywords = ContainsKeywords(message)
            };

            // Get context based on keywords
            if (processed.HasKeywords)
            {
                processed.FinancialContext = await GatherContextBasedOnKeywords(message, userId);
            }
            else
            {
                processed.FinancialContext = await GatherBasicFinancialContext(userId);
            }

            return processed;
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

        private bool ContainsKeywords(string message)
        {
            var keywords = new[] { "@", "#" };
            return keywords.Any(k => message.Contains(k));
        }

        private async Task<FinancialContext> GatherContextBasedOnKeywords(string message, string userId)
        {
            var context = new FinancialContext();
            var lowerMessage = message.ToLower();

            // Gather specific context based on keywords
            if (lowerMessage.Contains("@accounts") || lowerMessage.Contains("@account"))
            {
                context.Accounts = await _context.Accounts
                    .Where(a => a.UserId == userId)
                    .ToListAsync();
            }

            if (lowerMessage.Contains("@transactions") || lowerMessage.Contains("@transaction"))
            {
                var days = ExtractDaysFromMessage(message);
                var startDate = DateTime.UtcNow.AddDays(-days);
                
                context.RecentTransactions = await _context.Transactions
                    .Include(t => t.Account)
                    .Include(t => t.Category)
                    .Where(t => t.Account.UserId == userId && t.Date >= startDate)
                    .OrderByDescending(t => t.Date)
                    .Take(50)
                    .ToListAsync();
            }

            if (lowerMessage.Contains("#spending") || lowerMessage.Contains("#expenses"))
            {
                var category = ExtractCategoryFromMessage(message);
                var lastMonth = DateTime.UtcNow.AddMonths(-1);
                
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

            // Always include basic financial summary
            context.BasicSummary = await CalculateBasicSummary(userId);

            return context;
        }

        private async Task<FinancialContext> GatherBasicFinancialContext(string userId)
        {
            var context = new FinancialContext
            {
                BasicSummary = await CalculateBasicSummary(userId)
            };

            return context;
        }

        private async Task<BasicFinancialSummary> CalculateBasicSummary(string userId)
        {
            var accounts = await _context.Accounts
                .Where(a => a.UserId == userId)
                .ToListAsync();

            var lastMonth = DateTime.UtcNow.AddMonths(-1);
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

        private int ExtractDaysFromMessage(string message)
        {
            // Extract number from message like "@transactions 30" or default to 30
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
            // Extract category from message like "#spending groceries"
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

        private async Task<BotAIResponse> GenerateAIResponse(ProcessedChatRequest request, string userId, string userName)
        {
            try
            {
                var systemPrompt = GenerateSystemPrompt(request.FinancialContext, userName);
                var userMessage = request.OriginalMessage;

                // Call OpenRouter API
                var openRouterApiKey = _configuration["OpenRouter:ApiKey"] ?? "DEMO_API_KEY";
                var httpClient = _httpClientFactory.CreateClient();
                httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", openRouterApiKey);

                var requestBody = new
                {
                    model = "deepseek/deepseek-r1-distill-qwen-7b",
                    messages = new[]
                    {
                        new { role = "system", content = systemPrompt },
                        new { role = "user", content = userMessage }
                    },
                    temperature = 0.7,
                    max_tokens = 800  // Keep responses concise
                };

                var content = new StringContent(
                    JsonSerializer.Serialize(requestBody),
                    Encoding.UTF8,
                    "application/json");

                string aiResponse;
                try
                {
                    var response = await httpClient.PostAsync("https://openrouter.ai/api/v1/chat/completions", content);

                    Console.WriteLine(response.Content);

                    if (response.IsSuccessStatusCode)
                    {
                        string responseBody = await response.Content.ReadAsStringAsync();
                        responseBody = responseBody?.Trim(); // Remove any leading/trailing whitespace
                        
                        _logger.LogInformation("AI API Response (trimmed): {Response}", responseBody);
                        
                        if (string.IsNullOrEmpty(responseBody))
                        {
                            _logger.LogWarning("AI API returned empty response body");
                            throw new InvalidOperationException("AI service returned empty response body");
                        }

                        try
                        {
                            var jsonOptions = new JsonSerializerOptions
                            {
                                PropertyNameCaseInsensitive = true
                            };
                            
                            var responseObject = JsonSerializer.Deserialize<OpenRouterResponse>(responseBody, jsonOptions);
                            _logger.LogInformation("Deserialized response object: Choices count = {Count}", responseObject?.Choices?.Count ?? 0);
                            
                            aiResponse = responseObject?.Choices?.FirstOrDefault()?.Message?.Content;
                            _logger.LogInformation("Extracted AI response content: {Content}", aiResponse);

                            if (string.IsNullOrEmpty(aiResponse))
                            {
                                _logger.LogWarning("AI API returned empty response content. ResponseObject null: {IsNull}, Choices null: {ChoicesNull}, First choice null: {FirstChoiceNull}, Message null: {MessageNull}", 
                                    responseObject == null, 
                                    responseObject?.Choices == null,
                                    responseObject?.Choices?.FirstOrDefault() == null,
                                    responseObject?.Choices?.FirstOrDefault()?.Message == null);
                                throw new InvalidOperationException("AI service returned empty response content");
                            }
                        }
                        catch (JsonException ex)
                        {
                            _logger.LogError(ex, "Failed to deserialize AI API response: {Response}", responseBody);
                            throw new InvalidOperationException("AI service returned invalid response format", ex);
                        }
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

                return new BotAIResponse
                {
                    Message = aiResponse,
                    TokensUsed = EstimateTokens(aiResponse),
                    RequiresNewConversation = false, // Will implement token tracking later
                    Data = FormatContextData(request.FinancialContext)
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating AI response");
                throw new InvalidOperationException("AI Assistant is currently experiencing technical difficulties. Please try again later.", ex);
            }
        }

        private string GenerateSystemPrompt(FinancialContext context, string userName)
        {
            var prompt = new StringBuilder();
            
            prompt.AppendLine($"You are a highly knowledgeable and friendly Financial AI Assistant named 'FinBot'. You are helping {userName} with their personal finances.");
            prompt.AppendLine();
            prompt.AppendLine("CRITICAL RULES:");
            prompt.AppendLine("1. ONLY discuss financial topics: budgeting, saving, investing, expenses, debt management, financial planning, taxes, retirement, insurance, and related topics.");
            prompt.AppendLine("2. If asked about non-financial topics, politely redirect: 'I'm specialized in financial advice. Let's talk about your finances - how can I help you save, invest, or manage your money better?'");
            prompt.AppendLine("3. Keep responses concise (under 500 words) but helpful and actionable.");
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
            prompt.AppendLine("- No need to keep repeating the user's name in every response, but use it naturally when appropriate");
            prompt.AppendLine("- If the user asks for general financial advice, provide it based on their context");
            prompt.AppendLine("- If the user asks about non-financial topics, politely redirect them to financial matters");
            prompt.AppendLine("- No need to keep repeating the users financial data if it's out of context");
            prompt.AppendLine("- No need to repeat yourself in subsequent response unless needed like restating the user savings etc");
            prompt.AppendLine("- Drop hard hitting quotes naturally and randomly 1 in 5 chance without making them appear out of context");
            prompt.AppendLine();
            prompt.AppendLine("AVAILABLE KEYWORDS for users:");
            prompt.AppendLine("- @accounts - Get account information");
            prompt.AppendLine("- @transactions [days] - Get recent transactions");
            prompt.AppendLine("- #spending [category] - Analyze spending");
            prompt.AppendLine("- #savings - Savings advice");
            prompt.AppendLine("- #investment - Investment guidance");

            return prompt.ToString();
        }

        private int EstimateTokens(string text)
        {
            // Rough estimation: 4 characters per token
            return (int)Math.Ceiling(text.Length / 4.0);
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

        private List<FinancialKeyword> GetAvailableKeywords()
        {
            return new List<FinancialKeyword>
            {
                new() { Keyword = "@accounts", Description = "Get information about your accounts", Example = "@accounts" },
                new() { Keyword = "@transactions", Description = "Show recent transactions", Example = "@transactions 30" },
                new() { Keyword = "#spending", Description = "Analyze spending patterns", Example = "#spending groceries" },
                new() { Keyword = "#savings", Description = "Get savings advice", Example = "#savings tips" },
                new() { Keyword = "#investment", Description = "Investment guidance", Example = "#investment strategy" }
            };
        }

        private List<QuickAction> GetQuickActions()
        {
            return new List<QuickAction>
            {
                new() { Text = "Show my account balances", Action = "@accounts" },
                new() { Text = "Analyze my spending this month", Action = "#spending" },
                new() { Text = "Recent transactions", Action = "@transactions 14" },
                new() { Text = "How can I save more money?", Action = "#savings help" },
                new() { Text = "Investment advice for beginners", Action = "#investment beginner" }
            };
        }
    }

    // API Response Models
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

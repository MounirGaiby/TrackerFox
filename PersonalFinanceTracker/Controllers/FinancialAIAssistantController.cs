using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using PersonalFinanceTracker.Models;
using PersonalFinanceTracker.Services;
using PersonalFinanceTracker.ViewModels;

namespace PersonalFinanceTracker.Controllers
{
    [Authorize]
    public class FinancialAIAssistantController : Controller
    {
        private readonly IFinancialAIService _aiService;
        private readonly UserManager<User> _userManager;
        private readonly ILogger<FinancialAIAssistantController> _logger;

        public FinancialAIAssistantController(
            IFinancialAIService aiService,
            UserManager<User> userManager,
            ILogger<FinancialAIAssistantController> logger)
        {
            _aiService = aiService;
            _userManager = userManager;
            _logger = logger;
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

            // Get or create active conversation
            var activeConversation = await _aiService.GetOrCreateActiveConversationAsync(userId);

            var viewModel = new FinancialAIAssistantViewModel
            {
                UserName = userName,
                ConversationId = activeConversation.Id,
                ConversationTitle = activeConversation.Title,
                IsNewConversation = activeConversation.Messages.Count == 0,
                TokensUsed = activeConversation.TotalTokens,
                TokensRemaining = Math.Max(0, activeConversation.MaxTokens - activeConversation.TotalTokens),
                IsNearTokenLimit = activeConversation.TotalTokens >= Conversation.TokenWarningThreshold,
                Messages = activeConversation.Messages.OrderBy(m => m.CreatedAt).ToList(),
                AvailableKeywords = GetAvailableKeywords(),
                QuickActions = GetQuickActions(),
                WelcomeMessage = GetWelcomeMessage(userName, activeConversation.Messages.Count == 0)
            };

            return View(viewModel);
        }

        [HttpPost]
        public async Task<IActionResult> Chat([FromBody] ChatRequest request)
        {
            try
            {
                var userId = _userManager.GetUserId(User);
                if (string.IsNullOrEmpty(userId))
                {
                    return Json(new ChatResponse { Success = false, Error = "User not authenticated" });
                }

                var user = await _userManager.FindByIdAsync(userId);
                var userName = user?.UserName ?? "User";

                // Validate input
                if (string.IsNullOrWhiteSpace(request.Message))
                {
                    return Json(new ChatResponse { Success = false, Error = "Message cannot be empty" });
                }

                if (request.Message.Length > 1000)
                {
                    return Json(new ChatResponse { Success = false, Error = "Message is too long. Please keep it under 1000 characters." });
                }

                // Process chat message through AI service
                var response = await _aiService.ProcessChatMessageAsync(
                    userId, 
                    request.Message, 
                    request.ConversationId
                );

                return Json(new ChatResponse
                {
                    Success = true,
                    Message = response.Message,
                    ConversationId = response.ConversationId,
                    ConversationTitle = response.ConversationTitle,
                    TokensUsed = response.TokensUsed,
                    TotalTokens = response.TotalTokens,
                    TokensRemaining = response.TokensRemaining,
                    IsNearTokenLimit = response.IsNearTokenLimit,
                    RequiresNewConversation = response.RequiresNewConversation,
                    ContextType = response.ContextType,
                    Timestamp = response.Timestamp
                });
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Invalid chat request from user {UserId}", _userManager.GetUserId(User));
                return Json(new ChatResponse
                {
                    Success = false,
                    Error = ex.Message
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing chat message for user {UserId}", _userManager.GetUserId(User));
                return Json(new ChatResponse
                {
                    Success = false,
                    Error = "I'm having trouble processing your request. Please try again in a moment."
                });
            }
        }

        [HttpPost]
        public async Task<IActionResult> StartNewConversation()
        {
            try
            {
                var userId = _userManager.GetUserId(User);
                if (string.IsNullOrEmpty(userId))
                {
                    return Json(new { Success = false, Error = "User not authenticated" });
                }

                var newConversation = await _aiService.StartNewConversationAsync(userId);

                return Json(new
                {
                    Success = true,
                    ConversationId = newConversation.Id,
                    ConversationTitle = newConversation.Title,
                    Message = "Started a new conversation! How can I help you with your finances today?"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error starting new conversation for user {UserId}", _userManager.GetUserId(User));
                return Json(new { Success = false, Error = "Failed to start new conversation" });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetConversationHistory()
        {
            try
            {
                var userId = _userManager.GetUserId(User);
                if (string.IsNullOrEmpty(userId))
                {
                    return Json(new { Success = false, Error = "User not authenticated" });
                }

                var conversations = await _aiService.GetUserConversationsAsync(userId);

                var conversationSummaries = conversations.Select(c => new
                {
                    Id = c.Id,
                    Title = c.Title,
                    LastMessage = c.Messages.OrderByDescending(m => m.CreatedAt).FirstOrDefault()?.Content ?? "",
                    LastMessageTime = c.Messages.OrderByDescending(m => m.CreatedAt).FirstOrDefault()?.CreatedAt ?? c.CreatedAt,
                    MessageCount = c.Messages.Count,
                    TokensUsed = c.TotalTokens,
                    IsActive = c.IsActive
                }).OrderByDescending(c => c.LastMessageTime);

                return Json(new { Success = true, Conversations = conversationSummaries });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting conversation history for user {UserId}", _userManager.GetUserId(User));
                return Json(new { Success = false, Error = "Failed to load conversation history" });
            }
        }

        [HttpPost]
        public async Task<IActionResult> LoadConversation([FromBody] LoadConversationRequest request)
        {
            try
            {
                var userId = _userManager.GetUserId(User);
                if (string.IsNullOrEmpty(userId))
                {
                    return Json(new { Success = false, Error = "User not authenticated" });
                }

                var conversation = await _aiService.LoadConversationAsync(userId, request.ConversationId);
                if (conversation == null)
                {
                    return Json(new { Success = false, Error = "Conversation not found" });
                }

                return Json(new
                {
                    Success = true,
                    ConversationId = conversation.Id,
                    ConversationTitle = conversation.Title,
                    Messages = conversation.Messages.OrderBy(m => m.CreatedAt).Select(m => new
                    {
                        Role = m.Role.ToString(),
                        Content = m.Content,
                        Timestamp = m.CreatedAt,
                        TokensUsed = m.TokensUsed,
                        ContextType = m.ContextType?.ToString()
                    }),
                    TokensUsed = conversation.TotalTokens,
                    TokensRemaining = Math.Max(0, conversation.MaxTokens - conversation.TotalTokens),
                    IsNearTokenLimit = conversation.TotalTokens >= Conversation.TokenWarningThreshold
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading conversation {ConversationId} for user {UserId}", 
                    request.ConversationId, _userManager.GetUserId(User));
                return Json(new { Success = false, Error = "Failed to load conversation" });
            }
        }

        private List<FinancialKeyword> GetAvailableKeywords()
        {
            return new List<FinancialKeyword>
            {
                new FinancialKeyword
                {
                    Keyword = "@accounts",
                    Description = "Get information about your accounts and balances",
                    Example = "Show me my @accounts balances"
                },
                new FinancialKeyword
                {
                    Keyword = "@transactions",
                    Description = "Get recent transaction history",
                    Example = "Show me my @transactions from last 30 days"
                },
                new FinancialKeyword
                {
                    Keyword = "#spending",
                    Description = "Analyze your spending patterns",
                    Example = "Analyze my #spending on groceries"
                },
                new FinancialKeyword
                {
                    Keyword = "#budget",
                    Description = "Review budget goals and progress",
                    Example = "How am I doing with my #budget this month?"
                },
                new FinancialKeyword
                {
                    Keyword = "#savings",
                    Description = "Get savings insights and recommendations",
                    Example = "Give me #savings tips based on my spending"
                },
                new FinancialKeyword
                {
                    Keyword = "#investment",
                    Description = "Investment analysis and advice",
                    Example = "Suggest #investment strategies for my portfolio"
                }
            };
        }

        private List<QuickAction> GetQuickActions()
        {
            return new List<QuickAction>
            {
                new QuickAction
                {
                    Text = "Show my account balances",
                    Action = "Show me my @accounts balances"
                },
                new QuickAction
                {
                    Text = "Analyze recent spending",
                    Action = "Analyze my #spending from last month"
                },
                new QuickAction
                {
                    Text = "Check budget progress",
                    Action = "How am I doing with my #budget goals?"
                },
                new QuickAction
                {
                    Text = "Get savings tips",
                    Action = "Give me personalized #savings recommendations"
                },
                new QuickAction
                {
                    Text = "Recent transactions",
                    Action = "Show me my recent @transactions"
                }
            };
        }

        private string GetWelcomeMessage(string userName, bool isNewConversation)
        {
            if (isNewConversation)
            {
                return $"Hello {userName}! I'm your Financial AI Assistant. I'm here to help you with budgeting, spending analysis, savings tips, and financial planning. " +
                       $"Use keywords like @accounts or #spending to get specific insights, or just ask me anything about your finances!";
            }
            else
            {
                return $"Welcome back, {userName}! Let's continue our conversation about your finances.";
            }
        }
    }

    // Request/Response models
    public class ChatRequest
    {
        public string Message { get; set; } = string.Empty;
        public int? ConversationId { get; set; }
    }

    public class ChatResponse
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
        public DateTime Timestamp { get; set; }
    }

    public class LoadConversationRequest
    {
        public int ConversationId { get; set; }
    }
}

using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using PersonalFinanceTracker.Models;
using PersonalFinanceTracker.Data;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace PersonalFinanceTracker.Controllers
{
    [Authorize]
    public class SettingsController : Controller
    {
        private readonly UserManager<User> _userManager;
        private readonly SignInManager<User> _signInManager;
        private readonly ApplicationDbContext _context;
        private readonly ILogger<SettingsController> _logger;

        public SettingsController(
            UserManager<User> userManager,
            SignInManager<User> signInManager,
            ApplicationDbContext context,
            ILogger<SettingsController> logger)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _context = context;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return RedirectToAction("Login", "Account");
            }

            ViewBag.Country = user.Country;
            ViewBag.Currency = user.Currency;

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdatePreferences(string country, string currency)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return RedirectToAction("Login", "Account");
            }

            user.Country = country ?? "Morocco";
            user.Currency = currency ?? "MAD";
            
            await _userManager.UpdateAsync(user);
            TempData["SuccessMessage"] = "Settings updated successfully!";

            return RedirectToAction("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ExportData()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return RedirectToAction("Login", "Account");
            }

            try
            {
                // Get all user data
                var accounts = await _context.Accounts
                    .Where(a => a.UserId == user.Id)
                    .ToListAsync();

                var transactions = await _context.Transactions
                    .Where(t => accounts.Select(a => a.Id).Contains(t.AccountId))
                    .Include(t => t.Category)
                    .ToListAsync();

                var categories = await _context.Categories.ToListAsync();

                // Create export data object
                var exportData = new
                {
                    ExportDate = DateTime.UtcNow,
                    User = new
                    {
                        user.FirstName,
                        user.LastName,
                        user.Email,
                        user.Country,
                        user.Currency,
                        user.CreatedAt
                    },
                    Accounts = accounts.Select(a => new
                    {
                        a.Id,
                        a.Name,
                        a.Type,
                        a.Balance,
                        a.CreatedAt
                    }),
                    Transactions = transactions.Select(t => new
                    {
                        t.Id,
                        t.Amount,
                        t.Description,
                        t.Date,
                        t.Type,
                        Category = t.Category?.Name,
                        AccountId = t.AccountId
                    }),
                    Categories = categories.Select(c => new
                    {
                        c.Id,
                        c.Name,
                        c.Description
                    })
                };

                // Convert to JSON
                var json = JsonSerializer.Serialize(exportData, new JsonSerializerOptions
                {
                    WriteIndented = true
                });

                var fileName = $"finance_data_{user.Email}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.json";
                
                return File(System.Text.Encoding.UTF8.GetBytes(json), "application/json", fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting data for user {UserId}", user.Id);
                TempData["ErrorMessage"] = "Error exporting data. Please try again.";
                return RedirectToAction("Index");
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteAccount(string confirmEmail)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return RedirectToAction("Login", "Account");
            }

            if (confirmEmail != user.Email)
            {
                TempData["ErrorMessage"] = "Email confirmation doesn't match your account email.";
                return RedirectToAction("Index");
            }

            try
            {
                // Delete all related data first
                var accounts = await _context.Accounts
                    .Where(a => a.UserId == user.Id)
                    .ToListAsync();

                var accountIds = accounts.Select(a => a.Id).ToList();

                // Delete transactions
                var transactions = await _context.Transactions
                    .Where(t => accountIds.Contains(t.AccountId))
                    .ToListAsync();
                _context.Transactions.RemoveRange(transactions);

                // Delete conversations and chat messages
                var conversations = await _context.Conversations
                    .Where(c => c.UserId == user.Id)
                    .Include(c => c.Messages)
                    .ToListAsync();
                _context.Conversations.RemoveRange(conversations);

                // Delete accounts
                _context.Accounts.RemoveRange(accounts);

                await _context.SaveChangesAsync();

                // Delete the user account
                var result = await _userManager.DeleteAsync(user);
                
                if (result.Succeeded)
                {
                    _logger.LogInformation("User account {Email} was deleted successfully", user.Email);
                    
                    // Sign out the user after successful account deletion
                    await _signInManager.SignOutAsync();
                    _logger.LogInformation("User {Email} signed out after account deletion", user.Email);
                    
                    return RedirectToAction("Index", "Home");
                }
                else
                {
                    foreach (var error in result.Errors)
                    {
                        _logger.LogError("Error deleting user account: {Error}", error.Description);
                    }
                    TempData["ErrorMessage"] = "Error deleting account. Please contact support.";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting account for user {UserId}", user.Id);
                TempData["ErrorMessage"] = "Error deleting account. Please try again.";
            }

            return RedirectToAction("Index");
        }
    }
}

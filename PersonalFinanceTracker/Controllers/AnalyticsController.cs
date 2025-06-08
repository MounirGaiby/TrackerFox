using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PersonalFinanceTracker.Data;
using PersonalFinanceTracker.Models;
using PersonalFinanceTracker.ViewModels;
using System.Text.Json;

namespace PersonalFinanceTracker.Controllers
{
    [Authorize]
    public class AnalyticsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<User> _userManager;
        private readonly ILogger<AnalyticsController> _logger;

        public AnalyticsController(
            ApplicationDbContext context,
            UserManager<User> userManager,
            ILogger<AnalyticsController> logger)
        {
            _context = context;
            _userManager = userManager;
            _logger = logger;
        }

        public async Task<IActionResult> Index()
        {
            var userId = _userManager.GetUserId(User);
            var currentYear = DateTime.Now.Year;
            
            // Get accounts
            var accounts = await _context.Accounts
                .Where(a => a.UserId == userId)
                .ToListAsync();
            
            // Get monthly spending by category for the current year
            var monthlyCategorySpending = await _context.Transactions
                .Include(t => t.Account)
                .Include(t => t.Category)
                .Where(t => t.Account.UserId == userId && 
                           t.Type == TransactionType.Expense &&
                           t.Date.Year == currentYear)
                .GroupBy(t => new { Month = t.Date.Month, CategoryName = t.Category.Name })
                .Select(g => new
                {
                    Month = g.Key.Month,
                    Category = g.Key.CategoryName,
                    Amount = g.Sum(t => t.Amount)
                })
                .ToListAsync();
                
            // Monthly income/expense totals
            var monthlyTotals = await _context.Transactions
                .Include(t => t.Account)
                .Where(t => t.Account.UserId == userId &&
                           t.Date.Year == currentYear)
                .GroupBy(t => new { Month = t.Date.Month, t.Type })
                .Select(g => new
                {
                    Month = g.Key.Month,
                    Type = g.Key.Type,
                    Amount = g.Sum(t => t.Amount)
                })
                .ToListAsync();
                
            // Spending trends by day of week
            var dayOfWeekSpending = await _context.Transactions
                .Include(t => t.Account)
                .Where(t => t.Account.UserId == userId &&
                           t.Type == TransactionType.Expense &&
                           t.Date.Year == currentYear)
                .GroupBy(t => (int)t.Date.DayOfWeek)
                .Select(g => new
                {
                    DayOfWeek = g.Key,
                    Amount = g.Sum(t => t.Amount)
                })
                .OrderBy(x => x.DayOfWeek)
                .ToListAsync();
            
            // Net worth over time (monthly)
            var netWorthOverTime = new List<object>();
            for (int month = 1; month <= 12; month++)
            {
                var transactionsUpToMonth = await _context.Transactions
                    .Include(t => t.Account)
                    .Where(t => t.Account.UserId == userId &&
                               (t.Date.Year < currentYear || 
                                (t.Date.Year == currentYear && t.Date.Month <= month)))
                    .ToListAsync();

                var income = transactionsUpToMonth
                    .Where(t => t.Type == TransactionType.Income)
                    .Sum(t => t.Amount);
                
                var expenses = transactionsUpToMonth
                    .Where(t => t.Type == TransactionType.Expense)
                    .Sum(t => t.Amount);
                
                var netWorth = income - expenses;
                
                netWorthOverTime.Add(new
                {
                    Month = month,
                    NetWorth = netWorth
                });
            }
            
            // Convert data to JSON for charts
            ViewBag.MonthlyCategorySpendingJson = JsonSerializer.Serialize(monthlyCategorySpending);
            ViewBag.MonthlyTotalsJson = JsonSerializer.Serialize(monthlyTotals);
            ViewBag.DayOfWeekSpendingJson = JsonSerializer.Serialize(dayOfWeekSpending);
            ViewBag.NetWorthOverTimeJson = JsonSerializer.Serialize(netWorthOverTime);
            
            // Get top spending categories and merchants
            var topCategories = await _context.Transactions
                .Include(t => t.Account)
                .Include(t => t.Category)
                .Where(t => t.Account.UserId == userId && 
                           t.Type == TransactionType.Expense &&
                           t.Date.Year == currentYear)
                .GroupBy(t => t.Category.Name)
                .Select(g => new
                {
                    Category = g.Key,
                    Amount = g.Sum(t => t.Amount),
                    Count = g.Count()
                })
                .OrderByDescending(x => x.Amount)
                .Take(5)
                .ToListAsync();
            
            var topMerchants = await _context.Transactions
                .Include(t => t.Account)
                .Where(t => t.Account.UserId == userId && 
                           t.Type == TransactionType.Expense &&
                           t.Date.Year == currentYear)
                .GroupBy(t => t.Description)
                .Select(g => new
                {
                    Merchant = g.Key,
                    Amount = g.Sum(t => t.Amount),
                    Count = g.Count()
                })
                .OrderByDescending(x => x.Amount)
                .Take(5)
                .ToListAsync();
                
            ViewBag.TopCategoriesJson = JsonSerializer.Serialize(topCategories);
            ViewBag.TopMerchantsJson = JsonSerializer.Serialize(topMerchants);
            
            // Get monthly savings rate
            var savingsRates = new List<object>();
            for (int month = 1; month <= 12; month++)
            {
                var monthlyIncome = monthlyTotals
                    .Where(t => t.Month == month && t.Type == TransactionType.Income)
                    .Sum(t => t.Amount);
                
                var monthlyExpenses = monthlyTotals
                    .Where(t => t.Month == month && t.Type == TransactionType.Expense)
                    .Sum(t => t.Amount);
                
                var savingsRate = monthlyIncome > 0 
                    ? ((monthlyIncome - monthlyExpenses) / monthlyIncome) * 100 
                    : 0;
                
                savingsRates.Add(new
                {
                    Month = month,
                    SavingsRate = savingsRate
                });
            }
            
            ViewBag.SavingsRatesJson = JsonSerializer.Serialize(savingsRates);
            
            return View();
        }
        
        public async Task<IActionResult> CategoryDetails(int id)
        {
            var userId = _userManager.GetUserId(User);
            var category = await _context.Categories.FindAsync(id);
            
            if (category == null)
            {
                return NotFound();
            }
            
            var transactions = await _context.Transactions
                .Include(t => t.Account)
                .Include(t => t.Category)
                .Where(t => t.Account.UserId == userId && 
                           t.CategoryId == id)
                .OrderByDescending(t => t.Date)
                .ToListAsync();
                
            ViewBag.Category = category;
            
            return View(transactions);
        }
        
        public async Task<IActionResult> AccountDetails(int id)
        {
            var userId = _userManager.GetUserId(User);
            var account = await _context.Accounts
                .FirstOrDefaultAsync(a => a.Id == id && a.UserId == userId);
                
            if (account == null)
            {
                return NotFound();
            }
            
            var transactions = await _context.Transactions
                .Include(t => t.Category)
                .Where(t => t.AccountId == id)
                .OrderByDescending(t => t.Date)
                .ToListAsync();
                
            var monthlyBalances = new List<object>();
            var currentYear = DateTime.Now.Year;
            
            for (int month = 1; month <= 12; month++)
            {
                var transactionsUpToMonth = transactions
                    .Where(t => (t.Date.Year < currentYear || 
                               (t.Date.Year == currentYear && t.Date.Month <= month)))
                    .ToList();
                
                var income = transactionsUpToMonth
                    .Where(t => t.Type == TransactionType.Income)
                    .Sum(t => t.Amount);
                
                var expenses = transactionsUpToMonth
                    .Where(t => t.Type == TransactionType.Expense)
                    .Sum(t => t.Amount);
                
                monthlyBalances.Add(new
                {
                    Month = month,
                    Balance = income - expenses
                });
            }
            
            ViewBag.Account = account;
            ViewBag.MonthlyBalancesJson = JsonSerializer.Serialize(monthlyBalances);
            
            return View(transactions);
        }
        
        public async Task<IActionResult> Predictions()
        {
            var userId = _userManager.GetUserId(User);
            
            // Get spending patterns
            var transactions = await _context.Transactions
                .Include(t => t.Account)
                .Include(t => t.Category)
                .Where(t => t.Account.UserId == userId)
                .OrderBy(t => t.Date)
                .ToListAsync();
                
            // Calculate average monthly spending by category
            var categoryAverages = transactions
                .Where(t => t.Type == TransactionType.Expense)
                .GroupBy(t => new { t.Category.Name, Month = t.Date.Month, Year = t.Date.Year })
                .Select(g => new
                {
                    Category = g.Key.Name,
                    Month = g.Key.Month,
                    Year = g.Key.Year,
                    Amount = g.Sum(t => t.Amount)
                })
                .GroupBy(x => x.Category)
                .Select(g => new
                {
                    Category = g.Key,
                    AverageMonthly = g.Average(x => x.Amount)
                })
                .OrderByDescending(x => x.AverageMonthly)
                .ToList();
            
            // Calculate projected expenses for next 3 months
            var projectedExpenses = new List<object>();
            var currentMonth = DateTime.Now.Month;
            var currentYear = DateTime.Now.Year;
            
            for (int i = 1; i <= 3; i++)
            {
                var forecastMonth = (currentMonth + i) % 12;
                if (forecastMonth == 0) forecastMonth = 12;
                var forecastYear = currentYear + ((currentMonth + i) > 12 ? 1 : 0);
                
                projectedExpenses.Add(new
                {
                    Month = forecastMonth,
                    Year = forecastYear,
                    Expenses = categoryAverages.Sum(c => c.AverageMonthly)
                });
            }
            
            ViewBag.CategoryAveragesJson = JsonSerializer.Serialize(categoryAverages);
            ViewBag.ProjectedExpensesJson = JsonSerializer.Serialize(projectedExpenses);
            
            return View();
        }
    }
}

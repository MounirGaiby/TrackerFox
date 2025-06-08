using System.Diagnostics;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PersonalFinanceTracker.Data;
using PersonalFinanceTracker.Models;
using PersonalFinanceTracker.ViewModels;

namespace PersonalFinanceTracker.Controllers;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;
    private readonly ApplicationDbContext _context;
    private readonly UserManager<User> _userManager;

    public HomeController(
        ILogger<HomeController> logger,
        ApplicationDbContext context,
        UserManager<User> userManager)
    {
        _logger = logger;
        _context = context;
        _userManager = userManager;
    }

    public async Task<IActionResult> Index()
    {
        if (!User.Identity?.IsAuthenticated ?? true)
        {
            return View("Landing");
        }

        var userId = _userManager.GetUserId(User);
        var currentMonth = DateTime.Now.Month;
        var currentYear = DateTime.Now.Year;

        // Get user accounts
        var accounts = await _context.Accounts
            .Where(a => a.UserId == userId)
            .Select(a => new AccountViewModel
            {
                Id = a.Id,
                Name = a.Name,
                Type = a.Type,
                Balance = a.Balance
            })
            .ToListAsync();

        // Get recent transactions
        var recentTransactions = await _context.Transactions
            .Include(t => t.Account)
            .Include(t => t.Category)
            .Where(t => t.Account.UserId == userId)
            .OrderByDescending(t => t.Date)
            .Take(10)
            .Select(t => new TransactionViewModel
            {
                Id = t.Id,
                Amount = t.Amount,
                Description = t.Description,
                Type = t.Type,
                Date = t.Date,
                AccountName = t.Account.Name,
                CategoryName = t.Category.Name
            })
            .ToListAsync();

        // Calculate monthly income and expenses
        var monthlyTransactions = await _context.Transactions
            .Include(t => t.Account)
            .Where(t => t.Account.UserId == userId && 
                       t.Date.Month == currentMonth && 
                       t.Date.Year == currentYear)
            .ToListAsync();

        var monthlyIncome = monthlyTransactions
            .Where(t => t.Type == TransactionType.Income)
            .Sum(t => t.Amount);

        var monthlyExpenses = monthlyTransactions
            .Where(t => t.Type == TransactionType.Expense)
            .Sum(t => t.Amount);        // Get category spending for current month
        var categorySpendingData = await _context.Transactions
            .Include(t => t.Account)
            .Include(t => t.Category)
            .Where(t => t.Account.UserId == userId && 
                       t.Type == TransactionType.Expense &&
                       t.Date.Month == currentMonth && 
                       t.Date.Year == currentYear)
            .GroupBy(t => t.Category.Name)
            .Select(g => new CategorySpendingViewModel
            {
                CategoryName = g.Key,
                Amount = g.Sum(t => t.Amount)
            })
            .ToListAsync();

        var categorySpending = categorySpendingData
            .OrderByDescending(c => c.Amount)
            .Take(5)
            .ToList();

        // Calculate percentages for category spending
        var totalExpenses = categorySpending.Sum(c => c.Amount);
        foreach (var category in categorySpending)
        {
            category.Percentage = totalExpenses > 0 ? (category.Amount / totalExpenses) * 100 : 0;
        }

        var viewModel = new DashboardViewModel
        {
            TotalBalance = accounts.Sum(a => a.Balance),
            MonthlyIncome = monthlyIncome,
            MonthlyExpenses = monthlyExpenses,
            Accounts = accounts,
            RecentTransactions = recentTransactions,
            CategorySpending = categorySpending
        };

        return View(viewModel);
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}

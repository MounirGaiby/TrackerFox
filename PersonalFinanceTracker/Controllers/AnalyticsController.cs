using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PersonalFinanceTracker.Data;
using PersonalFinanceTracker.Models;
using PersonalFinanceTracker.ViewModels;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Globalization;
using System.Collections.Generic;

namespace PersonalFinanceTracker.Controllers
{
    [Authorize]
    public class AnalyticsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<User> _userManager;
        private readonly ILogger<AnalyticsController> _logger;

        public AnalyticsController(ApplicationDbContext context, UserManager<User> userManager, ILogger<AnalyticsController> logger)
        {
            _context = context;
            _userManager = userManager;
            _logger = logger;
        }

        public async Task<IActionResult> Index(DateTime? startDate, DateTime? endDate, int? accountId)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var sDate = startDate ?? DateTime.UtcNow.AddMonths(-1);
            var eDate = endDate ?? DateTime.UtcNow;

            // Ensure sDate is the beginning of its day and eDate is the end of its day
            sDate = sDate.Date;
            eDate = eDate.Date.AddDays(1).AddTicks(-1);

            // Specify Kind as UTC
            sDate = DateTime.SpecifyKind(sDate, DateTimeKind.Utc);
            eDate = DateTime.SpecifyKind(eDate, DateTimeKind.Utc);

            _logger.LogInformation("Analytics Index requested for user {UserId}, StartDate: {StartDate}, EndDate: {EndDate}, AccountId: {AccountId}", user.Id, sDate, eDate, accountId);


            var transactionsQuery = _context.Transactions
                                            .Include(t => t.Category)
                                            .Include(t => t.Account)
                                            .Where(t => t.Account.UserId == user.Id && t.Date >= sDate && t.Date <= eDate);

            if (accountId.HasValue)
            {
                transactionsQuery = transactionsQuery.Where(t => t.AccountId == accountId.Value);
            }

            var transactions = await transactionsQuery.OrderByDescending(t => t.Date).ToListAsync();

            var totalIncome = transactions.Where(t => t.Type == TransactionType.Income).Sum(t => t.Amount);
            var totalExpenses = transactions.Where(t => t.Type == TransactionType.Expense).Sum(t => t.Amount);
            var netFlow = totalIncome - totalExpenses;

            var spendingByCategory = transactions
                .Where(t => t.Type == TransactionType.Expense && t.Category != null)
                .GroupBy(t => t.Category.Name)
                .Select(g => new ChartDataItem<decimal> { CategoryName = g.Key ?? "Uncategorized", TotalAmount = g.Sum(t => t.Amount) })
                .OrderByDescending(x => x.TotalAmount)
                .ToList();

            var incomeVsExpenseMonthly = transactions
                .GroupBy(t => new { Year = t.Date.Year, Month = t.Date.Month })
                .Select(g => new
                {
                    Period = new DateTime(g.Key.Year, g.Key.Month, 1),
                    Income = g.Where(t => t.Type == TransactionType.Income).Sum(t => t.Amount),
                    Expenses = g.Where(t => t.Type == TransactionType.Expense).Sum(t => t.Amount)
                })
                .OrderBy(x => x.Period)
                .ToList();
            
            var cashFlowOverTime = transactions
                .OrderBy(t => t.Date)
                .Select(t => new {
                    t.Date,
                    Amount = t.Type == TransactionType.Income ? t.Amount : -t.Amount
                })
                .GroupBy(t => t.Date.Date) // Group by day
                .Select(g => new {
                    Date = g.Key,
                    NetChange = g.Sum(x => x.Amount)
                })
                .ToList();

            // Calculate cumulative cash flow for the chart
            decimal cumulativeFlow = 0;
            var cashFlowChartDataPoints = cashFlowOverTime.Select(cf => {
                cumulativeFlow += cf.NetChange;
                return new { Date = cf.Date, Value = cumulativeFlow };
            }).ToList();


            var availableAccounts = await _context.Accounts.Where(a => a.UserId == user.Id).ToListAsync();

            var viewModel = new AnalyticsViewModel
            {
                StartDate = sDate,
                EndDate = eDate,
                AccountId = accountId,
                AvailableAccounts = availableAccounts.Select(a => new AccountViewModel { Id = a.Id, Name = a.Name }).ToList(),
                TotalIncome = totalIncome,
                TotalExpenses = totalExpenses,
                NetFlow = netFlow,
                TotalTransactions = transactions.Count,
                RecentTransactions = transactions.Take(10).ToList(), // Show top 10 recent for the view
                SpendingByCategoryChart = new ChartDataViewModel<List<ChartDataItem<decimal>>>
                {
                    Data = spendingByCategory
                },
                IncomeVsExpenseChart = new ChartDataViewModel<IncomeExpenseData> 
                {
                     Data = new IncomeExpenseData
                    {
                        Labels = incomeVsExpenseMonthly.Select(x => x.Period.ToString("MMM yyyy")).ToList(),
                        IncomeData = incomeVsExpenseMonthly.Select(x => x.Income).ToList(),
                        ExpenseData = incomeVsExpenseMonthly.Select(x => x.Expenses).ToList()
                    }
                },
                 CashFlowChart = new ChartDataViewModel<object> 
                {
                    Data = new 
                    {
                        Labels = cashFlowChartDataPoints.Select(dp => dp.Date.ToString("yyyy-MM-dd")).ToList(), 
                        CashFlowData = cashFlowChartDataPoints.Select(dp => dp.Value).ToList() 
                    }
                }
            };

            return View(viewModel);
        }

        [HttpGet("Analytics/UpdateCharts")] 
        public async Task<IActionResult> UpdateCharts(DateTime startDate, DateTime endDate, int? accountId)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();
            
            _logger.LogInformation("UpdateCharts called for user {UserId}, StartDate: {StartDate}, EndDate: {EndDate}, AccountId: {AccountId}", user.Id, startDate, endDate, accountId);

            // Ensure startDate is the beginning of its day and UTC
            DateTime startDateUtc = DateTime.SpecifyKind(startDate.Date, DateTimeKind.Utc);
            // Ensure endDate is the end of its day and UTC
            DateTime endDateUtc = DateTime.SpecifyKind(endDate.Date.AddDays(1).AddTicks(-1), DateTimeKind.Utc);

            var transactionsQuery = _context.Transactions
                                            .Include(t => t.Category)
                                            .Include(t => t.Account)
                                            .Where(t => t.Account.UserId == user.Id && t.Date >= startDateUtc && t.Date <= endDateUtc);

            if (accountId.HasValue)
            {
                transactionsQuery = transactionsQuery.Where(t => t.AccountId == accountId.Value);
            }

            var transactions = await transactionsQuery.ToListAsync();

            var totalIncome = transactions.Where(t => t.Type == TransactionType.Income).Sum(t => t.Amount);
            var totalExpenses = transactions.Where(t => t.Type == TransactionType.Expense).Sum(t => t.Amount);
            var netFlow = totalIncome - totalExpenses;

            var spendingByCategory = transactions
                .Where(t => t.Type == TransactionType.Expense && t.Category != null)
                .GroupBy(t => t.Category.Name)
                .Select(g => new ChartDataItem<decimal> { CategoryName = g.Key ?? "Uncategorized", TotalAmount = g.Sum(t => t.Amount) })
                .OrderByDescending(x => x.TotalAmount)
                .ToList();
            
            var incomeVsExpenseMonthly = transactions
                .GroupBy(t => new { Year = t.Date.Year, Month = t.Date.Month })
                .Select(g => new
                {
                    Period = new DateTime(g.Key.Year, g.Key.Month, 1),
                    Income = g.Where(t => t.Type == TransactionType.Income).Sum(t => t.Amount),
                    Expenses = g.Where(t => t.Type == TransactionType.Expense).Sum(t => t.Amount)
                })
                .OrderBy(x => x.Period)
                .ToList();

            var cashFlowOverTime = transactions
                .OrderBy(t => t.Date)
                .Select(t => new {
                    t.Date,
                    Amount = t.Type == TransactionType.Income ? t.Amount : -t.Amount
                })
                .GroupBy(t => t.Date.Date) 
                .Select(g => new {
                    Date = g.Key,
                    NetChange = g.Sum(x => x.Amount)
                })
                .ToList();
            
            decimal cumulativeFlow = 0;
            var cashFlowChartDataPoints = cashFlowOverTime.Select(cf => {
                cumulativeFlow += cf.NetChange;
                return new { Date = cf.Date, Value = cumulativeFlow };
            }).ToList();

            return Json(new
            {
                totalIncome,
                totalExpenses,
                netFlow,
                totalTransactions = transactions.Count,
                spendingByCategoryChart = new { Data = spendingByCategory },
                incomeVsExpenseChart = new 
                {
                    Data = new
                    {
                        Labels = incomeVsExpenseMonthly.Select(x => x.Period.ToString("MMM yyyy")).ToList(),
                        IncomeData = incomeVsExpenseMonthly.Select(x => x.Income).ToList(),
                        ExpenseData = incomeVsExpenseMonthly.Select(x => x.Expenses).ToList()
                    }
                },
                cashFlowChart = new
                {
                    Data = new
                    {
                        Labels = cashFlowChartDataPoints.Select(dp => dp.Date.ToString("yyyy-MM-dd")).ToList(),
                        CashFlowData = cashFlowChartDataPoints.Select(dp => dp.Value).ToList()
                    }
                },
                recentTransactions = transactions.Take(10).Select(t => new {
                    Date = t.Date.ToString("yyyy-MM-dd"),
                    Description = t.Description,
                    Amount = t.Amount,
                    Type = t.Type.ToString(),
                    Category = t.Category?.Name ?? "N/A",
                    Account = t.Account.Name
                }).ToList()
            });
        }
    }
}

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using PersonalFinanceTracker.Data;
using PersonalFinanceTracker.Models;
using PersonalFinanceTracker.ViewModels;

namespace PersonalFinanceTracker.Controllers
{
    [Authorize]
    public class TransactionsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<User> _userManager;
        private readonly ILogger<TransactionsController> _logger;

        public TransactionsController(
            ApplicationDbContext context,
            UserManager<User> userManager,
            ILogger<TransactionsController> logger)
        {
            _context = context;
            _userManager = userManager;
            _logger = logger;
        }

        public async Task<IActionResult> Index(
            int? accountId, 
            int? categoryId, 
            string? type,
            DateTime? fromDate,
            DateTime? toDate,
            string? searchTerm,
            decimal? minAmount,
            decimal? maxAmount,
            int page = 1,
            int pageSize = 20,
            string? sortBy = "Date",
            string? sortDirection = "desc")
        {
            var userId = _userManager.GetUserId(User);
            var query = _context.Transactions
                .Include(t => t.Account)
                .Include(t => t.Category)
                .Where(t => t.Account.UserId == userId);

            // Apply filters
            if (accountId.HasValue)
            {
                query = query.Where(t => t.AccountId == accountId.Value);
            }

            if (categoryId.HasValue)
            {
                query = query.Where(t => t.CategoryId == categoryId.Value);
            }

            if (!string.IsNullOrEmpty(type))
            {
                if (Enum.TryParse<TransactionType>(type, out var transactionType))
                {
                    query = query.Where(t => t.Type == transactionType);
                }
            }

            if (fromDate.HasValue)
            {
                query = query.Where(t => t.Date >= fromDate.Value);
            }

            if (toDate.HasValue)
            {
                query = query.Where(t => t.Date <= toDate.Value);
            }

            if (!string.IsNullOrEmpty(searchTerm))
            {
                query = query.Where(t => t.Description.Contains(searchTerm) || 
                                       (t.Notes != null && t.Notes.Contains(searchTerm)));
            }

            if (minAmount.HasValue)
            {
                query = query.Where(t => t.Amount >= minAmount.Value);
            }

            if (maxAmount.HasValue)
            {
                query = query.Where(t => t.Amount <= maxAmount.Value);
            }

            // Apply sorting
            query = sortBy?.ToLower() switch
            {
                "description" => sortDirection == "asc" 
                    ? query.OrderBy(t => t.Description)
                    : query.OrderByDescending(t => t.Description),
                "amount" => sortDirection == "asc"
                    ? query.OrderBy(t => t.Amount)
                    : query.OrderByDescending(t => t.Amount),
                "category" => sortDirection == "asc"
                    ? query.OrderBy(t => t.Category.Name)
                    : query.OrderByDescending(t => t.Category.Name),
                "account" => sortDirection == "asc"
                    ? query.OrderBy(t => t.Account.Name)
                    : query.OrderByDescending(t => t.Account.Name),
                "type" => sortDirection == "asc"
                    ? query.OrderBy(t => t.Type)
                    : query.OrderByDescending(t => t.Type),
                _ => sortDirection == "asc"
                    ? query.OrderBy(t => t.Date)
                    : query.OrderByDescending(t => t.Date)
            };

            // Get total count for pagination
            var totalRecords = await query.CountAsync();

            // Get filtered summary totals
            var filteredTransactions = await query.ToListAsync();
            var filteredIncome = filteredTransactions.Where(t => t.Type == TransactionType.Income).Sum(t => t.Amount);
            var filteredExpenses = filteredTransactions.Where(t => t.Type == TransactionType.Expense).Sum(t => t.Amount);

            // Apply pagination
            var transactions = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            // Get all transactions for overall summary
            var allTransactions = await _context.Transactions
                .Include(t => t.Account)
                .Where(t => t.Account.UserId == userId)
                .ToListAsync();

            // Get all accounts and categories for filter dropdowns
            var allAccounts = await _context.Accounts
                .Where(a => a.UserId == userId)
                .OrderBy(a => a.Name)
                .ToListAsync();

            var allCategories = await _context.Categories
                .OrderBy(c => c.Name)
                .ToListAsync();

            var viewModel = new TransactionListViewModel
            {
                Transactions = transactions.Select(t => new TransactionViewModel
                {
                    Id = t.Id,
                    Amount = t.Amount,
                    Description = t.Description,
                    Type = t.Type,
                    Date = t.Date,
                    AccountId = t.AccountId,
                    CategoryId = t.CategoryId,
                    AccountName = t.Account?.Name,
                    CategoryName = t.Category?.Name,
                    CreatedAt = t.CreatedAt,
                    Notes = t.Notes,
                    Account = t.Account,
                    Category = t.Category
                }).ToList(),
                
                // Filter properties
                SelectedAccountId = accountId,
                SelectedCategoryId = categoryId,
                SelectedType = type,
                FromDate = fromDate,
                ToDate = toDate,
                SearchTerm = searchTerm,
                MinAmount = minAmount,
                MaxAmount = maxAmount,
                
                // Pagination properties
                CurrentPage = page,
                PageSize = pageSize,
                TotalRecords = totalRecords,
                
                // Sorting properties
                SortBy = sortBy,
                SortDirection = sortDirection,
                
                // Summary properties
                TotalIncome = allTransactions.Where(t => t.Type == TransactionType.Income).Sum(t => t.Amount),
                TotalExpenses = allTransactions.Where(t => t.Type == TransactionType.Expense).Sum(t => t.Amount),
                FilteredIncome = filteredIncome,
                FilteredExpenses = filteredExpenses,
                
                Accounts = allAccounts,
                Categories = allCategories
            };

            return View(viewModel);
        }

        [HttpGet]
        public async Task<IActionResult> Create()
        {
            var userId = _userManager.GetUserId(User);
            
            var model = new CreateTransactionViewModel
            {
                Accounts = await _context.Accounts
                    .Where(a => a.UserId == userId)
                    .ToListAsync(),
                Categories = await _context.Categories
                    .ToListAsync()
            };
            
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreateTransactionViewModel model)
        {
            if (ModelState.IsValid)
            {
                var userId = _userManager.GetUserId(User);
                
                // Verify account belongs to user
                var account = await _context.Accounts
                    .FirstOrDefaultAsync(a => a.Id == model.AccountId && a.UserId == userId);

                if (account == null)
                {
                    ModelState.AddModelError("AccountId", "Invalid account selected.");
                    model.Accounts = await _context.Accounts
                        .Where(a => a.UserId == userId)
                        .ToListAsync();
                    model.Categories = await _context.Categories
                        .ToListAsync();
                    return View(model);
                }

                var transaction = new Transaction
                {
                    Amount = model.Amount,
                    Description = model.Description,
                    Type = Enum.Parse<TransactionType>(model.Type),
                    Date = DateTime.SpecifyKind(model.Date, DateTimeKind.Utc),
                    AccountId = model.AccountId,
                    CategoryId = model.CategoryId,
                    Notes = model.Notes
                };

                _context.Transactions.Add(transaction);

                // Update account balance
                if (model.Type == "Income")
                {
                    account.Balance += model.Amount;
                }
                else
                {
                    account.Balance -= model.Amount;
                }

                account.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                _logger.LogInformation("Transaction created: {Description} - {Amount}", model.Description, model.Amount);
                return RedirectToAction(nameof(Index));
            }

            model.Accounts = await _context.Accounts
                .Where(a => a.UserId == _userManager.GetUserId(User))
                .ToListAsync();
            model.Categories = await _context.Categories
                .ToListAsync();
            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var userId = _userManager.GetUserId(User);
            var transaction = await _context.Transactions
                .Include(t => t.Account)
                .FirstOrDefaultAsync(t => t.Id == id && t.Account.UserId == userId);

            if (transaction == null)
            {
                return NotFound();
            }

            var model = new EditTransactionViewModel
            {
                Id = transaction.Id,
                Amount = transaction.Amount,
                Description = transaction.Description,
                Type = transaction.Type.ToString(),
                Date = transaction.Date,
                AccountId = transaction.AccountId,
                CategoryId = transaction.CategoryId,
                Notes = transaction.Notes,
                Accounts = await _context.Accounts
                    .Where(a => a.UserId == userId)
                    .ToListAsync(),
                Categories = await _context.Categories
                    .ToListAsync()
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, EditTransactionViewModel model)
        {
            if (id != model.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                var userId = _userManager.GetUserId(User);
                var transaction = await _context.Transactions
                    .Include(t => t.Account)
                    .FirstOrDefaultAsync(t => t.Id == id && t.Account.UserId == userId);

                if (transaction == null)
                {
                    return NotFound();
                }

                // Reverse the old transaction's effect on account balance
                if (transaction.Type == TransactionType.Income)
                {
                    transaction.Account.Balance -= transaction.Amount;
                }
                else
                {
                    transaction.Account.Balance += transaction.Amount;
                }

                // Update transaction
                transaction.Amount = model.Amount;
                transaction.Description = model.Description;
                transaction.Type = Enum.Parse<TransactionType>(model.Type);
                transaction.Date = DateTime.SpecifyKind(model.Date, DateTimeKind.Utc);
                transaction.CategoryId = model.CategoryId;
                transaction.Notes = model.Notes;
                transaction.UpdatedAt = DateTime.UtcNow;

                // Apply new transaction's effect on account balance
                if (transaction.Type == TransactionType.Income)
                {
                    transaction.Account.Balance += model.Amount;
                }
                else
                {
                    transaction.Account.Balance -= model.Amount;
                }

                transaction.Account.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();
                _logger.LogInformation("Transaction updated: {Description}", model.Description);
                return RedirectToAction(nameof(Index));
            }

            // Reload data if validation fails
            model.Accounts = await _context.Accounts
                .Where(a => a.UserId == _userManager.GetUserId(User))
                .ToListAsync();
            model.Categories = await _context.Categories
                .ToListAsync();
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var userId = _userManager.GetUserId(User);
            var transaction = await _context.Transactions
                .Include(t => t.Account)
                .FirstOrDefaultAsync(t => t.Id == id && t.Account.UserId == userId);

            if (transaction == null)
            {
                return NotFound();
            }

            // Reverse transaction's effect on account balance
            if (transaction.Type == TransactionType.Income)
            {
                transaction.Account.Balance -= transaction.Amount;
            }
            else
            {
                transaction.Account.Balance += transaction.Amount;
            }

            transaction.Account.UpdatedAt = DateTime.UtcNow;

            _context.Transactions.Remove(transaction);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Transaction deleted: {Description}", transaction.Description);
            return RedirectToAction(nameof(Index));
        }

        private async Task LoadSelectLists()
        {
            var userId = _userManager.GetUserId(User);

            ViewBag.Accounts = new SelectList(
                await _context.Accounts
                    .Where(a => a.UserId == userId)
                    .Select(a => new { a.Id, a.Name })
                    .ToListAsync(),
                "Id", "Name");

            ViewBag.Categories = new SelectList(
                await _context.Categories
                    .Select(c => new { c.Id, c.Name })
                    .ToListAsync(),
                "Id", "Name");
        }
    }
}

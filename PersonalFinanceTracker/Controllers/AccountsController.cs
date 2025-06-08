using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PersonalFinanceTracker.Data;
using PersonalFinanceTracker.Models;
using PersonalFinanceTracker.ViewModels;

namespace PersonalFinanceTracker.Controllers
{
    [Authorize]
    public class AccountsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<User> _userManager;
        private readonly ILogger<AccountsController> _logger;

        public AccountsController(
            ApplicationDbContext context,
            UserManager<User> userManager,
            ILogger<AccountsController> logger)
        {
            _context = context;
            _userManager = userManager;
            _logger = logger;
        }

        public async Task<IActionResult> Index()
        {
            var userId = _userManager.GetUserId(User);
            var accounts = await _context.Accounts
                .Where(a => a.UserId == userId)
                .Select(a => new AccountViewModel
                {
                    Id = a.Id,
                    Name = a.Name,
                    Type = a.Type,
                    Balance = a.Balance,
                    CreatedAt = a.CreatedAt
                })
                .OrderBy(a => a.Name)
                .ToListAsync();

            return View(accounts);
        }

        [HttpGet]
        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreateAccountViewModel model)
        {
            if (ModelState.IsValid)
            {
                var userId = _userManager.GetUserId(User);
                var account = new Account
                {
                    Name = model.Name,
                    Type = Enum.Parse<AccountType>(model.Type),
                    Balance = model.InitialBalance,
                    Description = model.Description,
                    UserId = userId!
                };

                _context.Accounts.Add(account);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Account created: {AccountName}", model.Name);
                return RedirectToAction(nameof(Index));
            }

            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var userId = _userManager.GetUserId(User);
            var account = await _context.Accounts
                .FirstOrDefaultAsync(a => a.Id == id && a.UserId == userId);

            if (account == null)
            {
                return NotFound();
            }

            var model = new EditAccountViewModel
            {
                Id = account.Id,
                Name = account.Name,
                Type = account.Type.ToString(),
                Description = account.Description,
                CurrentBalance = account.Balance
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, EditAccountViewModel model)
        {
            if (id != model.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                var userId = _userManager.GetUserId(User);
                var account = await _context.Accounts
                    .FirstOrDefaultAsync(a => a.Id == id && a.UserId == userId);

                if (account == null)
                {
                    return NotFound();
                }

                account.Name = model.Name;
                account.Type = Enum.Parse<AccountType>(model.Type);
                account.Description = model.Description;
                account.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();
                _logger.LogInformation("Account updated: {AccountName}", model.Name);
                return RedirectToAction(nameof(Index));
            }

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var userId = _userManager.GetUserId(User);
            var account = await _context.Accounts
                .Include(a => a.Transactions)
                .FirstOrDefaultAsync(a => a.Id == id && a.UserId == userId);

            if (account == null)
            {
                return NotFound();
            }

            // Delete all associated transactions first
            if (account.Transactions.Any())
            {
                _context.Transactions.RemoveRange(account.Transactions);
            }

            // Then delete the account
            _context.Accounts.Remove(account);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Account deleted: {AccountName} with {TransactionCount} transactions", 
                account.Name, account.Transactions.Count);
            TempData["Success"] = $"Account '{account.Name}' and all its transactions have been deleted successfully.";
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> Details(int id)
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
                .Select(t => new TransactionViewModel
                {
                    Id = t.Id,
                    Date = t.Date,
                    Description = t.Description,
                    Amount = t.Amount,
                    Type = t.Type,
                    AccountId = t.AccountId,
                    CategoryId = t.CategoryId,
                    CategoryName = t.Category.Name,
                    Notes = t.Notes
                })
                .ToListAsync();

            ViewBag.Account = account;

            return View(transactions);
        }
    }
}

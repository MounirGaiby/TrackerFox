using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PersonalFinanceTracker.Data;
using PersonalFinanceTracker.Models;

namespace PersonalFinanceTracker.Services
{
    public class DataSeedingService
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<User> _userManager;
        private readonly ILogger<DataSeedingService> _logger;

        public DataSeedingService(
            ApplicationDbContext context,
            UserManager<User> userManager,
            ILogger<DataSeedingService> logger)
        {
            _context = context;
            _userManager = userManager;
            _logger = logger;
        }

        public async Task SeedAnalyticsDataAsync(string userEmail)
        {
            try
            {
                // Find the user
                var user = await _userManager.FindByEmailAsync(userEmail);
                if (user == null)
                {
                    _logger.LogWarning("User with email {Email} not found for seeding analytics data", userEmail);
                    return;
                }

                // Check if user already has transactions
                var existingTransactionCount = await _context.Transactions
                    .Include(t => t.Account)
                    .CountAsync(t => t.Account.UserId == user.Id);

                if (existingTransactionCount > 10)
                {
                    _logger.LogInformation("User {Email} already has {Count} transactions, skipping seeding", userEmail, existingTransactionCount);
                    return;
                }

                // Create sample accounts if none exist
                var existingAccounts = await _context.Accounts
                    .Where(a => a.UserId == user.Id)
                    .ToListAsync();

                if (!existingAccounts.Any())
                {
                    var sampleAccounts = new[]
                    {
                        new Account
                        {
                            Name = "Main Checking",
                            Type = AccountType.Checking,
                            Balance = 5000m,
                            Description = "Primary checking account",
                            UserId = user.Id,
                            CreatedAt = DateTime.UtcNow,
                            UpdatedAt = DateTime.UtcNow
                        },
                        new Account
                        {
                            Name = "Savings Account",
                            Type = AccountType.Savings,
                            Balance = 15000m,
                            Description = "Emergency fund savings",
                            UserId = user.Id,
                            CreatedAt = DateTime.UtcNow,
                            UpdatedAt = DateTime.UtcNow
                        }
                    };

                    _context.Accounts.AddRange(sampleAccounts);
                    await _context.SaveChangesAsync();
                    existingAccounts = sampleAccounts.ToList();
                    _logger.LogInformation("Created {Count} sample accounts for user {Email}", sampleAccounts.Length, userEmail);
                }

                // Get the primary account
                var primaryAccount = existingAccounts.First();

                // Create sample transactions from January 1, 2024 to June 30, 2025
                var transactions = new List<Transaction>();
                var random = new Random(42); // Seed for consistent data
                var startDate = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
                var endDate = new DateTime(2025, 6, 30, 0, 0, 0, DateTimeKind.Utc);

                // Generate dates for all months from start to end
                var monthlyDates = new List<DateTime>();
                var currentDate = startDate;
                while (currentDate <= endDate)
                {
                    monthlyDates.Add(currentDate);
                    currentDate = currentDate.AddMonths(1);
                }

                // Monthly salary (Salary category = 1)
                foreach (var monthDate in monthlyDates)
                {
                    var salaryDate = new DateTime(monthDate.Year, monthDate.Month, 1, 0, 0, 0, DateTimeKind.Utc);
                    transactions.Add(new Transaction
                    {
                        Amount = 5000m + (random.Next(-500, 1000)), // $4500-$6000 salary
                        Description = "Monthly Salary",
                        Type = TransactionType.Income,
                        Date = salaryDate,
                        AccountId = primaryAccount.Id,
                        CategoryId = 1, // Salary
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    });
                }

                // Rent payments (Rent category = 4)
                foreach (var monthDate in monthlyDates)
                {
                    var rentDate = new DateTime(monthDate.Year, monthDate.Month, 3, 0, 0, 0, DateTimeKind.Utc);
                    transactions.Add(new Transaction
                    {
                        Amount = 1200m,
                        Description = "Monthly Rent",
                        Type = TransactionType.Expense,
                        Date = rentDate,
                        AccountId = primaryAccount.Id,
                        CategoryId = 4, // Rent
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    });
                }

                // Utilities (Utilities category = 6)
                foreach (var monthDate in monthlyDates)
                {
                    var utilityDate = new DateTime(monthDate.Year, monthDate.Month, 5, 0, 0, 0, DateTimeKind.Utc);
                    transactions.Add(new Transaction
                    {
                        Amount = 150m + random.Next(-50, 100), // $100-$250
                        Description = "Utilities Bill",
                        Type = TransactionType.Expense,
                        Date = utilityDate,
                        AccountId = primaryAccount.Id,
                        CategoryId = 6, // Utilities
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    });
                }

                // Groceries (Groceries category = 7) - Weekly
                var currentWeekStart = startDate;
                while (currentWeekStart <= endDate)
                {
                    var groceryDate = currentWeekStart.AddDays(random.Next(0, 7));
                    if (groceryDate <= endDate)
                    {
                        transactions.Add(new Transaction
                        {
                            Amount = 80m + random.Next(-30, 50), // $50-$130
                            Description = random.Next(2) == 0 ? "Grocery Store" : "Supermarket Shopping",
                            Type = TransactionType.Expense,
                            Date = groceryDate,
                            AccountId = primaryAccount.Id,
                            CategoryId = 7, // Groceries
                            CreatedAt = DateTime.UtcNow,
                            UpdatedAt = DateTime.UtcNow
                        });
                    }
                    currentWeekStart = currentWeekStart.AddDays(7);
                }

                // Dining Out (Dining Out category = 8) - Bi-weekly
                var currentDiningDate = startDate;
                while (currentDiningDate <= endDate)
                {
                    transactions.Add(new Transaction
                    {
                        Amount = 35m + random.Next(-15, 40), // $20-$75
                        Description = GetRandomRestaurant(random),
                        Type = TransactionType.Expense,
                        Date = currentDiningDate,
                        AccountId = primaryAccount.Id,
                        CategoryId = 8, // Dining Out
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    });
                    currentDiningDate = currentDiningDate.AddDays(14);
                }

                // Transportation (Transportation category = 9)
                foreach (var monthDate in monthlyDates)
                {
                    // Gas
                    var gasDate = new DateTime(monthDate.Year, monthDate.Month, 10, 0, 0, 0, DateTimeKind.Utc);
                    transactions.Add(new Transaction
                    {
                        Amount = 60m + random.Next(-20, 40), // $40-$100
                        Description = "Gas Station",
                        Type = TransactionType.Expense,
                        Date = gasDate,
                        AccountId = primaryAccount.Id,
                        CategoryId = 9, // Transportation
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    });
                }

                // Entertainment (Entertainment category = 10)
                foreach (var monthDate in monthlyDates)
                {
                    var entDate = new DateTime(monthDate.Year, monthDate.Month, 15, 0, 0, 0, DateTimeKind.Utc);
                    transactions.Add(new Transaction
                    {
                        Amount = 25m + random.Next(-10, 50), // $15-$75
                        Description = GetRandomEntertainment(random),
                        Type = TransactionType.Expense,
                        Date = entDate,
                        AccountId = primaryAccount.Id,
                        CategoryId = 10, // Entertainment
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    });
                }

                // Healthcare (Healthcare category = 11) - Quarterly
                var currentQuarterDate = startDate;
                while (currentQuarterDate <= endDate)
                {
                    var healthDate = new DateTime(currentQuarterDate.Year, currentQuarterDate.Month, 20, 0, 0, 0, DateTimeKind.Utc);
                    if (healthDate <= endDate)
                    {
                        transactions.Add(new Transaction
                        {
                            Amount = 150m + random.Next(-50, 200), // $100-$350
                            Description = "Doctor Visit",
                            Type = TransactionType.Expense,
                            Date = healthDate,
                            AccountId = primaryAccount.Id,
                            CategoryId = 11, // Healthcare
                            CreatedAt = DateTime.UtcNow,
                            UpdatedAt = DateTime.UtcNow
                        });
                    }
                    currentQuarterDate = currentQuarterDate.AddMonths(3);
                }

                // Shopping (Shopping category = 12)
                foreach (var monthDate in monthlyDates)
                {
                    if (random.Next(3) == 0) // ~33% of months
                    {
                        var shopDate = new DateTime(monthDate.Year, monthDate.Month, 25, 0, 0, 0, DateTimeKind.Utc);
                        transactions.Add(new Transaction
                        {
                            Amount = 100m + random.Next(-50, 150), // $50-$250
                            Description = "Clothing Store",
                            Type = TransactionType.Expense,
                            Date = shopDate,
                            AccountId = primaryAccount.Id,
                            CategoryId = 12, // Shopping
                            CreatedAt = DateTime.UtcNow,
                            UpdatedAt = DateTime.UtcNow
                        });
                    }
                }

                // Add some freelance income (Freelance category = 2) - Quarterly
                var currentFreelanceDate = startDate;
                while (currentFreelanceDate <= endDate)
                {
                    if (random.Next(2) == 0) // 50% chance per quarter
                    {
                        var freelanceDate = new DateTime(currentFreelanceDate.Year, currentFreelanceDate.Month, 15, 0, 0, 0, DateTimeKind.Utc);
                        if (freelanceDate <= endDate)
                        {
                            transactions.Add(new Transaction
                            {
                                Amount = 800m + random.Next(-300, 1200), // $500-$2000
                                Description = "Freelance Project",
                                Type = TransactionType.Income,
                                Date = freelanceDate,
                                AccountId = primaryAccount.Id,
                                CategoryId = 2, // Freelance
                                CreatedAt = DateTime.UtcNow,
                                UpdatedAt = DateTime.UtcNow
                            });
                        }
                    }
                    currentFreelanceDate = currentFreelanceDate.AddMonths(3);
                }

                // Add transactions to context
                _context.Transactions.AddRange(transactions);
                await _context.SaveChangesAsync();

                // Update account balances based on transactions
                foreach (var account in existingAccounts)
                {
                    var accountTransactions = transactions.Where(t => t.AccountId == account.Id);
                    var incomeTotal = accountTransactions
                        .Where(t => t.Type == TransactionType.Income)
                        .Sum(t => t.Amount);
                    var expenseTotal = accountTransactions
                        .Where(t => t.Type == TransactionType.Expense)
                        .Sum(t => t.Amount);
                    
                    account.Balance = incomeTotal - expenseTotal;
                    account.UpdatedAt = DateTime.UtcNow;
                }

                await _context.SaveChangesAsync();

                _logger.LogInformation("Successfully seeded {Count} transactions for user {Email} from January 2024 to June 2025", 
                    transactions.Count, userEmail);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error seeding analytics data for user {Email}", userEmail);
                throw;
            }
        }

        private static string GetRandomRestaurant(Random random)
        {
            var restaurants = new[] { 
                "Pizza Palace", "Burger Joint", "Sushi Bar", "Taco Express", 
                "Italian Bistro", "Coffee Shop", "Thai Garden", "Steakhouse" 
            };
            return restaurants[random.Next(restaurants.Length)];
        }

        private static string GetRandomEntertainment(Random random)
        {
            var entertainment = new[] { 
                "Movie Theater", "Netflix Subscription", "Spotify Premium", "Concert Tickets", 
                "Gaming Store", "Theme Park", "Bowling Alley", "Mini Golf" 
            };
            return entertainment[random.Next(entertainment.Length)];
        }
    }
}

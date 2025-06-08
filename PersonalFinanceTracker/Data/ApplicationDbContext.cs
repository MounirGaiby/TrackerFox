using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using PersonalFinanceTracker.Models;

namespace PersonalFinanceTracker.Data
{
    public class ApplicationDbContext : IdentityDbContext<User>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<Account> Accounts { get; set; }
        public DbSet<Category> Categories { get; set; }
        public DbSet<Transaction> Transactions { get; set; }
        public DbSet<BudgetGoalModel> BudgetGoals { get; set; }
        public DbSet<Conversation> Conversations { get; set; }
        public DbSet<ChatMessage> ChatMessages { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // Configure decimal precision
            builder.Entity<Account>()
                .Property(a => a.Balance)
                .HasPrecision(18, 2);

            builder.Entity<Transaction>()
                .Property(t => t.Amount)
                .HasPrecision(18, 2);

            builder.Entity<BudgetGoalModel>()
                .Property(g => g.Amount)
                .HasPrecision(18, 2);

            // Configure relationships
            builder.Entity<Account>()
                .HasOne(a => a.User)
                .WithMany(u => u.Accounts)
                .HasForeignKey(a => a.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<Transaction>()
                .HasOne(t => t.Account)
                .WithMany(a => a.Transactions)
                .HasForeignKey(t => t.AccountId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<Transaction>()
                .HasOne(t => t.Category)
                .WithMany(c => c.Transactions)
                .HasForeignKey(t => t.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<BudgetGoalModel>()
                .HasOne<Category>()
                .WithMany()
                .HasForeignKey(g => g.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);

            // Configure Conversation and ChatMessage relationships
            builder.Entity<Conversation>()
                .HasMany(c => c.Messages)
                .WithOne(m => m.Conversation)
                .HasForeignKey(m => m.ConversationId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<ChatMessage>()
                .Property(m => m.Role)
                .HasConversion<string>();

            builder.Entity<ChatMessage>()
                .Property(m => m.ContextType)
                .HasConversion<string>();

            // Seed default categories
            SeedCategories(builder);
        }

        private void SeedCategories(ModelBuilder builder)
        {
            // Use static date for seeding to avoid migration issues
            var seedDate = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            
            var categories = new[]
            {
                new Category { Id = 1, Name = "Salary", Description = "Employment income", IsDefault = true, CreatedAt = seedDate },
                new Category { Id = 2, Name = "Freelance", Description = "Freelance work income", IsDefault = true, CreatedAt = seedDate },
                new Category { Id = 3, Name = "Investment", Description = "Investment returns", IsDefault = true, CreatedAt = seedDate },
                new Category { Id = 4, Name = "Rent", Description = "Housing rent payments", IsDefault = true, CreatedAt = seedDate },
                new Category { Id = 5, Name = "Mortgage", Description = "Home mortgage payments", IsDefault = true, CreatedAt = seedDate },
                new Category { Id = 6, Name = "Utilities", Description = "Electricity, water, gas, internet", IsDefault = true, CreatedAt = seedDate },
                new Category { Id = 7, Name = "Groceries", Description = "Food and household items", IsDefault = true, CreatedAt = seedDate },
                new Category { Id = 8, Name = "Dining Out", Description = "Restaurants and takeout", IsDefault = true, CreatedAt = seedDate },
                new Category { Id = 9, Name = "Transportation", Description = "Gas, public transport, car maintenance", IsDefault = true, CreatedAt = seedDate },
                new Category { Id = 10, Name = "Entertainment", Description = "Movies, games, subscriptions", IsDefault = true, CreatedAt = seedDate },
                new Category { Id = 11, Name = "Healthcare", Description = "Medical expenses and insurance", IsDefault = true, CreatedAt = seedDate },
                new Category { Id = 12, Name = "Shopping", Description = "Clothing and personal items", IsDefault = true, CreatedAt = seedDate },
                new Category { Id = 13, Name = "Education", Description = "Courses, books, training", IsDefault = true, CreatedAt = seedDate },
                new Category { Id = 14, Name = "Insurance", Description = "Life, auto, health insurance", IsDefault = true, CreatedAt = seedDate },
                new Category { Id = 15, Name = "Other Income", Description = "Miscellaneous income", IsDefault = true, CreatedAt = seedDate },
                new Category { Id = 16, Name = "Other Expense", Description = "Miscellaneous expenses", IsDefault = true, CreatedAt = seedDate }
            };

            builder.Entity<Category>().HasData(categories);
        }
    }
}

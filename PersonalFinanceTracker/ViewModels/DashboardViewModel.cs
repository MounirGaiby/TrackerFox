namespace PersonalFinanceTracker.ViewModels
{
    public class DashboardViewModel
    {
        public decimal TotalBalance { get; set; }
        public decimal MonthlyIncome { get; set; }
        public decimal MonthlyExpenses { get; set; }
        public decimal NetIncome => MonthlyIncome - MonthlyExpenses;
        
        public IEnumerable<AccountViewModel> Accounts { get; set; } = new List<AccountViewModel>();
        public IEnumerable<TransactionViewModel> RecentTransactions { get; set; } = new List<TransactionViewModel>();
        public IEnumerable<CategorySpendingViewModel> CategorySpending { get; set; } = new List<CategorySpendingViewModel>();
    }

    public class CategorySpendingViewModel
    {
        public string CategoryName { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public decimal Percentage { get; set; }
    }
}

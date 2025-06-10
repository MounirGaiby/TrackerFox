using PersonalFinanceTracker.Models;
using System;
using System.Collections.Generic;

namespace PersonalFinanceTracker.ViewModels
{
    public class AnalyticsViewModel
    {
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public int? AccountId { get; set; }
        public List<AccountViewModel> AvailableAccounts { get; set; } = new List<AccountViewModel>();

        public decimal TotalIncome { get; set; }
        public decimal TotalExpenses { get; set; }
        public decimal NetFlow { get; set; }
        public int TotalTransactions { get; set; }

        public List<Transaction> RecentTransactions { get; set; } = new List<Transaction>();

        public ChartDataViewModel<List<ChartDataItem<decimal>>> SpendingByCategoryChart { get; set; } = new ChartDataViewModel<List<ChartDataItem<decimal>>>();
        public ChartDataViewModel<IncomeExpenseData> IncomeVsExpenseChart { get; set; } = new ChartDataViewModel<IncomeExpenseData>();
        public ChartDataViewModel<object> CashFlowChart { get; set; } = new ChartDataViewModel<object>(); // Using object for flexibility with chart.js structure
    }

    public class ChartDataViewModel<T>
    {
        public T Data { get; set; }
    }

    public class ChartDataItem<T>
    {
        public string CategoryName { get; set; } = string.Empty;
        public T? TotalAmount { get; set; } // Made nullable for reference types
    }
    
    public class IncomeExpenseData
    {
        public List<string> Labels { get; set; } = new List<string>();
        public List<decimal> IncomeData { get; set; } = new List<decimal>();
        public List<decimal> ExpenseData { get; set; } = new List<decimal>();
    }
}


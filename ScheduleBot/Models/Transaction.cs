using System.ComponentModel.DataAnnotations;

namespace ScheduleBot.Models;

public class Wallet
{
    [Key]
    public Guid Id { get; set; }
    public string? Name { get; set; }
    public Guid CreatorId { get; set; }
}

public class Category
{
    [Key]
    public Guid Id { get; set; }
    public Guid WalletId { get; set; }
    public DateTime CreateTime { get; set; }
    public string? Name { get; set; }
    public bool TempAdded { get; set; }
    public bool TempDeleted { get; set; }
}

public class WalletAccess
{
    [Key]
    public Guid Id { get; set; }
    public Guid WalletId { get; set; }
    public Guid UserId { get; set; }
}

public class TransactionRecord
{
    [Key]
    public Guid Id { get; set; }
    public Guid WalletId { get; set; }
    public Guid CategoryId { get; set; }
    public Guid ConsumerId { get; set; }
    public DateTime Date { get; set; }
    public decimal Deposit { get; set; }
    public decimal Withdraw { get; set; }
    public decimal BalanceAfter { get; set; }
    public long? DocumentNo { get; set; }
    public string? Title { get; set; }
}

public class TransactionProcess
{
    public int Index { get; set; }
    public DateTime Date { get; set; }
    public string Type { get; set; }
    public string Description { get; set; }
    public long DocumentNo { get; set; }
    public decimal Deposit { get; set; }
    public decimal Withdraw { get; set; }
    public decimal BalanceAfter { get; set; }
    public Guid CategoryId { get; set; }
    public string CategoryName { get; set; }
    public string Title { get; set; }
    public bool Processed { get; set; }
}

public class ReportData
{
    public Wallet Wallet { get; set; }
    public List<Category> Categories { get; set; }
    public List<TransactionRecord> Transactions { get; set; }
    public List<User> Users { get; set; }
    public Dictionary<Guid, List<TransactionRecord>> TransactionsByCategory { get; set; }
    public Dictionary<Guid, User> UserMap { get; set; }
    public Dictionary<Guid, Category> CategoryMap { get; set; }
}

public class WalletReport
{
    public string WalletName { get; set; }
    public DateTime GeneratedAt { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public int TotalTransactions { get; set; }
    
    // Summary
    public ReportSummary Summary { get; set; }
    
    // Category Reports
    public List<CategoryReport> CategoryReports { get; set; }
    
    // Monthly Reports
    public List<MonthlyReport> MonthlyReports { get; set; }
    
    // User Reports
    public List<UserReport> UserReports { get; set; }
    
    // Transaction Details
    public IEnumerable<TransactionRecord> Transactions { get; set; }
}

public class ReportSummary
{
    public decimal TotalDeposits { get; set; }
    public decimal TotalWithdrawals { get; set; }
    public decimal NetCashFlow { get; set; }
    public int DepositCount { get; set; }
    public int WithdrawCount { get; set; }
    public decimal OpeningBalance { get; set; }
    public decimal ClosingBalance { get; set; }
    public decimal LargestDeposit { get; set; }
    public decimal LargestWithdrawal { get; set; }
    public decimal AverageTransaction { get; set; }
    public decimal AverageDeposit { get; set; }
    public decimal AverageWithdrawal { get; set; }
    public DateTime? EarliestTransaction { get; set; }
    public DateTime? LatestTransaction { get; set; }
}

public class CategoryReport
{
    public Guid CategoryId { get; set; }
    public string CategoryName { get; set; }
    public int TransactionCount { get; set; }
    public decimal TotalDeposits { get; set; }
    public decimal TotalWithdrawals { get; set; }
    public decimal NetCashFlow { get; set; }
    public decimal PercentageOfTotalWithdrawals { get; set; }
    public decimal PercentageOfTotalDeposits { get; set; }
    public decimal AverageTransaction { get; set; }
    public decimal LargestTransaction { get; set; }
    public DateTime? FirstTransaction { get; set; }
    public DateTime? LastTransaction { get; set; }
}

public class MonthlyReport
{
    public int Year { get; set; }
    public int Month { get; set; }
    public string MonthName { get; set; }
    public int TransactionCount { get; set; }
    public decimal Deposits { get; set; }
    public decimal Withdrawals { get; set; }
    public decimal NetCashFlow { get; set; }
    public decimal ClosingBalance { get; set; }
}

public class UserReport
{
    public Guid UserId { get; set; }
    public string UserName { get; set; }
    public int TransactionCount { get; set; }
    public decimal Deposits { get; set; }
    public decimal Withdrawals { get; set; }
    public decimal NetCashFlow { get; set; }
}
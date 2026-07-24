using System.ComponentModel.DataAnnotations;

namespace ScheduleBot.Models;

public class TransactionRecord
{
    [Key]
    public Guid Id { get; set; }
    public Guid ConsumerId { get; set; }
    public DateTime Date { get; set; }
    public bool IsDeposit { get; set; }
    public long DocumentNo { get; set; }
    public string? Title { get; set; }
    public decimal Amount { get; set; }
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
    public bool Processed { get; set; }
}

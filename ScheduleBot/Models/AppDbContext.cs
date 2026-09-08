using Microsoft.EntityFrameworkCore;

namespace ScheduleBot.Models;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users { get; set; }
    public DbSet<CycleDetail> CycleDetails { get; set; }
    public DbSet<CycleHistory> CycleHistories { get; set; }
    public DbSet<Cart> Cart { get; set; }
    public DbSet<CartItem> CartItem { get; set; }
    public DbSet<CartAccess> CartAccess { get; set; }
    
    public DbSet<Wallet>  Wallet { get; set; }
    public DbSet<Category> WalletCategory { get; set; }
    public DbSet<WalletAccess> WalletAccess { get; set; }
    public DbSet<TransactionRecord> WalletTransactions { get; set; }
    
    public DbSet<Notification> Notification { get; set; }
    public DbSet<NotificationAccess> NotificationAccess { get; set; }
    public DbSet<Future> NotificationFutureMessage { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Notification>()
            .HasIndex(x => new { x.SpecialBehavior, x.SpecialBehaviorTargetId })
            .IsUnique()
            .HasFilter("[SpecialBehaviorTargetId] IS NOT NULL");

        modelBuilder.Entity<NotificationAccess>()
            .HasIndex(x => new { x.NotificationId, x.UserId })
            .IsUnique();
    }
}

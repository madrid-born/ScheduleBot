using Microsoft.EntityFrameworkCore;

namespace ScheduleBot.Models;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users { get; set; }
    public DbSet<CycleDetail> CycleDetails { get; set; }
    public DbSet<CycleHistory> CycleHistories { get; set; }
    public DbSet<CycleNotify> CycleNotifies { get; set; }
    public DbSet<Cart> Cart { get; set; }
    public DbSet<CartItem> CartItem { get; set; }
    public DbSet<CartAccess> CartAccess { get; set; }
}
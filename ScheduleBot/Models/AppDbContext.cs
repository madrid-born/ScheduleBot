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
    
    public DbSet<MapifyMap> MapifyMaps { get; set; }
    public DbSet<MapifyMapAccess> MapifyMapAccesses { get; set; }
    public DbSet<MapifyCategory> MapifyCategories { get; set; }
    public DbSet<MapifyLocation> MapifyLocations { get; set; }
    public DbSet<MapifyLocationCategory> MapifyLocationCategories { get; set; }
    
    public DbSet<Survey> Survey { get; set; }
    public DbSet<SurveyAccess> SurveyAccess { get; set; }
    public DbSet<Question> SurveyQuestion { get; set; }
    public DbSet<Answer> SurveyAnswer { get; set; }
    public DbSet<UserAnswer> SurveyUserAnswer { get; set; }
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Survey>().Property(x => x.Name).HasMaxLength(200);
        modelBuilder.Entity<Survey>().HasIndex(x => x.InvitationCode).IsUnique();
        modelBuilder.Entity<Survey>().HasOne<User>().WithMany().HasForeignKey(x => x.CreatorId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<SurveyAccess>().HasIndex(x => new { x.SurveyId, x.UserId }).IsUnique();
        modelBuilder.Entity<SurveyAccess>().HasOne<Survey>().WithMany().HasForeignKey(x => x.SurveyId);
        modelBuilder.Entity<SurveyAccess>().HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<Question>().HasIndex(x => new { x.SurveyId, x.Position }).IsUnique();
        modelBuilder.Entity<Question>().HasOne<Survey>().WithMany().HasForeignKey(x => x.SurveyId);
        modelBuilder.Entity<Question>().Property(x => x.Title).HasMaxLength(1000);
        modelBuilder.Entity<Question>().Property(x => x.RightAnswer).HasMaxLength(1000);
        modelBuilder.Entity<Question>().Property(x => x.DataType).HasMaxLength(20);
        modelBuilder.Entity<Question>().Property(x => x.StateOneName).HasMaxLength(50);
        modelBuilder.Entity<Question>().Property(x => x.StateTwoName).HasMaxLength(50);
        modelBuilder.Entity<Question>().ToTable(table => table.HasCheckConstraint("CK_SurveyQuestion_States",
            "([StateOneName] IS NULL AND [StateTwoName] IS NULL) OR ([StateOneName] IS NOT NULL AND [StateTwoName] IS NOT NULL)"));
        modelBuilder.Entity<Answer>().HasOne<Question>().WithMany().HasForeignKey(x => x.QuestionId);
        modelBuilder.Entity<Answer>().HasIndex(x => new { x.QuestionId, x.Position }).IsUnique();
        modelBuilder.Entity<Answer>().Property(x => x.Value).HasMaxLength(100);
        modelBuilder.Entity<Answer>().Property(x => x.DataType).HasMaxLength(20);
        modelBuilder.Entity<UserAnswer>().HasIndex(x => new { x.UserId, x.SurveyId, x.QuestionId, x.StateIndex }).IsUnique();
        modelBuilder.Entity<UserAnswer>().HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<UserAnswer>().HasOne<Survey>().WithMany().HasForeignKey(x => x.SurveyId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<UserAnswer>().HasOne<Question>().WithMany().HasForeignKey(x => x.QuestionId);
        modelBuilder.Entity<UserAnswer>().HasOne<Answer>().WithMany().HasForeignKey(x => x.AnswerId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<UserAnswer>().Property(x => x.Value).HasMaxLength(1000);
        modelBuilder.Entity<UserAnswer>().ToTable(table => table.HasCheckConstraint("CK_SurveyUserAnswer_StateIndex", "[StateIndex] IN (0, 1)"));

        modelBuilder.Entity<Notification>()
            .HasIndex(x => new { x.SpecialBehavior, x.SpecialBehaviorTargetId })
            .IsUnique()
            .HasFilter("[SpecialBehaviorTargetId] IS NOT NULL");

        modelBuilder.Entity<NotificationAccess>()
            .HasIndex(x => new { x.NotificationId, x.UserId })
            .IsUnique();

        modelBuilder.Entity<MapifyMapAccess>()
            .HasIndex(x => new { x.MapId, x.UserId })
            .IsUnique();

        modelBuilder.Entity<MapifyLocationCategory>()
            .HasIndex(x => new { x.LocationId, x.CategoryId })
            .IsUnique();

        modelBuilder.Entity<MapifyLocation>()
            .Property(x => x.Score)
            .HasPrecision(3, 1);
    }
}

using Microsoft.EntityFrameworkCore;

namespace ScheduleBot.Models;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<ScoreGroup> ScoreGroups { get; set; }
    public DbSet<ScoreMember> ScoreMembers { get; set; }
    public DbSet<ScoreRequest> ScoreRequests { get; set; }
    public DbSet<ScoreApproval> ScoreApprovals { get; set; }
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

    public DbSet<Place> TeaMapPlaces { get; set; }
    public DbSet<TehranLearningNight> TeaMapLearningNights { get; set; }
    public DbSet<PlaceAlias> TeaMapPlaceAliases { get; set; }
    public DbSet<PlaceRelationship> TeaMapPlaceRelationships { get; set; }
    public DbSet<Lesson> TeaMapLessons { get; set; }
    public DbSet<LessonPlace> TeaMapLessonPlaces { get; set; }
    public DbSet<UserPlaceLearningProgress> TeaMapUserPlaceLearningProgress { get; set; }
    public DbSet<Quiz> TeaMapQuizzes { get; set; }
    public DbSet<QuizQuestion> TeaMapQuizQuestions { get; set; }
    public DbSet<QuizQuestionOption> TeaMapQuizQuestionOptions { get; set; }
    public DbSet<QuizAnswer> TeaMapQuizAnswers { get; set; }
    public DbSet<ScoreTransaction> TeaMapScoreTransactions { get; set; }
    public DbSet<TehranGameProfile> TeaMapTehranGameProfiles { get; set; }
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<ScoreGroup>().Property(x => x.Name).HasMaxLength(200);
        modelBuilder.Entity<ScoreGroup>().Property(x => x.Revision).IsConcurrencyToken();
        modelBuilder.Entity<ScoreGroup>().HasIndex(x => x.InvitationCode).IsUnique();
        modelBuilder.Entity<ScoreGroup>().HasOne<User>().WithMany().HasForeignKey(x => x.CreatorId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<ScoreMember>().HasIndex(x => new { x.GroupId, x.UserId }).IsUnique();
        modelBuilder.Entity<ScoreMember>().HasOne<ScoreGroup>().WithMany().HasForeignKey(x => x.GroupId);
        modelBuilder.Entity<ScoreMember>().HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<ScoreRequest>().HasIndex(x => new { x.GroupId, x.CreatedAtUtc });
        modelBuilder.Entity<ScoreRequest>().HasOne<ScoreGroup>().WithMany().HasForeignKey(x => x.GroupId);
        modelBuilder.Entity<ScoreRequest>().HasOne<User>().WithMany().HasForeignKey(x => x.InitiatorId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<ScoreRequest>().HasOne<User>().WithMany().HasForeignKey(x => x.FromUserId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<ScoreRequest>().HasOne<User>().WithMany().HasForeignKey(x => x.ToUserId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<ScoreRequest>().ToTable(t => t.HasCheckConstraint("CK_ScoreRequest_Amount", "[Amount] > 0"));
        modelBuilder.Entity<ScoreApproval>().HasIndex(x => new { x.RequestId, x.UserId }).IsUnique();
        modelBuilder.Entity<ScoreApproval>().HasOne<ScoreRequest>().WithMany().HasForeignKey(x => x.RequestId);
        modelBuilder.Entity<ScoreApproval>().HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);

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

        modelBuilder.Entity<Place>().Property(x => x.Latitude).HasPrecision(9, 6);
        modelBuilder.Entity<Place>().Property(x => x.Longitude).HasPrecision(9, 6);
        modelBuilder.Entity<PlaceRelationship>().Property(x => x.DistanceMeters).HasPrecision(12, 2);
        modelBuilder.Entity<Place>().HasIndex(x => new { x.Source, x.ExternalId })
            .IsUnique().HasFilter("[ExternalId] IS NOT NULL");
        modelBuilder.Entity<Place>().HasIndex(x => new { x.IsActive, x.Priority });
        modelBuilder.Entity<Place>().ToTable(table => table.HasCheckConstraint(
            "CK_Place_Priority", "[Priority] >= 0 AND [Priority] <= 100"));
        modelBuilder.Entity<Place>().ToTable(table => table.HasCheckConstraint(
            "CK_Place_Latitude", "[Latitude] >= -90 AND [Latitude] <= 90"));
        modelBuilder.Entity<Place>().ToTable(table => table.HasCheckConstraint(
            "CK_Place_Longitude", "[Longitude] >= -180 AND [Longitude] <= 180"));

        modelBuilder.Entity<PlaceAlias>().HasIndex(x => new { x.PlaceId, x.Name, x.LanguageCode }).IsUnique();
        modelBuilder.Entity<PlaceAlias>().HasOne<Place>().WithMany().HasForeignKey(x => x.PlaceId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<PlaceRelationship>().HasIndex(x =>
            new { x.FromPlaceId, x.ToPlaceId, x.RelationshipType }).IsUnique();
        modelBuilder.Entity<PlaceRelationship>().HasOne<Place>().WithMany().HasForeignKey(x => x.FromPlaceId)
            .OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<PlaceRelationship>().HasOne<Place>().WithMany().HasForeignKey(x => x.ToPlaceId)
            .OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<PlaceRelationship>().ToTable(table => table.HasCheckConstraint(
            "CK_PlaceRelationship_Confidence", "[Confidence] IS NULL OR ([Confidence] >= 0 AND [Confidence] <= 100)"));

        modelBuilder.Entity<Lesson>().HasIndex(x => new { x.IsPublished, x.Position });
        modelBuilder.Entity<Lesson>().ToTable(table => table.HasCheckConstraint(
            "CK_Lesson_Difficulty", "[Difficulty] >= 1 AND [Difficulty] <= 5"));
        modelBuilder.Entity<LessonPlace>().HasIndex(x => new { x.LessonId, x.PlaceId }).IsUnique();
        modelBuilder.Entity<LessonPlace>().HasIndex(x => new { x.LessonId, x.Position }).IsUnique();
        modelBuilder.Entity<LessonPlace>().HasOne<Lesson>().WithMany().HasForeignKey(x => x.LessonId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<LessonPlace>().HasOne<Place>().WithMany().HasForeignKey(x => x.PlaceId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<UserPlaceLearningProgress>().HasIndex(x => new { x.UserId, x.PlaceId }).IsUnique();
        modelBuilder.Entity<UserPlaceLearningProgress>().HasOne<User>().WithMany().HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<UserPlaceLearningProgress>().HasOne<Place>().WithMany().HasForeignKey(x => x.PlaceId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Quiz>().HasIndex(x => new { x.UserId, x.Status });
        modelBuilder.Entity<Quiz>().HasIndex(x => x.NightId).IsUnique().HasFilter("[NightId] IS NOT NULL");
        modelBuilder.Entity<Quiz>().HasOne<TehranLearningNight>().WithMany().HasForeignKey(x => x.NightId)
            .OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<TehranLearningNight>().HasIndex(x => new { x.UserId, x.LocalDate }).IsUnique();
        modelBuilder.Entity<TehranLearningNight>().HasOne<User>().WithMany().HasForeignKey(x => x.UserId);
        modelBuilder.Entity<TehranLearningNight>().HasOne<Place>().WithMany().HasForeignKey(x => x.FirstPlaceId)
            .OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<TehranLearningNight>().HasOne<Place>().WithMany().HasForeignKey(x => x.SecondPlaceId)
            .OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<TehranLearningNight>().ToTable(t => t.HasCheckConstraint("CK_TeaMapLearningNight_Count", "[LearntCount] BETWEEN 0 AND 2"));
        modelBuilder.Entity<Quiz>().HasOne<User>().WithMany().HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<Quiz>().HasOne<Lesson>().WithMany().HasForeignKey(x => x.LessonId)
            .OnDelete(DeleteBehavior.SetNull);
        modelBuilder.Entity<QuizQuestion>().HasIndex(x => new { x.QuizId, x.Position }).IsUnique();
        modelBuilder.Entity<QuizQuestion>().HasOne<Quiz>().WithMany().HasForeignKey(x => x.QuizId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<QuizQuestion>().HasOne<Place>().WithMany().HasForeignKey(x => x.PlaceId)
            .OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<QuizQuestion>().HasOne<Place>().WithMany().HasForeignKey(x => x.ReferencePlaceId)
            .OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<QuizQuestionOption>().HasIndex(x => new { x.QuizQuestionId, x.Position }).IsUnique();
        modelBuilder.Entity<QuizQuestionOption>().HasOne<QuizQuestion>().WithMany().HasForeignKey(x => x.QuizQuestionId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<QuizAnswer>().HasIndex(x => new { x.QuizQuestionId, x.UserId }).IsUnique();
        modelBuilder.Entity<QuizAnswer>().HasOne<QuizQuestion>().WithMany().HasForeignKey(x => x.QuizQuestionId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<QuizAnswer>().HasOne<QuizQuestionOption>().WithMany().HasForeignKey(x => x.SelectedOptionId)
            .OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<QuizAnswer>().HasOne<User>().WithMany().HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<ScoreTransaction>().HasIndex(x => new { x.UserId, x.CreatedAtUtc });
        modelBuilder.Entity<ScoreTransaction>().HasOne<User>().WithMany().HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<TehranGameProfile>().HasIndex(x => x.UserId).IsUnique();
        modelBuilder.Entity<TehranGameProfile>().HasOne<User>().WithMany().HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

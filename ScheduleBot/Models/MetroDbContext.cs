using Microsoft.EntityFrameworkCore;

namespace ScheduleBot.Models;

public sealed class MetroDbContext(DbContextOptions<MetroDbContext> options) : DbContext(options)
{
    public DbSet<MetroStation> Stations => Set<MetroStation>();
    public DbSet<MetroLine> Lines => Set<MetroLine>();
    public DbSet<MetroStationLine> StationLines => Set<MetroStationLine>();
    public DbSet<MetroRoute> Routes => Set<MetroRoute>();
    public DbSet<MetroRouteStop> RouteStops => Set<MetroRouteStop>();
    public DbSet<MetroSegment> Segments => Set<MetroSegment>();
    public DbSet<MetroTransferRule> TransferRules => Set<MetroTransferRule>();
    public DbSet<MetroTrip> Trips => Set<MetroTrip>();
    public DbSet<MetroStopTime> StopTimes => Set<MetroStopTime>();
    public DbSet<MetroHolidayDate> HolidayDates => Set<MetroHolidayDate>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<MetroStation>().ToTable("Stations").HasKey(x => x.StationId);
        modelBuilder.Entity<MetroLine>().ToTable("Lines").HasKey(x => x.LineId);
        modelBuilder.Entity<MetroStationLine>().ToTable("StationLines").HasKey(x => new { x.StationId, x.LineId });
        modelBuilder.Entity<MetroRoute>().ToTable("Routes").HasKey(x => x.RouteId);
        modelBuilder.Entity<MetroRouteStop>().ToTable("RouteStops").HasKey(x => new { x.RouteId, x.StopSequence });
        modelBuilder.Entity<MetroSegment>().ToTable("Segments").HasKey(x => x.SegmentId);
        modelBuilder.Entity<MetroTransferRule>().ToTable("TransferRules").HasKey(x => x.TransferRuleId);
        modelBuilder.Entity<MetroTrip>().ToTable("Trips").HasKey(x => x.TripId);
        modelBuilder.Entity<MetroStopTime>().ToTable("StopTimes").HasKey(x => new { x.TripId, x.StopSequence });
        modelBuilder.Entity<MetroHolidayDate>().ToTable("HolidayDates").HasKey(x => x.ServiceDate);

        foreach (var entity in modelBuilder.Model.GetEntityTypes())
            entity.SetIsTableExcludedFromMigrations(true);
    }

    public override int SaveChanges() => throw ReadOnlyException();
    public override int SaveChanges(bool acceptAllChangesOnSuccess) => throw ReadOnlyException();
    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => throw ReadOnlyException();
    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default) => throw ReadOnlyException();

    private static InvalidOperationException ReadOnlyException() =>
        new("MetroDbContext is read-only. Writes are not allowed from ScheduleBot.");
}

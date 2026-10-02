using Microsoft.EntityFrameworkCore;
using ScheduleBot.Models;

namespace ScheduleBot.Services;

public sealed class TehranCatalogSeeder(AppDbContext db, TehranMapCatalog maps)
{
    public async Task EnsureAsync(CancellationToken ct = default)
    {
        await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            // Prevent two app instances from importing the same catalog concurrently.
            await db.Database.ExecuteSqlRawAsync("DECLARE @result int; EXEC @result=sp_getapplock @Resource='TeaMapCatalogImport', @LockMode='Exclusive', @LockOwner='Transaction', @LockTimeout=60000; IF @result < 0 THROW 51000, 'Could not lock the Tehran catalog import.', 1;", ct);
            var ids = await db.TeaMapPlaces.Select(p => p.Id).ToListAsync(ct);
            db.TeaMapPlaces.AddRange(maps.AdditionalPlaces.Where(p => !ids.Contains(p.Id)).Select(p => new Place
            {
                Id = p.Id, Name = p.Name, Source = p.Source, ExternalId = p.ExternalId, PlaceType = p.PlaceType,
                Latitude = p.Latitude, Longitude = p.Longitude, Priority = p.Priority,
                CreatedAtUtc = p.CreatedAtUtc, UpdatedAtUtc = p.UpdatedAtUtc
            }));
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        });
    }
}

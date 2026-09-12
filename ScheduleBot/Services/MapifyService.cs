using Microsoft.EntityFrameworkCore;
using ScheduleBot.Models;

namespace ScheduleBot.Services;

/// <summary>Database access for collaborative Mapify maps and their locations.</summary>
public sealed class MapifyService(AppDbContext dbContext, MainService service) : DatabaseService(dbContext, service)
{
    private readonly AppDbContext _dbContext = dbContext;

    public async Task<Guid> CreateMapAsync(long chatId, string name)
    {
        var user = await GetUserByTelId(chatId) ?? throw new InvalidOperationException("User not found.");
        var map = new MapifyMap
        {
            Id = Guid.NewGuid(),
            Name = name.Trim(),
            CreatorId = user.Id,
            CreateTime = GetIranDateTime()
        };
        _dbContext.MapifyMaps.Add(map);
        _dbContext.MapifyMapAccesses.Add(new MapifyMapAccess { Id = Guid.NewGuid(), MapId = map.Id, UserId = user.Id });
        await _dbContext.SaveChangesAsync();
        return map.Id;
    }

    public async Task<List<MapifyMap>> GetMapsForUserAsync(long chatId)
    {
        var user = await GetUserByTelId(chatId);
        if (user == null) return [];
        var accessibleMapIds = _dbContext.MapifyMapAccesses.Where(x => x.UserId == user.Id).Select(x => x.MapId);
        return await _dbContext.MapifyMaps.Where(x => accessibleMapIds.Contains(x.Id)).OrderBy(x => x.Name).ToListAsync();
    }

    public async Task<MapifyMap?> GetMapForUserAsync(Guid mapId, long chatId)
    {
        var user = await GetUserByTelId(chatId);
        if (user == null) return null;
        return await _dbContext.MapifyMapAccesses.Where(x => x.MapId == mapId && x.UserId == user.Id)
            .Join(_dbContext.MapifyMaps, access => access.MapId, map => map.Id, (_, map) => map)
            .FirstOrDefaultAsync();
    }

    public async Task<bool> AcceptInvitationAsync(long chatId, Guid mapId)
    {
        var user = await GetUserByTelId(chatId);
        if (user == null || !await _dbContext.MapifyMaps.AnyAsync(x => x.Id == mapId)) return false;
        if (await _dbContext.MapifyMapAccesses.AnyAsync(x => x.MapId == mapId && x.UserId == user.Id)) return false;
        _dbContext.MapifyMapAccesses.Add(new MapifyMapAccess { Id = Guid.NewGuid(), MapId = mapId, UserId = user.Id });
        await _dbContext.SaveChangesAsync();
        return true;
    }

    public async Task<List<MapifyCategory>> GetCategoriesAsync(Guid mapId, long chatId, bool includePending = false)
    {
        if (await GetMapForUserAsync(mapId, chatId) == null) return [];
        var query = _dbContext.MapifyCategories.Where(x => x.MapId == mapId).OrderBy(x => x.CreateTime);
        return includePending
            ? await query.ToListAsync()
            : await query.Where(x => !x.TempAdded && !x.TempDeleted).ToListAsync();
    }

    public async Task<bool> AddCategoryAsync(long chatId, Guid mapId, string name)
    {
        if (string.IsNullOrWhiteSpace(name) || await GetMapForUserAsync(mapId, chatId) == null) return false;
        _dbContext.MapifyCategories.Add(new MapifyCategory
        {
            Id = Guid.NewGuid(), MapId = mapId, Name = name.Trim(), CreateTime = GetIranDateTime(), TempAdded = true
        });
        await _dbContext.SaveChangesAsync();
        return true;
    }

    public async Task<bool> ToggleCategoryDeletionAsync(long chatId, Guid categoryId)
    {
        var category = await _dbContext.MapifyCategories.FirstOrDefaultAsync(x => x.Id == categoryId);
        if (category == null || await GetMapForUserAsync(category.MapId, chatId) == null) return false;
        category.TempDeleted = !category.TempDeleted;
        await _dbContext.SaveChangesAsync();
        return true;
    }

    public async Task<bool> SubmitCategoryChangesAsync(long chatId, Guid mapId)
    {
        if (await GetMapForUserAsync(mapId, chatId) == null) return false;
        var categories = await _dbContext.MapifyCategories.Where(x => x.MapId == mapId).ToListAsync();
        var removedIds = categories.Where(x => x.TempDeleted).Select(x => x.Id).ToList();
        if (removedIds.Count != 0)
        {
            await _dbContext.MapifyLocationCategories.Where(x => removedIds.Contains(x.CategoryId)).ExecuteDeleteAsync();
            _dbContext.MapifyCategories.RemoveRange(categories.Where(x => x.TempDeleted));
        }
        foreach (var category in categories.Where(x => x.TempAdded && !x.TempDeleted)) category.TempAdded = false;
        await _dbContext.SaveChangesAsync();
        return true;
    }

    public async Task<bool> CancelCategoryChangesAsync(long chatId, Guid mapId)
    {
        if (await GetMapForUserAsync(mapId, chatId) == null) return false;
        var categories = await _dbContext.MapifyCategories.Where(x => x.MapId == mapId).ToListAsync();
        _dbContext.MapifyCategories.RemoveRange(categories.Where(x => x.TempAdded));
        foreach (var category in categories.Where(x => !x.TempAdded && x.TempDeleted)) category.TempDeleted = false;
        await _dbContext.SaveChangesAsync();
        return true;
    }

    public async Task<bool> AddLocationAsync(long chatId, MapifyLocationDraft draft)
    {
        var user = await GetUserByTelId(chatId);
        if (user == null || await GetMapForUserAsync(draft.MapId, chatId) == null || draft.CategoryIds.Count == 0 ||
            (draft.Score is < 0 or > 10) || (draft.IsVisited && draft.Score == null) || (!draft.IsVisited && draft.Score != null)) return false;

        var categoryIds = draft.CategoryIds.Distinct().ToList();
        var validCategoryCount = await _dbContext.MapifyCategories.CountAsync(x =>
            x.MapId == draft.MapId && categoryIds.Contains(x.Id) && !x.TempAdded && !x.TempDeleted);
        if (validCategoryCount != categoryIds.Count) return false;

        var location = new MapifyLocation
        {
            Id = Guid.NewGuid(), MapId = draft.MapId, AddedByUserId = user.Id, Name = draft.Name.Trim(),
            Latitude = draft.Latitude, Longitude = draft.Longitude, Description = string.IsNullOrWhiteSpace(draft.Description) ? null : draft.Description.Trim(),
            IsVisited = draft.IsVisited, Score = draft.Score, CreateTime = GetIranDateTime()
        };
        _dbContext.MapifyLocations.Add(location);
        _dbContext.MapifyLocationCategories.AddRange(categoryIds.Select(categoryId => new MapifyLocationCategory
        {
            Id = Guid.NewGuid(), LocationId = location.Id, CategoryId = categoryId
        }));
        await _dbContext.SaveChangesAsync();
        return true;
    }

    public async Task<List<MapifyLocationSuggestion>> GetSuggestionLocationsAsync(long chatId, Guid mapId, IReadOnlyCollection<Guid> categoryIds)
    {
        if (categoryIds.Count == 0 || await GetMapForUserAsync(mapId, chatId) == null) return [];
        var ids = categoryIds.Distinct().ToList();
        var locations = await _dbContext.MapifyLocations
            .Where(x => x.MapId == mapId && _dbContext.MapifyLocationCategories.Any(link => link.LocationId == x.Id && ids.Contains(link.CategoryId)))
            .ToListAsync();
        var locationIds = locations.Select(x => x.Id).ToList();
        var categoryNames = await _dbContext.MapifyLocationCategories.Where(x => locationIds.Contains(x.LocationId))
            .Join(_dbContext.MapifyCategories, link => link.CategoryId, category => category.Id,
                (link, category) => new { link.LocationId, category.Name, category.TempAdded, category.TempDeleted })
            .Where(x => !x.TempAdded && !x.TempDeleted)
            .ToListAsync();

        return locations.Select(location => new MapifyLocationSuggestion
        {
            Location = location,
            CategoryNames = categoryNames.Where(x => x.LocationId == location.Id).Select(x => x.Name).OrderBy(x => x).ToList()
        }).ToList();
    }
}

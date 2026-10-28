using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ScheduleBot.Models;

namespace ScheduleBot.Services;

public sealed class FileLinkService(AppDbContext db, IOptions<FileLinkOptions> options, TimeProvider clock)
{
    public static string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    public static bool ValidToken(string token) => token.Length == 64 && token.All(Uri.IsHexDigit);

    public async Task<string> CreateAsync(FileLink file, long ownerId, CancellationToken ct)
    {
        if (!options.Value.HasPublicBaseUrl)
            throw new FileLinkException(Messages.FileLinkPublicUrlMissing);
        if (options.Value.UseMtProto && !options.Value.HasMtProtoCredentials)
            throw new FileLinkException(Messages.FileLinkMtProtoNotConfigured);
        if (file.FileSize > options.Value.EffectiveMaxFileBytes)
            throw new FileLinkException(options.Value.UseMtProto ? Messages.FileLinkConfiguredLimit : Messages.FileLinkTooLarge);
        var now = clock.GetUtcNow().UtcDateTime;
        if (await db.FileLinks.CountAsync(x => x.OwnerId == ownerId && !x.Revoked && x.ExpiresAtUtc > now, ct)
            >= options.Value.MaxActiveLinksPerUser)
            throw new FileLinkException(Messages.FileLinkLimitReached);
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        file.TokenHash = Hash(token);
        file.OwnerId = ownerId;
        file.ExpiresAtUtc = now.AddMinutes(options.Value.ExpirationMinutes);
        db.FileLinks.Add(file);
        await db.SaveChangesAsync(ct);
        return options.Value.PublicBaseUrl.TrimEnd('/') + "/downloads/" + token;
    }

    public Task<FileLink?> ResolveAsync(string token, CancellationToken ct) =>
        db.FileLinks.AsNoTracking().SingleOrDefaultAsync(x => x.TokenHash == Hash(token), ct);

    public async Task<bool> RevokeAsync(string token, long ownerId, CancellationToken ct)
    {
        if (!ValidToken(token)) return false;
        return await db.FileLinks.Where(x => x.TokenHash == Hash(token) && x.OwnerId == ownerId)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Revoked, true), ct) > 0;
    }

    public static string SafeName(string name)
    {
        var safe = new string(name.Replace('\\', '/').Split('/').Last().Where(c => !char.IsControl(c) && "<>:\"|?*".IndexOf(c) < 0).Take(180).ToArray()).Trim(' ', '.');
        return string.IsNullOrEmpty(safe) ? "file" : safe;
    }

    public static string SafeMime(string mime) => mime.Length <= 150 &&
        System.Net.Http.Headers.MediaTypeHeaderValue.TryParse(mime, out var parsed) && parsed.Parameters.Count == 0
        ? parsed.MediaType! : "application/octet-stream";
}

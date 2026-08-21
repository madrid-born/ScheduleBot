using System.Net.Http.Headers;
using System.Text.Json;
using ScheduleBot.Models;

namespace ScheduleBot.Services;

public class SpotifyService(HttpClient httpClient, IConfiguration configuration)
{
    private readonly CancellationToken _cancellationToken = CancellationToken.None;
    
    
    public async Task<string> GetAccessToken()
    {
        var requestUri = $"{SpotifyApi.ApiCallSignIn}?password={SpotifyApi.ApiPassword}";
        using var response = await SendAsync(HttpMethod.Post, requestUri);
        if (!response.IsSuccessStatusCode) throw new HttpRequestException($"Error ({(int)response.StatusCode}).");
        return await response.Content.ReadAsStringAsync(_cancellationToken);
    }
    
    public async Task<FullTrack> GetTrackFromSpotify(string accessToken ,string trackId)
    {
        var requestUri = $"{SpotifyApi.ApiCallGetTrack}?trackSpotifyId={trackId}";
        using var response = await SendAsync(HttpMethod.Get, requestUri, accessToken);
    
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"Spotify API could not load the track ({(int)response.StatusCode}).");

        await using var stream = await response.Content.ReadAsStreamAsync(_cancellationToken);
    
        var trackDto = await JsonSerializer.DeserializeAsync<FullTrack>(
            stream, 
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true },
            _cancellationToken
        );

        if (trackDto == null || string.IsNullOrEmpty(trackDto.Track.SpotifyId))
            throw new InvalidOperationException("Invalid track data received");
        
        return trackDto;
    }
    
    public async Task<Playlist> GetPlayListFromSpotify(string accessToken ,string playlistId)
    {
        var requestUri = $"{SpotifyApi.ApiCallGetPlaylist}?playlistSpotifyId={playlistId}";
        using var response = await SendAsync(HttpMethod.Get, requestUri, accessToken);
    
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"Spotify API could not load the playlist ({(int)response.StatusCode}).");

        await using var stream = await response.Content.ReadAsStreamAsync(_cancellationToken);
    
        var playlistDto = await JsonSerializer.DeserializeAsync<Playlist>(
            stream, 
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true },
            _cancellationToken
        );

        if (playlistDto == null || string.IsNullOrEmpty(playlistDto.SpotifyId))
            throw new InvalidOperationException("Invalid playlist data received");
        
        return playlistDto;
    }
    
    public async Task<List<Playlist>> GetPlayListsFromDatabase(string accessToken)
    {
        var requestUri = $"{SpotifyApi.ApiCallGetPlaylists}";
        using var response = await SendAsync(HttpMethod.Get, requestUri, accessToken);
    
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"Spotify API could not load playlists ({(int)response.StatusCode}).");

        await using var stream = await response.Content.ReadAsStreamAsync(_cancellationToken);
    
        var playlists = await JsonSerializer.DeserializeAsync<List<Playlist>>(
            stream, 
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true },
            _cancellationToken
        );

        if (playlists == null) throw new InvalidOperationException("Invalid playlist data received");
        
        return playlists;
    }
    
    public async Task<SpotifyTrack?> GetNextTrackAsync(CancellationToken cancellationToken = default)
    {
        var inboxPlaylistId = GetRequiredConfiguration("SpotifyApi:InboxPlaylistSpotifyId");
        var requestUri = $"{SpotifyApi.ApiCallGetPlaylist}?playlistSpotifyId={Uri.EscapeDataString(inboxPlaylistId)}";
        using var response = await SendAsync(HttpMethod.Get, requestUri, "accessToken");

        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"Spotify API could not load the inbox playlist ({(int)response.StatusCode}).");

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        return FindTrack(document.RootElement);
    }

    public async Task AddTrackToCollectionsAsync(string trackSpotifyId, IEnumerable<string> playlistSpotifyIds,
        CancellationToken cancellationToken = default)
    {
        return;
        var playlistIds = playlistSpotifyIds.Distinct(StringComparer.Ordinal).ToList();
        if (playlistIds.Count == 0) return;

        var query = new List<string> { $"trackSpotifyId={Uri.EscapeDataString(trackSpotifyId)}" };
        query.AddRange(playlistIds.Select(id => $"playlistSpotifyIds={Uri.EscapeDataString(id)}"));

        using var response = await SendAsync(HttpMethod.Post, $"{SpotifyApi.ApiCallAddTrack}?{string.Join("&", query)}", "accessToken");
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"Spotify API could not categorize the track ({(int)response.StatusCode}).");
    }

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string relativeUri, string? accessToken = null)
    {
        using var request = new HttpRequestMessage(method, relativeUri);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/plain"));
        if (accessToken != null)
        {
            var prefix = "Bearer ";
            accessToken = accessToken.Remove(0, prefix.Length);

            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        }
        return await httpClient.SendAsync(request, _cancellationToken);
    }

    private string GetRequiredConfiguration(string key)
        => configuration[key] is { Length: > 0 } value
            ? value
            : throw new IOException($"Missing required configuration value '{key}'.");

    private static SpotifyTrack? FindTrack(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            if (TryGetProperty(element, "playlistTracks", out var nestedTrack))
            {
                var track = ParseTrack(nestedTrack);
                if (track != null) return track;
            }

            var directTrack = ParseTrack(element);
            if (directTrack != null) return directTrack;

            foreach (var property in element.EnumerateObject())
            {
                var track = FindTrack(property.Value);
                if (track != null) return track;
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                var track = FindTrack(item);
                if (track != null) return track;
            }
        }

        return null;
    }

    private static SpotifyTrack? ParseTrack(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object ||
            !TryGetString(element, ["id", "spotifyId", "trackSpotifyId"], out var id) ||
            !TryGetString(element, ["name", "trackName"], out var name) ||
            !TryGetProperty(element, ["artists", "trackArtists"], out var artistsElement)) return null;

        var artists = ParseArtists(artistsElement);
        if (artists.Count == 0) return null;

        string? albumName = null;
        if (TryGetProperty(element, "album", out var album) && TryGetString(album, ["name", "albumName"], out var parsedAlbumName))
            albumName = parsedAlbumName;
        else if (TryGetString(element, ["albumName"], out var directAlbumName))
            albumName = directAlbumName;

        return new SpotifyTrack(id, name, albumName, artists);
    }

    private static List<string> ParseArtists(JsonElement artists)
    {
        if (artists.ValueKind != JsonValueKind.Array) return [];

        var result = new List<string>();
        foreach (var artist in artists.EnumerateArray())
        {
            if (artist.ValueKind == JsonValueKind.String) result.Add(artist.GetString()!);
            else if (TryGetString(artist, ["name", "artistName"], out var name)) result.Add(name);
        }

        return result;
    }

    private static bool TryGetString(JsonElement element, string name, out string value)
    {
        value = string.Empty;
        return TryGetProperty(element, name, out var property) &&
               property.ValueKind == JsonValueKind.String &&
               !string.IsNullOrWhiteSpace(value = property.GetString());
    }

    private static bool TryGetString(JsonElement element, IEnumerable<string> names, out string value)
    {
        foreach (var name in names)
        {
            if (TryGetString(element, name, out value)) return true;
        }

        value = string.Empty;
        return false;
    }

    private static bool TryGetProperty(JsonElement element, string name, out JsonElement value)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    value = property.Value;
                    return true;
                }
            }
        }

        value = default;
        return false;
    }

    private static bool TryGetProperty(JsonElement element, IEnumerable<string> names, out JsonElement value)
    {
        foreach (var name in names)
        {
            if (TryGetProperty(element, name, out value)) return true;
        }

        value = default;
        return false;
    }

}

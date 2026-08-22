using System.Net.Http.Headers;
using System.Text.Json;
using ScheduleBot.Models;

namespace ScheduleBot.Services;

public class SpotifyService(HttpClient httpClient, IConfiguration configuration)
{
    private readonly CancellationToken _cancellationToken = CancellationToken.None;
    
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
    
    public async Task<string> AddTrackToCollections(string accessToken, string trackSpotifyId, IEnumerable<string> playlistSpotifyIds)
    {
        var requestUri = $"{SpotifyApi.ApiCallAddTrack}?trackSpotifyId={trackSpotifyId}";
        requestUri = playlistSpotifyIds.Aggregate(requestUri,
            (current, playlistId) => current + $"&playlistSpotifyIds={playlistId}");
        
        using var response = await SendAsync(HttpMethod.Post, requestUri, accessToken);
    
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"Error happend at adding the track to playlist ({(int)response.StatusCode}).");

        return await response.Content.ReadAsStringAsync(_cancellationToken);
    }
}

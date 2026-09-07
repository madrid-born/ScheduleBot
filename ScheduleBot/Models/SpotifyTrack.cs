namespace ScheduleBot.Models;

public class Playlist
{
    public string SpotifyId { get; set; }
    public int PlaylistTypeId { get; set; }
    public string PlaylistName { get; set; }
    public string ImageUrl { get; set; }
    public string OwnerName { get; set; }
    public List<string> PlaylistTracks { get; set; }
}


public class FullTrack
{
    public Track Track { get; set; }
    public Album? TrackAlbum  { get; set; }
    public List<Artist> TrackArtists  { get; set; }
}

public class Track
{
    public int Id { get; set; }
    public string? SpotifyId { get; set; }
    public string TrackName { get; set; } = string.Empty;
    public bool isPlayable { get; set; }
    public DateTime? ReleaseDate { get; set; }
}

public class Artist
{
    public int Id { get; set; }
    public string SpotifyId { get; set; } = string.Empty;
    public string ArtistName { get; set; } = string.Empty;
    public int? SpotifyPlayListId { get; set; }
    // public string? ImageUrl { get; set; }
}

public class Album
{
    public int Id { get; set; }
    public string SpotifyId { get; set; } = string.Empty;
    public string AlbumName { get; set; } = string.Empty;
    // public int TrackCount { get; set; }
    // public string? AlbumUrl { get; set; }
    // public string? ImageUrl { get; set; }
}

public sealed class PlaylistTracksAvailability
{
    public string SpotifyId { get; set; } = string.Empty;
    public string PlaylistName { get; set; } = string.Empty;
    public List<PlaylistAvailabilityTrack> PreviousAvailableTracks { get; set; } = [];
    public List<PlaylistAvailabilityTrack> PreviousUnavailableTracks { get; set; } = [];
}

public sealed class PlaylistAvailabilityTrack
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Artist0 { get; set; } = string.Empty;
}
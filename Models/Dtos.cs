namespace SurSuno.Api.Models;

public record LoginRequest(string Username, string Password);
public record LoginResponse(bool Success, string Token, string Username, string Message);

public record SongDto(
    int Id,
    string Title,
    int ArtistId,
    string Artist,
    int? AlbumId,
    string Album,
    int GenreId,
    string Genre,
    string Language,
    int? Year,
    int DurationSeconds,
    string? CoverUrl,
    string? AudioUrl,
    string AudioSourceType,
    string? ExternalUrl,
    bool IsPublished,
    bool IsFeatured,
    int PlayCount
);

public record ArtistDto(
    int Id,
    string Name,
    string? Bio,
    string? ImageUrl
);

public record AlbumDto(
    int Id,
    string Title,
    int ArtistId,
    string Artist,
    int? Year,
    string? CoverUrl
);

public record GenreDto(
    int Id,
    string Name
);

public record AdminStatsDto(
    int TotalSongs,
    int TotalArtists,
    int TotalAlbums,
    int TotalGenres,
    int LocalAudioCount,
    int ExternalAudioCount,
    bool DatabaseConnected
);

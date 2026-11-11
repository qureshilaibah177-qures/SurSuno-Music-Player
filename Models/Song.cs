namespace SurSuno.Api.Models;

public class Song
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public int ArtistId { get; set; }
    public int? AlbumId { get; set; }
    public int GenreId { get; set; }
    public string Artist { get; set; } = string.Empty;
    public string Album { get; set; } = string.Empty;
    public string Genre { get; set; } = string.Empty;
    public string Language { get; set; } = string.Empty;
    public int? Year { get; set; }
    public int DurationSeconds { get; set; } = 0;
    public string? CoverUrl { get; set; }
    public string? AudioUrl { get; set; }
    public string AudioSourceType { get; set; } = "External";
    public string? ExternalUrl { get; set; }
    public bool IsPublished { get; set; } = true;
    public bool IsFeatured { get; set; } = false;
    public int PlayCount { get; set; } = 0;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
namespace SurSuno.Api.Models;

public class Album
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public int ArtistId { get; set; }
    public string Artist { get; set; } = string.Empty;
    public int? Year { get; set; }
    public string? CoverUrl { get; set; }
}
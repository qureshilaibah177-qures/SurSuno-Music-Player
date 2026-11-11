using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SurSuno.Api.Data;
using SurSuno.Api.Models;
using SurSuno.Api.Security;

var builder = WebApplication.CreateBuilder(args);

// Initialize Token Security with HMAC signing key from configuration
TokenSecurity.Initialize(builder.Configuration);

// Configure Entity Framework Core with SQL Server
builder.Services.AddDbContext<SurSunoDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("SurSuno")));

// Configure CORS
builder.Services.AddCors(options => options.AddPolicy("Client", policy =>
    policy.WithOrigins("http://localhost:5173", "http://localhost:3000", "http://127.0.0.1:5173")
          .AllowAnyHeader()
          .AllowAnyMethod()));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.UseCors("Client");
app.UseStaticFiles(); // Serve uploaded audio and covers securely from wwwroot
app.UseSwagger();
app.UseSwaggerUI();

// -----------------------------------------------------------------------------
// Health & Info
// -----------------------------------------------------------------------------
app.MapGet("/", () => Results.Ok(new {
    app = "SurSuno API",
    status = "running",
    version = "2.0",
    docs = "/swagger"
}));

// -----------------------------------------------------------------------------
// Admin Authentication (Database-backed, PBKDF2 verified, Signed Tokens)
// -----------------------------------------------------------------------------
app.MapPost("/api/admin/login", async (LoginRequest req, SurSunoDbContext db, ILogger<Program> logger) =>
{
    if (string.IsNullOrWhiteSpace(req.Username) || string.IsNullOrWhiteSpace(req.Password))
    {
        return Results.BadRequest(new LoginResponse(false, "", "", "Username and password are required."));
    }

    try
    {
        var user = await db.AdminUsers.AsNoTracking().FirstOrDefaultAsync(u => u.Username == req.Username);
        if (user == null)
        {
            logger.LogWarning("Admin login failed: User '{Username}' not found.", req.Username);
            return Results.Unauthorized();
        }

        // Verify PBKDF2 salt:hash using constant-time comparison
        var isValid = PasswordSecurity.VerifyPassword(req.Password, user.PasswordHash);
        if (!isValid)
        {
            logger.LogWarning("Admin login failed: Invalid password for user '{Username}'.", req.Username);
            return Results.Unauthorized();
        }

        // Issue cryptographically signed HMAC-SHA256 bearer token with 120-minute expiration
        var token = TokenSecurity.GenerateToken(user.Username, expiryMinutes: 120);
        logger.LogInformation("Admin user '{Username}' authenticated successfully.", user.Username);

        return Results.Ok(new LoginResponse(true, token, user.Username, "Authentication successful."));
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Admin login error while accessing database.");
        return Results.Problem(
            detail: "Database is unavailable. Ensure PakistaniMusicDB is initialized via schema.sql in SSMS.",
            statusCode: 503,
            title: "Database Unavailable"
        );
    }
});

// Verify token validity
app.MapGet("/api/admin/verify", (HttpRequest request) =>
{
    if (!TokenSecurity.ValidateToken(request, out var username))
    {
        return Results.Unauthorized();
    }
    return Results.Ok(new { valid = true, username });
});

// -----------------------------------------------------------------------------
// Admin Dashboard Overview & Statistics
// -----------------------------------------------------------------------------
app.MapGet("/api/admin/stats", async (SurSunoDbContext db, ILogger<Program> logger) =>
{
    try
    {
        var songs = await db.Songs.AsNoTracking().ToListAsync();
        var artists = await db.Artists.CountAsync();
        var albums = await db.Albums.CountAsync();
        var genres = await db.Genres.CountAsync();

        var localAudio = songs.Count(s => !string.IsNullOrEmpty(s.AudioUrl) && s.AudioSourceType == "Local");
        var externalAudio = songs.Count(s => s.AudioSourceType == "External" || !string.IsNullOrEmpty(s.ExternalUrl));

        return Results.Ok(new AdminStatsDto(
            songs.Count,
            artists,
            albums,
            genres,
            localAudio,
            externalAudio,
            true
        ));
    }
    catch (Exception ex)
    {
        logger.LogWarning(ex, "Could not fetch stats: database not reachable.");
        return Results.Ok(new AdminStatsDto(0, 0, 0, 0, 0, 0, false));
    }
});

// -----------------------------------------------------------------------------
// Secure Media Upload (Content Validation, Size Limit, Traversal Prevention)
// -----------------------------------------------------------------------------
app.MapPost("/api/admin/upload", async (HttpRequest request, IWebHostEnvironment env, ILogger<Program> logger) =>
{
    // 1. Strict Server-side Token Authorization
    if (!TokenSecurity.ValidateToken(request, out var adminUser))
    {
        return Results.Unauthorized();
    }

    if (!request.HasFormContentType)
    {
        return Results.BadRequest(new { error = "Request must be multipart/form-data." });
    }

    var form = await request.ReadFormAsync();
    var file = form.Files.GetFile("file");
    var uploadType = form["type"].ToString().ToLowerInvariant();

    if (file == null || file.Length == 0)
    {
        return Results.BadRequest(new { error = "No file was uploaded." });
    }

    var extension = Path.GetExtension(file.FileName).ToLowerInvariant();

    if (uploadType == "audio")
    {
        // Extension Whitelist
        var allowedAudio = new[] { ".mp3", ".wav", ".ogg", ".m4a" };
        if (!allowedAudio.Contains(extension))
        {
            return Results.BadRequest(new { error = $"Invalid audio extension ({extension}). Allowed: .mp3, .wav, .ogg, .m4a" });
        }

        // File Size Limit (Max 50MB)
        if (file.Length > 50 * 1024 * 1024)
        {
            return Results.BadRequest(new { error = "Audio file exceeds maximum 50MB limit." });
        }

        // Magic Byte Content Verification
        using var stream = file.OpenReadStream();
        if (!FileUploadSecurity.ValidateAudioContent(stream, extension))
        {
            return Results.BadRequest(new { error = "File content does not match expected audio format signature." });
        }

        // Path Traversal Prevention & Safe Filename
        var targetFolder = Path.GetFullPath(Path.Combine(env.WebRootPath ?? Path.Combine(env.ContentRootPath, "wwwroot"), "audio"));
        Directory.CreateDirectory(targetFolder);

        var safeFileName = $"{Guid.NewGuid():N}{extension}";
        var destinationPath = Path.GetFullPath(Path.Combine(targetFolder, safeFileName));

        if (!destinationPath.StartsWith(targetFolder, StringComparison.OrdinalIgnoreCase))
        {
            return Results.BadRequest(new { error = "Invalid file destination." });
        }

        using var fileStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None);
        stream.Position = 0;
        await stream.CopyToAsync(fileStream);

        logger.LogInformation("Admin {Admin} uploaded audio file: {FileName}", adminUser, safeFileName);
        return Results.Ok(new { url = $"/audio/{safeFileName}", originalName = Path.GetFileName(file.FileName), size = file.Length });
    }
    else if (uploadType == "image")
    {
        // Extension Whitelist
        var allowedImages = new[] { ".jpg", ".jpeg", ".png", ".webp" };
        if (!allowedImages.Contains(extension))
        {
            return Results.BadRequest(new { error = $"Invalid image extension ({extension}). Allowed: .jpg, .jpeg, .png, .webp" });
        }

        // File Size Limit (Max 5MB)
        if (file.Length > 5 * 1024 * 1024)
        {
            return Results.BadRequest(new { error = "Cover image exceeds maximum 5MB limit." });
        }

        // Magic Byte Content Verification
        using var stream = file.OpenReadStream();
        if (!FileUploadSecurity.ValidateImageContent(stream, extension))
        {
            return Results.BadRequest(new { error = "File content does not match expected image format signature." });
        }

        // Path Traversal Prevention & Safe Filename
        var targetFolder = Path.GetFullPath(Path.Combine(env.WebRootPath ?? Path.Combine(env.ContentRootPath, "wwwroot"), "images"));
        Directory.CreateDirectory(targetFolder);

        var safeFileName = $"{Guid.NewGuid():N}{extension}";
        var destinationPath = Path.GetFullPath(Path.Combine(targetFolder, safeFileName));

        if (!destinationPath.StartsWith(targetFolder, StringComparison.OrdinalIgnoreCase))
        {
            return Results.BadRequest(new { error = "Invalid file destination." });
        }

        using var fileStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None);
        stream.Position = 0;
        await stream.CopyToAsync(fileStream);

        logger.LogInformation("Admin {Admin} uploaded cover image: {FileName}", adminUser, safeFileName);
        return Results.Ok(new { url = $"/images/{safeFileName}", originalName = Path.GetFileName(file.FileName), size = file.Length });
    }

    return Results.BadRequest(new { error = "Unsupported upload type. Specify 'audio' or 'image'." });
});

// -----------------------------------------------------------------------------
// Public Song Queries (Sanitized error responses)
// -----------------------------------------------------------------------------
app.MapGet("/api/songs", async (SurSunoDbContext db, ILogger<Program> logger) =>
{
    try
    {
        var list = await db.Songs.AsNoTracking().Where(s => s.IsPublished)
            .OrderBy(s => s.Title).Select(s => new {
                s.Id, s.Title, s.ArtistId, s.Artist, s.AlbumId, s.Album, s.GenreId, s.Genre,
                s.Language, s.Year, s.DurationSeconds, coverUrl = s.CoverUrl, s.AudioUrl,
                s.AudioSourceType, s.ExternalUrl, s.IsFeatured, s.PlayCount
            }).ToListAsync();
        return Results.Ok(list);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Failed to retrieve public songs from database.");
        return Results.Problem(detail: "Database service is currently unavailable.", statusCode: 503, title: "Database Error");
    }
});

app.MapGet("/api/songs/{id:int}", async (int id, SurSunoDbContext db, ILogger<Program> logger) =>
{
    try
    {
        var song = await db.Songs.AsNoTracking().Where(s => s.Id == id).Select(s => new {
            s.Id, s.Title, s.ArtistId, s.Artist, s.AlbumId, s.Album, s.GenreId, s.Genre,
            s.Language, s.Year, s.DurationSeconds, coverUrl = s.CoverUrl, s.AudioUrl,
            s.AudioSourceType, s.ExternalUrl, s.IsFeatured, s.PlayCount
        }).FirstOrDefaultAsync();
        return song != null ? Results.Ok(song) : Results.NotFound();
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Failed to retrieve song {Id}.", id);
        return Results.Problem(detail: "Database service is currently unavailable.", statusCode: 503, title: "Database Error");
    }
});

// -----------------------------------------------------------------------------
// Protected Song CRUD Endpoints
// -----------------------------------------------------------------------------
app.MapPost("/api/songs", async (HttpRequest request, Song song, SurSunoDbContext db, ILogger<Program> logger) =>
{
    if (!TokenSecurity.ValidateToken(request, out _)) return Results.Unauthorized();
    if (string.IsNullOrWhiteSpace(song.Title) || string.IsNullOrWhiteSpace(song.Artist))
        return Results.BadRequest(new { error = "Song Title and Artist are required." });

    try
    {
        song.CreatedAt = DateTime.UtcNow;
        db.Songs.Add(song);
        await db.SaveChangesAsync();
        return Results.Created($"/api/songs/{song.Id}", song);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error creating song entry.");
        return Results.Problem(detail: "Database operation failed. Ensure PakistaniMusicDB is initialized.", statusCode: 503);
    }
});

app.MapPut("/api/songs/{id:int}", async (int id, HttpRequest request, Song updated, SurSunoDbContext db, ILogger<Program> logger) =>
{
    if (!TokenSecurity.ValidateToken(request, out _)) return Results.Unauthorized();
    try
    {
        var existing = await db.Songs.FindAsync(id);
        if (existing == null) return Results.NotFound();

        existing.Title = updated.Title;
        existing.Artist = updated.Artist;
        existing.ArtistId = updated.ArtistId;
        existing.Album = updated.Album;
        existing.AlbumId = updated.AlbumId;
        existing.Genre = updated.Genre;
        existing.GenreId = updated.GenreId;
        existing.Language = updated.Language;
        existing.Year = updated.Year;
        existing.DurationSeconds = updated.DurationSeconds;
        existing.CoverUrl = updated.CoverUrl;
        existing.AudioUrl = updated.AudioUrl;
        existing.AudioSourceType = updated.AudioSourceType;
        existing.ExternalUrl = updated.ExternalUrl;
        existing.IsPublished = updated.IsPublished;
        existing.IsFeatured = updated.IsFeatured;

        await db.SaveChangesAsync();
        return Results.Ok(existing);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error updating song {Id}.", id);
        return Results.Problem(detail: "Database operation failed.", statusCode: 503);
    }
});

app.MapDelete("/api/songs/{id:int}", async (int id, HttpRequest request, SurSunoDbContext db, ILogger<Program> logger) =>
{
    if (!TokenSecurity.ValidateToken(request, out _)) return Results.Unauthorized();
    try
    {
        var existing = await db.Songs.FindAsync(id);
        if (existing == null) return Results.NotFound();

        db.Songs.Remove(existing);
        await db.SaveChangesAsync();
        return Results.NoContent();
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error deleting song {Id}.", id);
        return Results.Problem(detail: "Database operation failed.", statusCode: 503);
    }
});

// -----------------------------------------------------------------------------
// Protected Artists CRUD Endpoints
// -----------------------------------------------------------------------------
app.MapGet("/api/artists", async (SurSunoDbContext db, ILogger<Program> logger) =>
{
    try { return Results.Ok(await db.Artists.AsNoTracking().OrderBy(a => a.Name).ToListAsync()); }
    catch (Exception ex) { logger.LogError(ex, "Error fetching artists."); return Results.Problem(statusCode: 503, detail: "Database unavailable."); }
});

app.MapPost("/api/artists", async (HttpRequest request, Artist artist, SurSunoDbContext db, ILogger<Program> logger) =>
{
    if (!TokenSecurity.ValidateToken(request, out _)) return Results.Unauthorized();
    if (string.IsNullOrWhiteSpace(artist.Name)) return Results.BadRequest(new { error = "Artist Name is required." });
    try
    {
        db.Artists.Add(artist);
        await db.SaveChangesAsync();
        return Results.Created($"/api/artists/{artist.Id}", artist);
    }
    catch (Exception ex) { logger.LogError(ex, "Error adding artist."); return Results.Problem(statusCode: 503, detail: "Database operation failed."); }
});

app.MapPut("/api/artists/{id:int}", async (int id, HttpRequest request, Artist updated, SurSunoDbContext db, ILogger<Program> logger) =>
{
    if (!TokenSecurity.ValidateToken(request, out _)) return Results.Unauthorized();
    try
    {
        var existing = await db.Artists.FindAsync(id);
        if (existing == null) return Results.NotFound();
        existing.Name = updated.Name;
        existing.Bio = updated.Bio;
        existing.ImageUrl = updated.ImageUrl;
        await db.SaveChangesAsync();
        return Results.Ok(existing);
    }
    catch (Exception ex) { logger.LogError(ex, "Error updating artist {Id}.", id); return Results.Problem(statusCode: 503, detail: "Database operation failed."); }
});

app.MapDelete("/api/artists/{id:int}", async (int id, HttpRequest request, SurSunoDbContext db, ILogger<Program> logger) =>
{
    if (!TokenSecurity.ValidateToken(request, out _)) return Results.Unauthorized();
    try
    {
        var existing = await db.Artists.FindAsync(id);
        if (existing == null) return Results.NotFound();
        db.Artists.Remove(existing);
        await db.SaveChangesAsync();
        return Results.NoContent();
    }
    catch (Exception ex) { logger.LogError(ex, "Error deleting artist {Id}.", id); return Results.Problem(statusCode: 503, detail: "Database operation failed."); }
});

// -----------------------------------------------------------------------------
// Protected Albums CRUD Endpoints
// -----------------------------------------------------------------------------
app.MapGet("/api/albums", async (SurSunoDbContext db, ILogger<Program> logger) =>
{
    try { return Results.Ok(await db.Albums.AsNoTracking().OrderBy(a => a.Title).ToListAsync()); }
    catch (Exception ex) { logger.LogError(ex, "Error fetching albums."); return Results.Problem(statusCode: 503, detail: "Database unavailable."); }
});

app.MapPost("/api/albums", async (HttpRequest request, Album album, SurSunoDbContext db, ILogger<Program> logger) =>
{
    if (!TokenSecurity.ValidateToken(request, out _)) return Results.Unauthorized();
    if (string.IsNullOrWhiteSpace(album.Title)) return Results.BadRequest(new { error = "Album Title is required." });
    try
    {
        db.Albums.Add(album);
        await db.SaveChangesAsync();
        return Results.Created($"/api/albums/{album.Id}", album);
    }
    catch (Exception ex) { logger.LogError(ex, "Error adding album."); return Results.Problem(statusCode: 503, detail: "Database operation failed."); }
});

app.MapPut("/api/albums/{id:int}", async (int id, HttpRequest request, Album updated, SurSunoDbContext db, ILogger<Program> logger) =>
{
    if (!TokenSecurity.ValidateToken(request, out _)) return Results.Unauthorized();
    try
    {
        var existing = await db.Albums.FindAsync(id);
        if (existing == null) return Results.NotFound();
        existing.Title = updated.Title;
        existing.Artist = updated.Artist;
        existing.ArtistId = updated.ArtistId;
        existing.Year = updated.Year;
        existing.CoverUrl = updated.CoverUrl;
        await db.SaveChangesAsync();
        return Results.Ok(existing);
    }
    catch (Exception ex) { logger.LogError(ex, "Error updating album {Id}.", id); return Results.Problem(statusCode: 503, detail: "Database operation failed."); }
});

app.MapDelete("/api/albums/{id:int}", async (int id, HttpRequest request, SurSunoDbContext db, ILogger<Program> logger) =>
{
    if (!TokenSecurity.ValidateToken(request, out _)) return Results.Unauthorized();
    try
    {
        var existing = await db.Albums.FindAsync(id);
        if (existing == null) return Results.NotFound();
        db.Albums.Remove(existing);
        await db.SaveChangesAsync();
        return Results.NoContent();
    }
    catch (Exception ex) { logger.LogError(ex, "Error deleting album {Id}.", id); return Results.Problem(statusCode: 503, detail: "Database operation failed."); }
});

// -----------------------------------------------------------------------------
// Protected Genres CRUD Endpoints
// -----------------------------------------------------------------------------
app.MapGet("/api/genres", async (SurSunoDbContext db, ILogger<Program> logger) =>
{
    try { return Results.Ok(await db.Genres.AsNoTracking().OrderBy(g => g.Name).ToListAsync()); }
    catch (Exception ex) { logger.LogError(ex, "Error fetching genres."); return Results.Problem(statusCode: 503, detail: "Database unavailable."); }
});

app.MapPost("/api/genres", async (HttpRequest request, Genre genre, SurSunoDbContext db, ILogger<Program> logger) =>
{
    if (!TokenSecurity.ValidateToken(request, out _)) return Results.Unauthorized();
    if (string.IsNullOrWhiteSpace(genre.Name)) return Results.BadRequest(new { error = "Genre Name is required." });
    try
    {
        db.Genres.Add(genre);
        await db.SaveChangesAsync();
        return Results.Created($"/api/genres/{genre.Id}", genre);
    }
    catch (Exception ex) { logger.LogError(ex, "Error adding genre."); return Results.Problem(statusCode: 503, detail: "Database operation failed."); }
});

app.MapPut("/api/genres/{id:int}", async (int id, HttpRequest request, Genre updated, SurSunoDbContext db, ILogger<Program> logger) =>
{
    if (!TokenSecurity.ValidateToken(request, out _)) return Results.Unauthorized();
    try
    {
        var existing = await db.Genres.FindAsync(id);
        if (existing == null) return Results.NotFound();
        existing.Name = updated.Name;
        await db.SaveChangesAsync();
        return Results.Ok(existing);
    }
    catch (Exception ex) { logger.LogError(ex, "Error updating genre {Id}.", id); return Results.Problem(statusCode: 503, detail: "Database operation failed."); }
});

app.MapDelete("/api/genres/{id:int}", async (int id, HttpRequest request, SurSunoDbContext db, ILogger<Program> logger) =>
{
    if (!TokenSecurity.ValidateToken(request, out _)) return Results.Unauthorized();
    try
    {
        var existing = await db.Genres.FindAsync(id);
        if (existing == null) return Results.NotFound();
        db.Genres.Remove(existing);
        await db.SaveChangesAsync();
        return Results.NoContent();
    }
    catch (Exception ex) { logger.LogError(ex, "Error deleting genre {Id}.", id); return Results.Problem(statusCode: 503, detail: "Database operation failed."); }
});

app.Run();

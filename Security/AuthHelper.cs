using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace SurSuno.Api.Security;

public static class PasswordSecurity
{
    private const int SaltSize = 16;
    private const int KeySize = 32;
    private const int Iterations = 100_000;
    private static readonly HashAlgorithmName Algorithm = HashAlgorithmName.SHA256;

    public static string HashPassword(string password)
    {
        byte[] salt = RandomNumberGenerator.GetBytes(SaltSize);
        byte[] hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, Algorithm, KeySize);
        return $"{Convert.ToBase64String(salt)}:{Convert.ToBase64String(hash)}";
    }

    public static bool VerifyPassword(string password, string storedHash)
    {
        if (string.IsNullOrWhiteSpace(storedHash) || !storedHash.Contains(':')) return false;

        try
        {
            var parts = storedHash.Split(':');
            if (parts.Length != 2) return false;

            byte[] salt = Convert.FromBase64String(parts[0]);
            byte[] expectedHash = Convert.FromBase64String(parts[1]);

            byte[] actualHash = Rfc2898DeriveBytes.Pbkdf2(
                password,
                salt,
                Iterations,
                Algorithm,
                expectedHash.Length
            );

            return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
        }
        catch
        {
            return false;
        }
    }
}

public static class TokenSecurity
{
    // The HMAC secret key is populated strictly via IConfiguration / Environment Variables.
    // Zero hardcoded secrets are present in source code.
    private static byte[] _secretKey = Array.Empty<byte>();

    public static void Initialize(IConfiguration configuration)
    {
        // Check configuration first (handles appsettings.json, User Secrets, and Jwt__SecretKey env var).
        var secret = configuration["Jwt:SecretKey"];

        // If appsettings.json has an empty string (""), the ?? null-coalescing operator will NOT fall back
        // because "" is non-null. We explicitly check IsNullOrWhiteSpace to allow SURSUNO_JWT_SECRET fallback.
        if (string.IsNullOrWhiteSpace(secret))
        {
            secret = Environment.GetEnvironmentVariable("SURSUNO_JWT_SECRET");
        }

        if (string.IsNullOrWhiteSpace(secret) || secret.Length < 32)
        {
            throw new InvalidOperationException("Cryptographic failure: JWT secret key must be configured with at least 32 characters via Jwt:SecretKey (appsettings / User Secrets / Jwt__SecretKey) or SURSUNO_JWT_SECRET environment variable.");
        }

        _secretKey = Encoding.UTF8.GetBytes(secret);
    }

    public record TokenPayload(string Sub, string Role, long Exp, string Jti);

    public static string GenerateToken(string username, int expiryMinutes = 120)
    {
        if (_secretKey.Length == 0)
        {
            throw new InvalidOperationException("TokenSecurity has not been initialized with configuration.");
        }

        var exp = DateTimeOffset.UtcNow.AddMinutes(expiryMinutes).ToUnixTimeSeconds();
        var payload = new TokenPayload(username, "Admin", exp, Guid.NewGuid().ToString("N"));
        var payloadJson = JsonSerializer.Serialize(payload);
        var payloadBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(payloadJson));

        using var hmac = new HMACSHA256(_secretKey);
        var signatureBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(payloadBase64));
        var signatureBase64 = Convert.ToBase64String(signatureBytes);

        return $"{payloadBase64}.{signatureBase64}";
    }

    public static bool ValidateToken(HttpRequest request, out string? username)
    {
        username = null;
        if (_secretKey.Length == 0) return false;

        var authHeader = request.Headers.Authorization.ToString();
        if (string.IsNullOrWhiteSpace(authHeader) || !authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var token = authHeader["Bearer ".Length..].Trim();
        var parts = token.Split('.');
        if (parts.Length != 2) return false;

        var payloadBase64 = parts[0];
        var signatureBase64 = parts[1];

        try
        {
            // 1. Verify HMAC-SHA256 Signature using constant-time comparison
            using var hmac = new HMACSHA256(_secretKey);
            var expectedSignature = hmac.ComputeHash(Encoding.UTF8.GetBytes(payloadBase64));
            var actualSignature = Convert.FromBase64String(signatureBase64);

            if (!CryptographicOperations.FixedTimeEquals(expectedSignature, actualSignature))
            {
                return false;
            }

            // 2. Decode payload and check expiration
            var payloadJson = Encoding.UTF8.GetString(Convert.FromBase64String(payloadBase64));
            var payload = JsonSerializer.Deserialize<TokenPayload>(payloadJson);
            if (payload == null) return false;

            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            if (now > payload.Exp)
            {
                return false; // Token expired
            }

            username = payload.Sub;
            return true;
        }
        catch
        {
            return false;
        }
    }
}

public static class FileUploadSecurity
{
    public static bool ValidateImageContent(Stream stream, string extension)
    {
        if (stream.Length < 4) return false;
        var header = new byte[12];
        var originalPos = stream.Position;
        stream.Position = 0;
        int read = stream.Read(header, 0, header.Length);
        stream.Position = originalPos;

        if (read < 4) return false;

        // JPEG: FF D8 FF
        if (extension is ".jpg" or ".jpeg")
        {
            return header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF;
        }

        // PNG: 89 50 4E 47 0D 0A 1A 0A
        if (extension is ".png")
        {
            return header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47;
        }

        // WEBP: "RIFF" .... "WEBP"
        if (extension is ".webp")
        {
            if (read < 12) return false;
            var isRiff = header[0] == 0x52 && header[1] == 0x49 && header[2] == 0x46 && header[3] == 0x46;
            var isWebp = header[8] == 0x57 && header[9] == 0x45 && header[10] == 0x42 && header[11] == 0x50;
            return isRiff && isWebp;
        }

        return false;
    }

    public static bool ValidateAudioContent(Stream stream, string extension)
    {
        if (stream.Length < 4) return false;
        var header = new byte[12];
        var originalPos = stream.Position;
        stream.Position = 0;
        int read = stream.Read(header, 0, header.Length);
        stream.Position = originalPos;

        if (read < 4) return false;

        // MP3: "ID3" (49 44 33) or MPEG sync frame (FF FB or FF F3 or FF F2)
        if (extension is ".mp3")
        {
            var isId3 = header[0] == 0x49 && header[1] == 0x44 && header[2] == 0x33;
            var isMpeg = header[0] == 0xFF && (header[1] & 0xE0) == 0xE0;
            return isId3 || isMpeg;
        }

        // WAV: "RIFF" .... "WAVE"
        if (extension is ".wav")
        {
            if (read < 12) return false;
            var isRiff = header[0] == 0x52 && header[1] == 0x49 && header[2] == 0x46 && header[3] == 0x46;
            var isWave = header[8] == 0x57 && header[9] == 0x41 && header[10] == 0x56 && header[11] == 0x45;
            return isRiff && isWave;
        }

        // OGG: "OggS" (4F 67 67 53)
        if (extension is ".ogg")
        {
            return header[0] == 0x4F && header[1] == 0x67 && header[2] == 0x67 && header[3] == 0x53;
        }

        // M4A: "ftyp" at offset 4
        if (extension is ".m4a" or ".aac")
        {
            if (read < 8) return false;
            return header[4] == 0x66 && header[5] == 0x74 && header[6] == 0x79 && header[7] == 0x70;
        }

        return false;
    }
}

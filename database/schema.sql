-- =========================================================================
-- SurSuno — Pakistani Music Streaming Platform Database Schema
-- Database Name: PakistaniMusicDB
-- =========================================================================
-- NOTE: Do not run destructive commands if database already exists.
-- Open and execute this script in SQL Server Management Studio (SSMS)
-- or via sqlcmd once your SQL Server instance is connected.
-- =========================================================================

IF DB_ID(N'PakistaniMusicDB') IS NULL
BEGIN
    CREATE DATABASE PakistaniMusicDB;
END
GO

USE PakistaniMusicDB;
GO

-- 1. Genres Table
IF OBJECT_ID('dbo.Genres', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Genres (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        Name NVARCHAR(100) NOT NULL UNIQUE
    );
END
GO

-- 2. Artists Table
IF OBJECT_ID('dbo.Artists', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Artists (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        Name NVARCHAR(160) NOT NULL UNIQUE,
        Bio NVARCHAR(MAX) NULL,
        ImageUrl NVARCHAR(1000) NULL
    );
END
GO

-- 3. Albums Table (Relational FK to Artists)
IF OBJECT_ID('dbo.Albums', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Albums (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        Title NVARCHAR(200) NOT NULL,
        ArtistId INT NOT NULL CONSTRAINT FK_Albums_Artists FOREIGN KEY REFERENCES dbo.Artists(Id) ON DELETE CASCADE,
        Artist NVARCHAR(200) NOT NULL,
        [Year] INT NULL,
        CoverUrl NVARCHAR(1000) NULL
    );
END
GO

-- 4. Songs Table (Relational FK to Artists, Albums, Genres)
IF OBJECT_ID('dbo.Songs', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Songs (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        Title NVARCHAR(200) NOT NULL,
        ArtistId INT NOT NULL CONSTRAINT FK_Songs_Artists FOREIGN KEY REFERENCES dbo.Artists(Id),
        AlbumId INT NULL CONSTRAINT FK_Songs_Albums FOREIGN KEY REFERENCES dbo.Albums(Id) ON DELETE SET NULL,
        GenreId INT NOT NULL CONSTRAINT FK_Songs_Genres FOREIGN KEY REFERENCES dbo.Genres(Id),
        Artist NVARCHAR(250) NOT NULL,
        Album NVARCHAR(200) NOT NULL,
        Genre NVARCHAR(100) NOT NULL,
        [Language] NVARCHAR(100) NOT NULL,
        [Year] INT NULL,
        DurationSeconds INT NOT NULL DEFAULT 0,
        CoverUrl NVARCHAR(1000) NULL,
        AudioUrl NVARCHAR(1000) NULL,
        AudioSourceType NVARCHAR(50) NOT NULL DEFAULT 'External', -- 'Local' or 'External'
        ExternalUrl NVARCHAR(1500) NULL,
        IsPublished BIT NOT NULL DEFAULT 1,
        IsFeatured BIT NOT NULL DEFAULT 0,
        PlayCount INT NOT NULL DEFAULT 0,
        CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Songs_Title' AND object_id = OBJECT_ID('dbo.Songs'))
    CREATE INDEX IX_Songs_Title ON dbo.Songs(Title);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Songs_ArtistId' AND object_id = OBJECT_ID('dbo.Songs'))
    CREATE INDEX IX_Songs_ArtistId ON dbo.Songs(ArtistId);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Songs_AlbumId' AND object_id = OBJECT_ID('dbo.Songs'))
    CREATE INDEX IX_Songs_AlbumId ON dbo.Songs(AlbumId);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Songs_GenreId' AND object_id = OBJECT_ID('dbo.Songs'))
    CREATE INDEX IX_Songs_GenreId ON dbo.Songs(GenreId);
GO

-- 5. Admin Users Table
IF OBJECT_ID('dbo.AdminUsers', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.AdminUsers (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        Username NVARCHAR(100) NOT NULL UNIQUE,
        PasswordHash NVARCHAR(500) NOT NULL,
        Email NVARCHAR(200) NULL,
        Role NVARCHAR(50) NOT NULL DEFAULT 'Admin',
        CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
    );
END
GO

-- =========================================================================
-- Seed Data (Per-Record Uniqueness Guards)
-- Safe for empty, fresh, or partially populated databases
-- =========================================================================

-- Seed Genres (Per-record uniqueness guard based on unique Name)
INSERT INTO dbo.Genres (Name)
SELECT v.Name
FROM (VALUES 
    (N'Coke Studio'),
    (N'Sufi'),
    (N'Qawwali'),
    (N'Rock'),
    (N'Pop'),
    (N'Folk'),
    (N'OST'),
    (N'Classical'),
    (N'Punjabi'),
    (N'Romantic')
) AS v(Name)
WHERE NOT EXISTS (
    SELECT 1 FROM dbo.Genres g WHERE g.Name = v.Name
);
GO

-- Seed Artists (Per-record uniqueness guard based on unique Name)
INSERT INTO dbo.Artists (Name, Bio, ImageUrl)
SELECT v.Name, v.Bio, v.ImageUrl
FROM (VALUES 
    (N'Ali Sethi', N'Pakistani singer and writer, acclaimed for ghazals and Coke Studio hit Pasoori.', N'https://images.unsplash.com/photo-1516280440614-37939bbacd81?w=500'),
    (N'Shae Gill', N'Vocalist discovered on Instagram, achieved global recognition with Pasoori.', N'https://images.unsplash.com/photo-1534528741775-53994a69daeb?w=500'),
    (N'Abida Parveen', N'The Queen of Sufi Music, renowned worldwide for transcendent spiritual performances.', N'https://images.unsplash.com/photo-1508700115892-45ecd05ae2ad?w=500'),
    (N'Naseebo Lal', N'Iconic folk and Punjabi playback legend known for her powerful vocal range.', N'https://images.unsplash.com/photo-1544005313-94ddf0286df2?w=500'),
    (N'Atif Aslam', N'Celebrated pop-rock and Sufi vocalist with numerous chart-topping Pakistani hits.', N'https://images.unsplash.com/photo-1511379938547-c1f69419868d?w=500'),
    (N'Rahat Fateh Ali Khan', N'Legendary Qawwali and playback maestro, nephew of Nusrat Fateh Ali Khan.', N'https://images.unsplash.com/photo-1516450360452-9312f5e86fc7?w=500'),
    (N'Momina Mustehsan', N'Vocalist and songwriter who broke out on Coke Studio Season 9.', N'https://images.unsplash.com/photo-1494790108377-be9c29b29330?w=500'),
    (N'Jal', N'Pioneering pop-rock band founded by Goher Mumtaz, known for the youth anthem Aadat.', N'https://images.unsplash.com/photo-1493225457124-a3eb161ffa5f?w=500'),
    (N'Strings', N'Legendary pop-rock duo Bilal Maqsood and Faisal Kapadia, shaped decades of Pakistani music.', N'https://images.unsplash.com/photo-1506157786151-b8491531f063?w=500'),
    (N'Coke Studio', N'Pakistan premier cultural music television series that brings together classical and contemporary artists.', N'https://images.unsplash.com/photo-1514525253161-7a46d19cd819?w=500')
) AS v(Name, Bio, ImageUrl)
WHERE NOT EXISTS (
    SELECT 1 FROM dbo.Artists a WHERE a.Name = v.Name
);
GO

-- Seed Albums (Per-record uniqueness guard linking dynamically to Artists by Name)
INSERT INTO dbo.Albums (Title, ArtistId, Artist, [Year], CoverUrl)
SELECT v.Title, a.Id, v.Artist, v.[Year], v.CoverUrl
FROM (VALUES 
    (N'Coke Studio Season 14', N'Coke Studio', N'Various Artists', 2022, N'https://images.unsplash.com/photo-1516280440614-37939bbacd81?w=500'),
    (N'Coke Studio Season 8', N'Coke Studio', N'Various Artists', 2015, N'https://images.unsplash.com/photo-1511379938547-c1f69419868d?w=500'),
    (N'Coke Studio Season 9', N'Coke Studio', N'Various Artists', 2016, N'https://images.unsplash.com/photo-1516450360452-9312f5e86fc7?w=500'),
    (N'Coke Studio Season 12', N'Coke Studio', N'Various Artists', 2019, N'https://images.unsplash.com/photo-1510915361894-db8b60106cb1?w=500'),
    (N'Aadat', N'Jal', N'Jal', 2004, N'https://images.unsplash.com/photo-1493225457124-a3eb161ffa5f?w=500'),
    (N'Dhaani', N'Strings', N'Strings', 2003, N'https://images.unsplash.com/photo-1506157786151-b8491531f063?w=500')
) AS v(Title, ArtistLookup, Artist, [Year], CoverUrl)
JOIN dbo.Artists a ON a.Name = v.ArtistLookup
WHERE NOT EXISTS (
    SELECT 1 FROM dbo.Albums alb WHERE alb.Title = v.Title AND alb.Artist = v.Artist
);
GO

-- Seed Songs (Per-record uniqueness guard linking dynamically to Artists, Albums, and Genres)
INSERT INTO dbo.Songs 
(Title, ArtistId, AlbumId, GenreId, Artist, Album, Genre, [Language], [Year], DurationSeconds, CoverUrl, AudioUrl, AudioSourceType, ExternalUrl, IsPublished, IsFeatured, PlayCount)
SELECT 
    v.Title,
    art.Id,
    alb.Id,
    g.Id,
    v.Artist,
    v.Album,
    v.Genre,
    v.[Language],
    v.[Year],
    v.DurationSeconds,
    v.CoverUrl,
    NULL,
    N'External',
    v.ExternalUrl,
    1,
    v.IsFeatured,
    v.PlayCount
FROM (VALUES
    (N'Pasoori', N'Ali Sethi', N'Coke Studio Season 14', N'Coke Studio', N'Ali Sethi, Shae Gill', N'Coke Studio Season 14', N'Coke Studio', N'Urdu / Punjabi', 2022, 224,
     N'https://images.unsplash.com/photo-1516280440614-37939bbacd81?w=500', N'https://www.youtube.com/results?search_query=Pasoori+Ali+Sethi+Shae+Gill+official', 1, 4820),

    (N'Tu Jhoom', N'Abida Parveen', N'Coke Studio Season 14', N'Sufi', N'Abida Parveen, Naseebo Lal', N'Coke Studio Season 14', N'Sufi', N'Punjabi / Urdu', 2022, 436,
     N'https://images.unsplash.com/photo-1508700115892-45ecd05ae2ad?w=500', N'https://www.youtube.com/results?search_query=Tu+Jhoom+official+Coke+Studio', 1, 3910),

    (N'Tajdar-e-Haram', N'Atif Aslam', N'Coke Studio Season 8', N'Qawwali', N'Atif Aslam', N'Coke Studio Season 8', N'Qawwali', N'Urdu', 2015, 628,
     N'https://images.unsplash.com/photo-1511379938547-c1f69419868d?w=500', N'https://www.youtube.com/results?search_query=Tajdar+e+Haram+Atif+Aslam+official', 1, 5740),

    (N'Afreen Afreen', N'Rahat Fateh Ali Khan', N'Coke Studio Season 9', N'Coke Studio', N'Rahat Fateh Ali Khan, Momina Mustehsan', N'Coke Studio Season 9', N'Coke Studio', N'Urdu', 2016, 404,
     N'https://images.unsplash.com/photo-1516450360452-9312f5e86fc7?w=500', N'https://www.youtube.com/results?search_query=Afreen+Afreen+Coke+Studio+official', 1, 6120),

    (N'Aadat', N'Jal', N'Aadat', N'Rock', N'Jal', N'Aadat', N'Rock', N'Urdu', 2004, 269,
     N'https://images.unsplash.com/photo-1493225457124-a3eb161ffa5f?w=500', N'https://www.youtube.com/results?search_query=Jal+Aadat+official', 0, 2450),

    (N'Wohi Khuda Hai', N'Atif Aslam', N'Coke Studio Season 12', N'Sufi', N'Atif Aslam', N'Coke Studio Season 12', N'Sufi', N'Urdu', 2019, 411,
     N'https://images.unsplash.com/photo-1510915361894-db8b60106cb1?w=500', N'https://www.youtube.com/results?search_query=Wohi+Khuda+Hai+Atif+Aslam+official', 0, 1980),

    (N'Sajni', N'Strings', N'Dhaani', N'Pop', N'Strings', N'Dhaani', N'Pop', N'Urdu', 2003, 290,
     N'https://images.unsplash.com/photo-1506157786151-b8491531f063?w=500', N'https://www.youtube.com/results?search_query=Strings+Sajni+official', 0, 1840),

    (N'Mera Bichra Yaar', N'Strings', N'Dhaani', N'Folk', N'Strings, Zoe Viccaji', N'Coke Studio', N'Folk', N'Urdu', 2011, 318,
     N'https://images.unsplash.com/photo-1514525253161-7a46d19cd819?w=500', N'https://www.youtube.com/results?search_query=Mera+Bichra+Yaar+Coke+Studio+official', 0, 1420)
) AS v(Title, PrimaryArtistName, AlbumTitle, GenreName, Artist, Album, Genre, [Language], [Year], DurationSeconds, CoverUrl, ExternalUrl, IsFeatured, PlayCount)
JOIN dbo.Artists art ON art.Name = v.PrimaryArtistName
LEFT JOIN dbo.Albums alb ON alb.Title = v.AlbumTitle
JOIN dbo.Genres g ON g.Name = v.GenreName
WHERE NOT EXISTS (
    SELECT 1 FROM dbo.Songs s WHERE s.Title = v.Title AND s.Artist = v.Artist
);
GO

-- Seed Initial Default Admin (Password: SurSunoAdmin#2026 hashed with PBKDF2-HMAC-SHA256, 100k iterations)
IF NOT EXISTS (SELECT 1 FROM dbo.AdminUsers WHERE Username = 'admin')
BEGIN
    INSERT INTO dbo.AdminUsers (Username, PasswordHash, Email, Role)
    VALUES ('admin', 'ml9zsmpdkBYCCBeFNRXXCA==:7b/eMpal8iFzCF5Via7TA2MKHfw8h6uQoOw3JKvCHco=', 'admin@sursuno.pk', 'Admin');
END
GO

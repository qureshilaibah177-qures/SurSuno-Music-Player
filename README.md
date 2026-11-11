# SurSuno

A Pakistani music discovery and playback web app starter.

## Stack
- React + Vite frontend
- ASP.NET Core Web API backend
- SQL Server + Entity Framework Core
- Browser localStorage for no-account playlists, favourites and listening history

## Prerequisites
- .NET 8 SDK (or newer compatible SDK)
- Node.js LTS
- SQL Server (LocalDB, Express, or Developer) and SSMS
- Antigravity or VS Code

## 1. Configure SQL Server
Open `database/schema.sql` in SSMS and execute it. It creates `PakistaniMusicDB` and starter tables/data.

## 2. Configure the API
Edit `SurSuno.Api/appsettings.json` and set `ConnectionStrings:SurSuno` to your SQL Server instance.

Examples:
- LocalDB: `Server=(localdb)\\MSSQLLocalDB;Database=PakistaniMusicDB;Trusted_Connection=True;TrustServerCertificate=True;`
- SQL Express: `Server=.\\SQLEXPRESS;Database=PakistaniMusicDB;Trusted_Connection=True;TrustServerCertificate=True;`

From the `SurSuno.Api` directory:
```bash
dotnet restore
dotnet run
```
The API listens on `http://localhost:5080` by default.

## 3. Run the frontend
From `SurSuno.Client`:
```bash
npm install
npm run dev
```
Open the Vite URL shown in the terminal (usually http://localhost:5173).

## Included functionality
- Browse/search/filter catalogue
- Play/pause, previous/next, seek, volume, shuffle and repeat
- Add/remove songs to local playlists
- Favourite songs and recently played history in localStorage
- API endpoints for songs, artists, albums, genres and playlist metadata
- SQL Server schema and sample Pakistani artist/song metadata

## Important audio and copyright note
The included sample catalogue is metadata only. It does not include copyrighted recordings. Playback works when you add audio files that you own or are licensed to use under `SurSuno.Client/public/audio/`, then set each song's `audioUrl` to `/audio/your-file.mp3` in SQL Server. For tracks without streaming permission, use an official external URL and open the official provider page instead of downloading or rehosting the recording.

## Add licensed audio
1. Put your licensed MP3/AAC/OGG file in `SurSuno.Client/public/audio/`.
2. Update that song's `AudioUrl` in SQL Server to `/audio/filename.mp3`.
3. Restart/refresh the app. The browser's native audio element handles playback.

## Project structure
```text
SurSuno/
  SurSuno.Client/       React frontend
  SurSuno.Api/           ASP.NET Core API
  database/schema.sql    SQL Server schema + starter metadata
```

This is a starter implementation. Before public deployment, add admin authentication, server-side validation, rate limiting, secure secrets management, HTTPS, and licensed streaming/storage infrastructure.

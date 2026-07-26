# Run and test MeetingMind locally

MeetingMind's Development configuration already contains the local PostgreSQL,
Storage, and FFmpeg settings for this checkout. You do not need to set
`Storage__RootPath`, `AudioProcessing__FfmpegBinaryFolder`, or run
`dotnet ef database update` during normal development.

## One-time setup

Install:

- .NET 8 SDK
- Docker Desktop
- Node.js and npm
- FFmpeg at `C:\ffmpeg\build\bin\ffmpeg.exe`

The committed Development settings use:

- Repository: `C:\work\Meetingminutes\MeetingMind`
- Storage: `C:\work\Meetingminutes\MeetingMind\Storage`
- FFmpeg: `C:\ffmpeg\build\bin\ffmpeg.exe`
- PostgreSQL: `127.0.0.1:5432`, database `meetingmind`

If this checkout or FFmpeg moves, update both:

- `src/MeetingMind.Api/appsettings.Development.json`
- `src/MeetingMind.Worker/appsettings.Development.json`

### Add the local OpenAI key once

From the repository root:

```powershell
Copy-Item appsettings.Local.example.json appsettings.Local.json
notepad appsettings.Local.json
```

Replace the placeholder with the real key and save the file:

```json
{
  "OpenAI": {
    "ApiKey": "your-real-key"
  }
}
```

`appsettings.Local.json` is ignored by Git and is loaded by both the API and
Worker. Never add the real key to either committed Development settings file.

Restore dependencies once after cloning or after dependency changes:

```powershell
dotnet tool restore
dotnet restore MeetingMind.sln
Set-Location frontend\meetingmind-ui
npm.cmd install
Set-Location ..\..
```

## Normal startup

Use four PowerShell windows so each long-running process remains visible.

### 1. Start PostgreSQL

Start Docker Desktop. In PowerShell window 1:

```powershell
docker compose up -d
docker compose ps
docker compose exec meetingmind-postgres pg_isready -U meetingmind_user -d meetingmind
```

Continue when PostgreSQL reports `accepting connections`.

If Docker returns `Access is denied`, open Docker Desktop and PowerShell with
the permissions required by your Windows Docker installation.

### 2. Start the API and apply migrations automatically

In PowerShell window 2:

```powershell
dotnet run --project src\MeetingMind.Api --launch-profile http
```

In Development, the API waits for PostgreSQL and applies all pending EF Core
migrations before it starts listening. When Swagger opens successfully, the
database schema is ready:

```text
http://localhost:5059/swagger
```

Do not routinely run `dotnet ef database update`. If PostgreSQL is unavailable,
the API retries for about 30 seconds and then reports a focused Docker/database
startup error. The committed Development connection uses IPv4 explicitly to
avoid a Windows `localhost`/WSL IPv6 relay taking precedence over Docker's
published PostgreSQL port.

### 3. Start the Worker

After Swagger is available, use PowerShell window 3:

```powershell
dotnet run --project src\MeetingMind.Worker
```

The Worker verifies that PostgreSQL is reachable and that no migrations are
pending. It never changes the schema. If it reports pending migrations, stop it,
start the API, wait for Swagger, and then start the Worker again.

The first transcription can take longer while the local Whisper model is
downloaded.

### 4. Start the frontend

In PowerShell window 4:

```powershell
Set-Location frontend\meetingmind-ui
npm.cmd run dev
```

Open:

```text
http://localhost:5173
```

The frontend proxies API requests to `http://localhost:5059`.

## Confirm readiness

With the API and Worker running:

```powershell
Invoke-RestMethod http://localhost:5059/health
Invoke-RestMethod http://localhost:5059/health/db
Invoke-RestMethod http://localhost:5059/health/ready
```

The readiness response reports:

- `database`
- `storage`
- `ffmpeg`
- `whisper_model`

The Whisper model can remain unavailable until the first transcription triggers
its download. Use privacy-safe test audio for that initial job.

Hangfire is available at:

```text
http://localhost:5059/hangfire
```

## Test the three processing workflows

Open `http://localhost:5173` and verify:

### Audio to transcript and minutes

1. Select **Audio → transcript and meeting minutes**.
2. Upload a supported MP3, WAV, M4A, or AAC file.
3. Confirm the job is accepted immediately and reaches `Completed`.
4. Confirm both Transcript and Minutes are available.

### Audio to transcript only

1. Select **Audio → transcript only**.
2. Upload supported audio.
3. Confirm the job completes with a Transcript.
4. Confirm no meeting minutes were generated.

### Transcript to minutes

1. Select **Transcript → meeting minutes**.
2. Upload UTF-8 `.txt` as `text/plain` or `.md` as `text/markdown`.
3. Confirm the job skips audio processing and produces Minutes.
4. Confirm the uploaded transcript remains downloadable.

For every workflow, refresh the browser and confirm accepted jobs remain in
History. Test retry behavior with a failed or cancelled job where practical.

## Automated verification

Keep Docker Desktop running for PostgreSQL-backed integration tests.

Backend:

```powershell
dotnet build MeetingMind.sln
dotnet test MeetingMind.sln
```

Frontend:

```powershell
Set-Location frontend\meetingmind-ui
npm.cmd run lint
npm.cmd test -- --run
npm.cmd run test:coverage
npm.cmd run build
Set-Location ..\..
```

All commands should complete with zero failures. Test counts can increase as
Phase 3 work is added, so use the test result rather than an old fixed count.

## Database troubleshooting

### API cannot connect to PostgreSQL

Check Docker and PostgreSQL:

```powershell
docker compose ps
docker compose logs meetingmind-postgres
docker compose exec meetingmind-postgres pg_isready -U meetingmind_user -d meetingmind
Test-NetConnection 127.0.0.1 -Port 5432
```

The API will apply migrations automatically after PostgreSQL becomes reachable.

### Worker reports pending migrations

Stop the Worker. Start the Development API and wait for Swagger, then restart
the Worker.

### Reset an unrecoverable local database

Reset only when local data can be discarded. The reset removes the MeetingMind
PostgreSQL Docker volume, including all job metadata and migration history. It
does not delete files under `Storage`.

Run:

```powershell
.\scripts\reset-local-database.ps1
```

The script requires this exact confirmation phrase:

```text
DELETE MEETINGMIND LOCAL DATABASE
```

After reset, start the API. It recreates and migrates the database
automatically.

Never use `docker compose down -v` as a routine stop command.

## Other common problems

### Worker reports that `OpenAI:ApiKey` is required

Create or correct root `appsettings.Local.json` using
`appsettings.Local.example.json`. Do not commit the local file.

### FFmpeg configuration fails

Confirm this file exists:

```powershell
Test-Path C:\ffmpeg\build\bin\ffmpeg.exe
```

If FFmpeg is elsewhere, update both Development appsettings files.

### Jobs remain queued

Confirm:

- Worker is running.
- Hangfire shows an active server.
- API and Worker point to the same PostgreSQL database and Storage folder.
- Worker terminal contains no configuration or provider error.

## Stop MeetingMind

Press `Ctrl+C` in this order:

1. Frontend
2. Worker
3. API

Then stop PostgreSQL while preserving its data:

```powershell
docker compose down
```

# MeetingMind AI

MeetingMind AI is a local AI meeting-minutes application that converts uploaded
audio into a transcript and structured meeting minutes. It identifies the
meeting summary, attendees, discussion points, decisions, action items, risks,
and next steps.

The application uses an asynchronous workflow: the API accepts an upload and
returns a job ID immediately, while a separate Hangfire worker performs audio
conversion, transcription, minutes generation, and result storage.

## Current status

The local MVP is complete and has been verified with end-to-end audio tests.
The implemented workflow supports:

- Uploading MP3, WAV, M4A, and AAC audio files.
- Validating file extension, MIME type, filename, and configurable file size.
- Returning a job ID without waiting for audio or AI processing.
- Tracking job stage, status, progress, and errors.
- Converting audio to Whisper-compatible WAV with FFmpeg.
- Transcribing audio locally with Whisper.net.
- Generating structured meeting minutes with OpenAI GPT.
- Viewing minutes and transcripts in the React frontend.
- Downloading transcript and Markdown minutes files.
- Reviewing processing history.
- Retrying failed or cancelled jobs.

No authentication is implemented in this version. The application is intended
to be run in a trusted local environment.

## Processing workflow

```text
Upload audio
  -> Validate and save original file
  -> Create MeetingJob record
  -> Enqueue Hangfire job
  -> Return job ID immediately

MeetingMind.Worker
  -> Validate stored audio
  -> Convert to PCM 16-bit, 16 kHz, mono WAV with FFmpeg
  -> Transcribe locally with Whisper.net
  -> Save transcript record and text file
  -> Generate structured minutes with OpenAI GPT
  -> Save minutes record and Markdown file
  -> Mark job completed
```

## Technology stack

### Frontend

- React 19
- TypeScript
- Vite
- Material UI
- Axios

### Backend

- .NET 8
- ASP.NET Core Web API
- Entity Framework Core
- Clean Architecture
- Dependency Injection

### Processing and persistence

- PostgreSQL 16
- Hangfire with PostgreSQL storage
- FFmpeg through FFMpegCore
- Whisper.net with the local CPU runtime
- OpenAI .NET SDK for structured meeting-minutes generation
- Local filesystem storage

## Architecture

```text
React frontend
    |
    v
MeetingMind.Api
    |
    v
MeetingMind.Application
    |
    +-------------------+
    |                   |
    v                   v
MeetingMind.Domain  Application interfaces
                        ^
                        |
MeetingMind.Infrastructure
    |
    +-- PostgreSQL / EF Core
    +-- Hangfire client
    +-- Local file storage

MeetingMind.Worker
    |
    +-- Hangfire server
    +-- FFmpeg audio processing
    +-- Whisper.net transcription
    +-- OpenAI meeting-minutes generation
```

`MeetingMind.Api` and `MeetingMind.Worker` are separate entry points into the
same Application and Infrastructure layers. Controllers enqueue work and read
results; they do not run FFmpeg, Whisper, or OpenAI operations directly.

External dependencies are accessed through application interfaces such as:

- `IFileStorageService`
- `IAudioProcessingService`
- `ITranscriptionService`
- `IMeetingMinutesService`
- `IBackgroundJobService`

## Repository structure

```text
MeetingMind.sln
docker-compose.yml
docs/
  PRD.md
  SAD.md
  FSD.md
frontend/
  meetingmind-ui/
src/
  MeetingMind.Api/             HTTP API and controllers
  MeetingMind.Application/     Use cases, interfaces, and orchestration
  MeetingMind.Domain/          Entities, enums, and business rules
  MeetingMind.Infrastructure/  EF Core, storage, FFmpeg, Whisper, and OpenAI
  MeetingMind.Shared/          Shared contracts
  MeetingMind.Worker/          Hangfire server and processing workflow
tests/
  MeetingMind.Unit.Tests/                    Domain and Application unit tests
  MeetingMind.Api.IntegrationTests/          HTTP contracts with disposable PostgreSQL
  MeetingMind.Infrastructure.IntegrationTests/ PostgreSQL repository behavior
  MeetingMind.Worker.Tests/                  Processing orchestration with test doubles
Storage/
  Audio/Original/
  Audio/Processed/
  Transcript/
  Minutes/
```

## Prerequisites

Install the following before running MeetingMind:

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Docker Desktop](https://www.docker.com/products/docker-desktop/)
- [Node.js LTS](https://nodejs.org/) with npm
- [FFmpeg](https://ffmpeg.org/download.html)
- An OpenAI API key for meeting-minutes generation

The default Worker configuration expects FFmpeg at:

```text
C:\ffmpeg\ffmpeg.exe
```

Change `AudioProcessing:FfmpegBinaryFolder` in the Worker configuration or set
the corresponding environment variable if FFmpeg is installed elsewhere.

## Local setup

Run commands from the repository root unless a different directory is shown.

### 1. Start PostgreSQL

Start Docker Desktop, then run:

```powershell
docker compose up -d
docker compose ps
```

The local PostgreSQL container uses:

```text
Host: localhost
Port: 5432
Database: meetingmind
Username: meetingmind_user
Password: meetingmind_password
```

These credentials are for local development only.

### 2. Restore tools, apply migrations, build, and test

```powershell
dotnet tool restore
dotnet restore
dotnet ef database update `
  --project src\MeetingMind.Infrastructure `
  --startup-project src\MeetingMind.Api
dotnet build MeetingMind.sln
dotnet test MeetingMind.sln
```

### 3. Start the API

Open a new PowerShell window:

```powershell
cd path\to\MeetingMind
dotnet run --project src\MeetingMind.Api --launch-profile http
```

The API listens on `http://localhost:5059`.

### 4. Start the Worker

Open another PowerShell window and set the OpenAI API key for that process:

```powershell
cd path\to\MeetingMind
$env:OPENAI_API_KEY = "your-openai-api-key"
dotnet run --project src\MeetingMind.Worker
```

The first transcription may take longer because Whisper.net downloads the
configured local model when it is not already present. The default model is
`small` and automatic model download is enabled.

### 5. Install and start the frontend

Open another PowerShell window:

```powershell
cd path\to\MeetingMind\frontend\meetingmind-ui
npm install
npm run dev
```

Open `http://localhost:5173`. The Vite development server proxies `/api`
requests to `http://localhost:5059`.

If PowerShell blocks `npm.ps1`, use the Windows command wrapper:

```powershell
npm.cmd install
npm.cmd run dev
```

## Local URLs

| Service | URL |
| --- | --- |
| Frontend | `http://localhost:5173` |
| Swagger UI | `http://localhost:5059/swagger` |
| API health | `http://localhost:5059/health` |
| Database health | `http://localhost:5059/health/db` |
| Hangfire dashboard | `http://localhost:5059/hangfire` |

## API endpoints

| Method | Endpoint | Purpose |
| --- | --- | --- |
| `GET` | `/health` | API health check |
| `GET` | `/health/db` | PostgreSQL health check |
| `POST` | `/api/meetings/upload` | Upload audio and enqueue a job |
| `GET` | `/api/meetings/{jobId}/status` | Read job status and progress |
| `GET` | `/api/meetings/history?skip=0&take=50` | Read processing history |
| `GET` | `/api/meetings/{jobId}/result` | Read structured minutes |
| `GET` | `/api/meetings/{jobId}/transcript/download` | Download transcript text |
| `GET` | `/api/meetings/{jobId}/minutes/download` | Download Markdown minutes |
| `POST` | `/api/meetings/{jobId}/retry` | Retry a failed or cancelled job |

## Configuration

Configuration is loaded through standard .NET configuration providers. Values
in `appsettings.json` can be overridden with environment variables by replacing
section separators with double underscores.

Common settings include:

| Setting | Environment variable | Default |
| --- | --- | --- |
| PostgreSQL connection | `ConnectionStrings__DefaultConnection` | Local Docker PostgreSQL |
| Storage root | `Storage__RootPath` | Path configured in `appsettings.json` |
| Maximum upload size | `Storage__MaxUploadSizeMb` | `100` MB |
| FFmpeg location | `AudioProcessing__FfmpegBinaryFolder` | `C:\ffmpeg\ffmpeg.exe` |
| Whisper model size | `Transcription__ModelSize` | `small` |
| Whisper model directory | `Transcription__ModelDirectory` | `Models/Whisper` |
| Automatic model download | `Transcription__AutoDownloadModel` | `true` |
| Transcription language | `Transcription__Language` | `auto` |
| OpenAI API key | `OPENAI_API_KEY` or `OpenAI__ApiKey` | Not configured |
| OpenAI model | `OpenAI__Model` | `gpt-4.1` |
| Transcript character guard | `OpenAI__MaxTranscriptCharactersForMinutes` | `120000` |

The API and Worker must use the same `Storage__RootPath`. The committed local
configuration points to `C:\work\Meetingminutes\MeetingMind\Storage`; update
both application settings or set the environment variable in both processes if
the repository is located elsewhere.

Never commit real API keys or other secrets. Use environment variables or .NET
user secrets on the local machine.

## Local storage

The application creates and uses these folders below `Storage__RootPath`:

```text
Audio/Original/    Uploaded source files
Audio/Processed/   Whisper-ready WAV files
Transcript/        Transcript text files
Minutes/           Generated Markdown minutes
```

Stored paths are kept relative to the configured root, and local filesystem
paths are not returned to the frontend.

## Build and verification

### Backend

Docker Desktop must be running. API and persistence integration tests create
disposable PostgreSQL 16 containers and never use the normal MeetingMind
database.

```powershell
dotnet build MeetingMind.sln
dotnet test MeetingMind.sln
dotnet test MeetingMind.sln `
  --collect:"XPlat Code Coverage" `
  --results-directory artifacts/test
```

### Frontend

```powershell
cd frontend\meetingmind-ui
npm run lint
npm test
npm run test:coverage
npm run build
```

If PowerShell blocks `npm.ps1`, replace `npm` with `npm.cmd`.

An end-to-end check should confirm that an uploaded recording reaches
`Completed`, displays transcript and minutes results, persists in history, and
allows both result files to be downloaded.

## Troubleshooting

### Frontend displays a 502 error

Confirm the API is running on `http://localhost:5059`. The frontend development
proxy requires that address.

### Jobs remain queued

Confirm `MeetingMind.Worker` is running and connected to the same PostgreSQL
database as the API.

### FFmpeg processing fails

Confirm `ffmpeg.exe` exists at the configured path, or set:

```powershell
$env:AudioProcessing__FfmpegBinaryFolder = "C:\path\to\ffmpeg"
```

### Whisper model download fails

Confirm the Worker has internet access for its first model download, or set
`Transcription__ModelPath` to an existing compatible Whisper model file.

### Minutes generation fails

Confirm `OPENAI_API_KEY` is set in the same PowerShell window that starts the
Worker.

### Database health fails

```powershell
docker compose ps
docker compose logs meetingmind-postgres
```

Then confirm the database migration has been applied.

## Shutting down

Press `Ctrl+C` in the frontend, API, and Worker windows. Stop PostgreSQL with:

```powershell
docker compose down
```

Do not add `-v` unless you intentionally want to delete the PostgreSQL data
volume.

## Documentation

- [Product Requirements Document](docs/PRD.md)
- [Software Architecture Document](docs/SAD.md)
- [Functional Specification Document](docs/FSD.md)
- [Backlog](BACKLOG.md)
- [Agent instructions](AGENTS.md)

## License

No license has been added.

# MeetingMind AI

MeetingMind AI is a local AI meeting-minutes application that converts uploaded
audio into a transcript and structured meeting minutes. It identifies the
meeting summary, attendees, discussion points, decisions, action items, risks,
and next steps.

The application uses an asynchronous workflow: the API accepts an upload and
returns a job ID immediately, while a separate Hangfire worker performs audio
conversion, transcription, minutes generation, and result storage.

## Current status

Phase 2 local hardening is complete and was verified on 2026-07-19. The
sanitized completion evidence is preserved in the
[Phase 2 verification record](docs/archive/phase2/PHASE2_VERIFICATION.md).
Phase 3 requirements and architecture are approved and P3-01 documentation
reconciliation is in progress. Phase 3 production features are not yet
implemented; the implemented workflow described below remains the Phase 2
baseline. See the [active backlog](BACKLOG.md) and
[Phase 3 plan](docs/phase3/PLAN.md).

The implemented workflow supports:

- Uploading MP3, WAV, M4A, and AAC audio files.
- Validating file extension, MIME type, filename, and configurable file size.
- Returning a job ID without waiting for audio or AI processing.
- Tracking job stage, status, progress, and errors.
- Tracking live processing and total elapsed duration.
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

The Worker requires the absolute path to either the FFmpeg executable or the
folder containing it. The path is configured outside committed files as shown
below.

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

Create the shared storage folder and configure its absolute path once for the
current Windows user. API and Worker then inherit exactly the same value:

```powershell
$storageRoot = (New-Item -ItemType Directory -Force -Path .\Storage).FullName
[Environment]::SetEnvironmentVariable("Storage__RootPath", $storageRoot, "User")
$env:Storage__RootPath = $storageRoot
```

Configure FFmpeg for the Worker and API readiness check with an absolute
executable or folder path:

```powershell
$ffmpegPath = "C:\ffmpeg\build\bin\ffmpeg.exe"
[Environment]::SetEnvironmentVariable(
  "AudioProcessing__FfmpegBinaryFolder",
  $ffmpegPath,
  "User")
```

Store the OpenAI key in the Worker's .NET user-secret store:

```powershell
dotnet user-secrets set "OpenAI:ApiKey" "your-openai-api-key" `
  --project src\MeetingMind.Worker
```

Never place the key in `appsettings*.json` or any other committed file.

Now restore, migrate, build, and test:

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

Open a new PowerShell window after setting the user environment variables:

```powershell
cd path\to\MeetingMind
dotnet run --project src\MeetingMind.Api --launch-profile http
```

The API listens on `http://localhost:5059`.

### 4. Start the Worker

Open another PowerShell window:

```powershell
cd path\to\MeetingMind
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
| API readiness | `http://localhost:5059/health/ready` |
| Database health | `http://localhost:5059/health/db` |
| Hangfire dashboard | `http://localhost:5059/hangfire` |

## API endpoints

| Method | Endpoint | Purpose |
| --- | --- | --- |
| `GET` | `/health` | API health check |
| `GET` | `/health/ready` | PostgreSQL, storage-write, FFmpeg, and Whisper-model readiness |
| `GET` | `/health/db` | PostgreSQL health check |
| `POST` | `/api/meetings/upload` | Upload audio and enqueue a job |
| `GET` | `/api/meetings/{jobId}/status` | Read job status, progress, and duration |
| `GET` | `/api/meetings/history?skip=0&take=50` | Read processing history with duration |
| `GET` | `/api/meetings/{jobId}/result` | Read structured minutes |
| `GET` | `/api/meetings/{jobId}/transcript/download` | Download transcript text |
| `GET` | `/api/meetings/{jobId}/minutes/download` | Download Markdown minutes |
| `POST` | `/api/meetings/{jobId}/retry` | Retry a failed or cancelled job |

## Duration semantics

Status and history return two non-negative whole-second fields:

- `processingDurationSeconds` measures Worker execution for the current or
  most recently completed attempt.
- `totalDurationSeconds` measures elapsed time since the original upload,
  including queue time and manual retries.

An initial queued job has zero processing duration while total duration grows.
Both values grow while processing. Terminal values stop at completion, with
`UpdatedAt` used only when older terminal data lacks `CompletedAt`.

After manual retry, the queued job keeps showing the previous attempt's final
processing duration. Processing resets when the Worker starts the new attempt;
total duration continues from the original upload. The frontend displays both
values as `0s`, `2m 05s`, or `1h 02m 03s` and ticks the selected active job once
per second between API polls.

During an automatic-retry delay, processing duration remains live from the
original automatic-recovery run's `StartedAt`. A later manual retry starts a new
processing-duration attempt.

## Automatic retry behavior

The Worker uses Hangfire for two automatic retries after the initial execution,
with default delays of 10 and 60 seconds. Transient storage, network, provider,
and database failures are retried. Known invalid input, missing dependencies,
unsafe paths, unsupported media, invalid provider responses, and non-transient
database errors fail immediately. Unclassified failures use the same bounded
automatic-retry policy.

While waiting, a job is `Queued` at its last stage and progress and exposes
`automaticRetryCount`, `automaticRetryLimit`, and `nextRetryAt` through status
and history. Retries reuse a valid transcript checkpoint first, then valid
processed audio; missing checkpoints fall back to the nearest safe earlier
stage. Exhausted and permanent failures remain eligible for manual retry, which
receives a fresh automatic-retry budget.

## Frontend experience

History is displayed in 20-job pages with Previous/Next controls and a total job
count. Changing pages does not clear the selected meeting or interrupt its
single non-overlapping status polling loop. History cards show automatic retry
attempts, while details show scheduled retry timing, exhausted-retry guidance,
and the manual retry action for eligible terminal jobs.

The selected meeting remains synchronized with its history card. On narrow
screens, selecting a meeting focuses and scrolls its details into view, and
pagination controls become full-width touch targets. Status, stage, and progress
share one polite live announcement; selection, pagination, and actionable
errors use intentional keyboard focus. Missing results and known safe error
codes provide state-specific Refresh or Retry guidance.

## Long-meeting generation

Transcripts up to 120,000 characters use one structured OpenAI request. Longer
transcripts are normalized and split into sequential 60,000-character chunks
with up to 1,500 characters of textual overlap. Boundaries prefer paragraphs,
then sentences, whitespace, and finally a hard bounded cut. The configured
overall maximum is 1,200,000 characters; larger transcripts fail safely and are
never silently truncated.

Every chunk produces the complete minutes schema. Application-layer
orchestration normalizes and deterministically deduplicates partial results,
then reduces bounded groups through one or more OpenAI aggregation tiers. Each
aggregation input is limited to 120,000 serialized characters. All eight output
sections are preserved, and action items with missing metadata are enriched
without discarding genuinely conflicting variants.

Long generation advances `GeneratingMinutes` progress from 60–85% during
partial calls and 86–89% during aggregation. A transient call failure uses the
P2-05 Hangfire policy; the retry reuses the persisted transcript and regenerates
partials because temporary partial results are not persisted.

## Configuration

Configuration is loaded through standard .NET configuration providers. Values
in `appsettings.json` can be overridden with environment variables by replacing
section separators with double underscores.

Common settings include:

| Setting | Environment variable | Default |
| --- | --- | --- |
| PostgreSQL connection | `ConnectionStrings__DefaultConnection` | Local Docker PostgreSQL |
| Storage root | `Storage__RootPath` | Required absolute path shared by API and Worker |
| Maximum upload size | `Storage__MaxUploadSizeMb` | `100` MB |
| FFmpeg location | `AudioProcessing__FfmpegBinaryFolder` | Required absolute executable or folder path |
| Whisper model size | `Transcription__ModelSize` | `small` |
| Whisper model directory | `Transcription__ModelDirectory` | `Models/Whisper` |
| Automatic model download | `Transcription__AutoDownloadModel` | `true` |
| Transcription language | `Transcription__Language` | `auto` |
| OpenAI API key | `OpenAI__ApiKey` | Required by Worker; no committed default |
| OpenAI model | `OpenAI__Model` | `gpt-4.1` |
| Single-pass minutes limit | `MeetingMinutesGeneration__SinglePassMaxCharacters` | `120000` |
| Minutes chunk size | `MeetingMinutesGeneration__ChunkSizeCharacters` | `60000` |
| Minutes chunk overlap | `MeetingMinutesGeneration__ChunkOverlapCharacters` | `1500` |
| Maximum transcript | `MeetingMinutesGeneration__MaxTranscriptCharacters` | `1200000` |
| Maximum aggregation input | `MeetingMinutesGeneration__MaxAggregationInputCharacters` | `120000` |
| Automatic retry delays | `AutomaticRetry__DelaysInSeconds__0`, `AutomaticRetry__DelaysInSeconds__1` | `10`, `60` seconds |
| Retention enabled | `StorageRetention__Enabled` | `false` |
| Terminal-job retention | `StorageRetention__RetentionDays` | `30` days |
| Retention schedule | `StorageRetention__Schedule` | `0 2 * * *` (daily at 02:00) |
| Retention batch size | `StorageRetention__BatchSize` | `100` jobs; maximum `1000` |

The API and Worker must use the same `Storage__RootPath`. Configure it once as
a user environment variable so both processes inherit the identical absolute
path. Confirm the configured value in any new PowerShell window with:

```powershell
[Environment]::GetEnvironmentVariable("Storage__RootPath", "User")
```

The local Docker connection string is a committed development-only default.
Override it with `ConnectionStrings__DefaultConnection` when needed. If using
.NET user secrets for that override, set the same value for both projects:

```powershell
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "<connection-string>" `
  --project src\MeetingMind.Api
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "<connection-string>" `
  --project src\MeetingMind.Worker
```

API startup validates storage and upload settings and loads FFmpeg/Whisper
locations for readiness. Worker startup strictly validates storage, FFmpeg,
Whisper, OpenAI, retry, minutes-generation, and retention settings. Missing or
invalid required Worker configuration stops startup and names the setting that
must be corrected. `/health/ready` returns HTTP 503 until PostgreSQL and storage
are usable and both the FFmpeg executable and Whisper model file exist.

Never commit real API keys or other secrets. Use environment variables or .NET
user secrets on the local machine.

## Local storage

The application creates and uses these folders below `Storage__RootPath`:

```text
Audio/Original/    Uploaded source files
Audio/Processed/   Whisper-ready WAV files
Transcript/        Transcript text files
Minutes/           Generated Markdown minutes
.health/           Temporary zero-byte readiness probes
```

Stored paths are kept relative to the configured root, and local filesystem
paths are not returned to the frontend.

Retention cleanup is disabled by default. When enabled, the Worker registers a
daily Hangfire cleanup. Only expired `Completed`, `Failed`, or `Cancelled` jobs
without a scheduled retry are eligible. Cleanup locks and revalidates each job,
prevalidates every path, deletes referenced artifacts, and then deletes the job;
transcript and minutes rows cascade. Missing in-root files are harmless. Unsafe
paths or other deletion failures keep the database record for a later retry.
Rooted, escaping, symbolic-link, and junction paths are rejected.

Status and history responses expose a stable `errorCode` and safe
`errorMessage`. Worker and Hangfire failure records exclude raw exception
messages, provider payloads, transcripts, secrets, and physical paths.

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

Confirm `ffmpeg.exe` exists at the configured absolute path, then set:

```powershell
$env:AudioProcessing__FfmpegBinaryFolder = "C:\ffmpeg\build\bin\ffmpeg.exe"
```

### Whisper model download fails

Confirm the Worker has internet access for its first model download, or set
`Transcription__ModelPath` to an existing compatible Whisper model file.

### Minutes generation fails

Confirm `OpenAI:ApiKey` exists in the Worker user-secret store, or set
`OpenAI__ApiKey` in the same PowerShell window that starts the Worker.

### Database health fails

```powershell
docker compose ps
docker compose logs meetingmind-postgres
```

Then confirm the database migration has been applied.

### Readiness returns HTTP 503

Inspect the named checks returned by `/health/ready`. Confirm PostgreSQL is
running, `Storage__RootPath` is writable, `AudioProcessing__FfmpegBinaryFolder`
points to FFmpeg, and `Transcription__ModelPath` points to a readable model. If
automatic model download is used, readiness remains unhealthy for
`whisper_model` until the Worker has downloaded the configured model below the
shared storage root.

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
- [Local run and test guide](LOCAL_RUN_AND_TEST_GUIDE.md)
- [Backlog](BACKLOG.md)
- [Phase 3 plan](docs/phase3/PLAN.md)
- [Phase 3 UX contract](docs/phase3/UX.md)
- [Phase 2 archive](docs/archive/phase2/README.md)
- [Phase 2 verification record](docs/archive/phase2/PHASE2_VERIFICATION.md)
- [Agent instructions](AGENTS.md)

## License

No license has been added.

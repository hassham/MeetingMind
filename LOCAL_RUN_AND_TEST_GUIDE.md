# Run MeetingMind locally

Follow these steps in order. Run the commands from the solution folder unless
the step says otherwise.

## 0. One-time prerequisites and configuration

Install:

- .NET 8 SDK
- Docker Desktop
- Node.js and npm
- FFmpeg
- An OpenAI API key

Configure the shared storage folder:

```powershell
$storageRoot = (New-Item -ItemType Directory -Force -Path .\Storage).FullName
[Environment]::SetEnvironmentVariable("Storage__RootPath", $storageRoot, "User")
$env:Storage__RootPath = $storageRoot
```

Configure your FFmpeg executable:

```powershell
$ffmpegPath = "C:\ffmpeg\build\bin\ffmpeg.exe"
[Environment]::SetEnvironmentVariable("AudioProcessing__FfmpegBinaryFolder", $ffmpegPath, "User")
$env:AudioProcessing__FfmpegBinaryFolder = $ffmpegPath
```

Store the OpenAI API key for the Worker:

```powershell
dotnet user-secrets set "OpenAI:ApiKey" "your-openai-api-key" --project src\MeetingMind.Worker
```

Open new PowerShell windows after setting user environment variables. Never
commit the OpenAI key.

### Recommended PowerShell window layout

Use four PowerShell windows so that each long-running application remains
visible and can be stopped independently:

| Window | Purpose | Keep running? |
| --- | --- | --- |
| 1 | Docker, migrations, health checks, and test commands | No |
| 2 | MeetingMind Web API | Yes |
| 3 | MeetingMind Worker | Yes |
| 4 | React frontend | Yes |

New PowerShell windows normally inherit saved user environment variables at
startup, but the API and Worker commands below also set their required values
explicitly. This makes each backend window self-contained.

## 1. Start the PostgreSQL database in Docker

Start Docker Desktop, then run:

```powershell
docker compose up -d
```

## 2. Check that the database is running

```powershell
docker compose ps
```

The `meetingmind-postgres` service should show as running and healthy.

For a direct PostgreSQL check:

```powershell
docker compose exec meetingmind-postgres pg_isready -U meetingmind_user -d meetingmind
```

Expected result:

```text
/var/run/postgresql:5432 - accepting connections
```

If the database is not healthy, inspect its logs:

```powershell
docker compose logs meetingmind-postgres
```

Do not continue until PostgreSQL is accepting connections.

## 3. Restore .NET dependencies and apply migrations

Restore the solution and local .NET tools:

```powershell
dotnet tool restore
dotnet restore MeetingMind.sln
```

Apply the database migrations:

```powershell
$env:Storage__RootPath = (New-Item -ItemType Directory -Force -Path .\Storage).FullName; dotnet ef database update --project src\MeetingMind.Infrastructure --startup-project src\MeetingMind.Api
```

Setting `Storage__RootPath` on the same line ensures the API startup used by EF
can create its service provider even when this PowerShell window was opened
before the user environment variable was saved. Run the command from the
solution folder.

Build the solution before starting the applications:

```powershell
dotnet build MeetingMind.sln
```

## 4. Start the Web API in PowerShell Window 2

Open a new PowerShell window and move to the solution folder. Replace the
example repository path if your checkout is elsewhere:

```powershell
$repo = "C:\work\Meetingminutes\MeetingMind"
Set-Location $repo
$env:Storage__RootPath = (New-Item -ItemType Directory -Force -Path .\Storage).FullName; $env:AudioProcessing__FfmpegBinaryFolder = "C:\ffmpeg\build\bin\ffmpeg.exe"; dotnet run --project src\MeetingMind.Api --launch-profile http
```

Keep this window running after the API starts.

The API should listen on:

```text
http://localhost:5059
```

Use PowerShell Window 1 to check the API and database while Window 2 continues
running:

```powershell
Invoke-RestMethod http://localhost:5059/health
Invoke-RestMethod http://localhost:5059/health/db
```

Both requests should report `Healthy`.

The full readiness endpoint may still report the Whisper model as unhealthy
until the Worker has started and downloaded it.

## 5. Start the Worker in PowerShell Window 3

Open a separate PowerShell window:

```powershell
$repo = "C:\work\Meetingminutes\MeetingMind"
Set-Location $repo
$env:Storage__RootPath = (New-Item -ItemType Directory -Force -Path .\Storage).FullName; 
$env:AudioProcessing__FfmpegBinaryFolder = "C:\ffmpeg\build\bin\ffmpeg.exe"; 
dotnet run --project src\MeetingMind.Worker
```

Because both commands resolve `.\Storage` from the same solution folder, API
and Worker receive the same absolute storage root. Keep both windows open. Do
not start API and Worker in the same terminal because stopping one process
would also interrupt the workflow for the other.

The Worker should start a Hangfire server and listen to the `default` queue.
The first transcription may take longer while the Whisper model is downloaded.

After the Worker is ready, check all dependencies:

```powershell
Invoke-RestMethod http://localhost:5059/health/ready
```

Confirm these checks are `Healthy`:

- `database`
- `storage`
- `ffmpeg`
- `whisper_model`

You can also confirm that the Worker is registered from the Hangfire dashboard:

```text
http://localhost:5059/hangfire
```

On a fresh machine, `whisper_model` can remain unhealthy until the first job
reaches transcription, because model download is demand-driven. In that case:

1. Confirm `database`, `storage`, and `ffmpeg` are healthy.
2. Continue to the frontend and upload a short privacy-safe test recording.
3. Leave the Worker running while it downloads the model and processes the job.
4. Request `/health/ready` again and confirm all four checks are healthy.

Do not process real meeting audio until the first model download and readiness
check have completed successfully.

## 6. Install and start the frontend in PowerShell Window 4

Open a third long-running application window. The frontend does not require the
.NET storage or FFmpeg environment variables:

```powershell
$repo = "C:\work\Meetingminutes\MeetingMind"
Set-Location "$repo\frontend\meetingmind-ui"

npm.cmd install
npm.cmd run dev
```

Keep Window 4 running alongside the API and Worker windows.

Open:

```text
http://localhost:5173
```

The frontend development server proxies API requests to
`http://localhost:5059`.

## 7. Test the complete application

1. Open `http://localhost:5173`.
2. Select a supported MP3, WAV, M4A, or AAC recording.
3. Confirm the upload appears immediately in History as `Queued`.
4. Confirm the status advances through processing stages.
5. Wait until the job reaches `Completed` and 100%.
6. Confirm the history card and selected-job status agree.
7. Confirm both structured Minutes and Transcript content are displayed.
8. Download Transcript and Minutes.
9. Confirm both downloaded files are non-empty.
10. Refresh the browser and confirm the job remains in History.

Use privacy-safe test audio rather than a real confidential meeting recording.

## 8. Run the automated test suites

Keep Docker Desktop running.

Backend tests:

```powershell
dotnet test MeetingMind.sln
```

Frontend tests:

```powershell
Set-Location frontend\meetingmind-ui
npm.cmd run lint
npm.cmd test -- --run
npm.cmd run test:coverage
npm.cmd run build
Set-Location ..\..
```

Expected Phase 2 baseline:

- 87 backend tests pass
- 19 frontend tests pass
- backend and frontend builds complete without errors

## Common startup errors

### `Storage:RootPath` is required

Set the variable in the same terminal that starts API or Worker:

```powershell
$env:Storage__RootPath = (Resolve-Path .\Storage).Path
```

### `AudioProcessing:FfmpegBinaryFolder` is required

```powershell
$env:AudioProcessing__FfmpegBinaryFolder = "C:\ffmpeg\build\bin\ffmpeg.exe"
```

The setting name is `FfmpegBinaryFolder`, even when its value points directly
to `ffmpeg.exe`.

### Jobs remain queued

Check that:

- The Worker process is running.
- The Hangfire dashboard shows an active server.
- API and Worker use the same database and `Storage__RootPath`.
- The Worker terminal has no startup configuration error.

### `/health/ready` returns HTTP 503

Read the individual readiness check results. Then verify PostgreSQL, storage,
FFmpeg, and the Whisper model before continuing.

## Stop MeetingMind

Press `Ctrl+C` in this order:

1. Frontend
2. Worker
3. Web API

Then stop PostgreSQL while preserving its data:

```powershell
docker compose down
```

Do not use `docker compose down -v` unless you intentionally want to delete the
database volume.

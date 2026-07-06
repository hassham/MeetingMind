# MeetingMind AI

MeetingMind AI is an AI meeting-minutes generator that turns uploaded meeting
audio into structured, searchable meeting records: transcripts, summaries,
decisions, action items, risks, and next steps.

The project is being built as a local-first MVP with a cloud-ready architecture.
Long-running audio and AI work is designed to run outside the HTTP request path
through background jobs, keeping the API responsive while processing continues.

## Current Status

This repository is currently in the foundation/scaffolding stage.

Already scaffolded:

- ASP.NET Core solution with Clean Architecture-oriented projects.
- PostgreSQL Docker service.
- ASP.NET Core API with Swagger/OpenAPI.
- Basic API health endpoints.
- EF Core `DbContext` registration.
- Vite React frontend skeleton.
- Project docs and backlog.

Still in progress:

- Domain entities and EF mappings.
- Upload, status, result, retry, and download endpoints.
- Hangfire background processing.
- FFmpeg audio processing.
- Whisper transcription integration.
- OpenAI GPT meeting-minutes generation.
- MeetingMind-specific frontend UI.

For the live task list, see [BACKLOG.md](BACKLOG.md).

## Product Goal

The MVP is complete when a user can:

1. Upload a supported audio file.
2. Receive an immediate job ID.
3. Track background processing progress.
4. View the generated transcript.
5. View structured meeting minutes.
6. Download transcript and minutes files.
7. Review processing history.
8. Retry failed jobs.

## Tech Stack

Backend:

- ASP.NET Core Web API
- Entity Framework Core
- PostgreSQL
- Hangfire
- Clean Architecture
- Dependency Injection

Frontend:

- React
- Vite
- Material UI
- Axios

Processing and AI:

- FFmpeg for audio conversion/normalization
- OpenAI Whisper for transcription
- OpenAI GPT for structured meeting-minutes generation

Storage:

- Local file storage for MVP
- Designed to be replaceable with S3 or Azure Blob Storage later

## Architecture

The intended layering is:

```text
Frontend
  -> API
    -> Application
      -> Domain
    -> Infrastructure
```

Important architecture rules:

- Controllers should not call FFmpeg, Whisper, OpenAI, Hangfire, or the database
  directly for business workflows.
- Application defines interfaces.
- Infrastructure implements external dependencies.
- Long-running work must run asynchronously through Hangfire.
- The API should create a job and return immediately.
- No authentication is planned for v1.

Main processing flow:

```text
Upload audio
  -> Save original file
  -> Create MeetingJob
  -> Enqueue Hangfire job
  -> Validate audio
  -> Transcode with FFmpeg
  -> Transcribe with Whisper
  -> Generate minutes with OpenAI GPT
  -> Save transcript and minutes
  -> Mark job completed
```

## Repository Structure

```text
.
|-- docs/
|   |-- PRD.md
|   |-- SAD.md
|   `-- FSD.md
|-- frontend/
|   `-- meetingmind-ui/
|-- src/
|   |-- MeetingMind.Api/
|   |-- MeetingMind.Application/
|   |-- MeetingMind.Domain/
|   |-- MeetingMind.Infrastructure/
|   |-- MeetingMind.Shared/
|   `-- MeetingMind.Worker/
|-- AGENTS.md
|-- BACKLOG.md
|-- docker-compose.yml
`-- MeetingMind.sln
```

## Local Development

Prerequisites:

- .NET 8 SDK
- Docker Desktop
- Node.js and npm
- FFmpeg, once audio processing is implemented

Start PostgreSQL:

```bash
docker compose up -d
```

Build the backend:

```bash
dotnet build MeetingMind.sln
```

Run the API:

```bash
dotnet run --project src/MeetingMind.Api
```

Run the frontend:

```bash
cd frontend/meetingmind-ui
npm install
npm run dev
```

The frontend is currently still the default Vite starter and will be replaced
as the MVP UI is implemented.

## Configuration

The local API currently uses PostgreSQL from `docker-compose.yml`.

Do not commit real secrets. Future OpenAI configuration should be supplied via
environment variables or .NET user secrets, for example:

```text
OpenAI__ApiKey
OpenAI__TranscriptionModel
OpenAI__MinutesModel
```

## Documentation

Project documents:

- [Product Requirements Document](docs/PRD.md)
- [Software Architecture Document](docs/SAD.md)
- [Functional Specification Document](docs/FSD.md)
- [Backlog](BACKLOG.md)
- [Agent/project instructions](AGENTS.md)

## Roadmap

Planned implementation phases:

1. Clean foundation and contracts.
2. Persistence model and database migration.
3. Local storage and upload job creation.
4. Hangfire background job integration.
5. Background job shell and status tracking.
6. FFmpeg audio processing.
7. Whisper transcription integration.
8. OpenAI GPT meeting-minutes generation.
9. Retry and processing history.
10. Frontend MVP.

## License

No license has been added yet.

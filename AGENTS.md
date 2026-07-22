# AGENTS.md — MeetingMind AI (AI Meeting Minutes Generator)

This file is durable, always-loaded context for coding agents (Codex, etc.)
working in this repo. Keep it short and current. For "what's done / what's
next," see `BACKLOG.md` instead — this file should rarely need edits mid-sprint.

## Project summary

MeetingMind AI converts uploaded meeting audio into a transcript and
structured, AI-generated meeting minutes (decisions, action items, risks,
next steps). The completed MVP/Phase 2 hardening cycle and the active Phase 3
product-expansion cycle run as a trusted local application. Cloud deployment is
a future delivery cycle, not part of Phase 3.

Full specs live in `/docs`:
- `docs/PRD.md` — product requirements, scope, user stories
- `docs/SAD.md` — architecture, ADRs, data model, API contract
- `docs/FSD.md` — functional requirements (FR-001..FR-010), NFRs

Read the relevant doc before implementing a feature that touches scope,
architecture, or requirements — don't guess when it's already specified.

## Tech stack

- **Frontend:** React, Material UI, Axios — lives in `/frontend`
- **Backend:** ASP.NET Core Web API, Entity Framework Core, Clean Architecture, DI
- **Database:** PostgreSQL (Docker, see `docker-compose.yml`)
- **Background processing:** Hangfire for queueing, background jobs, retries, and workflow execution
- **Audio processing:** FFmpeg (wrapped behind `IAudioProcessingService`, never called directly from controllers)
- **AI services:** local Whisper.net (transcription), OpenAI GPT (minutes generation)
- **Storage:** local file storage through Phase 3 (`Storage/Audio`,
  `Storage/Transcript`, `Storage/Minutes`), behind `IFileStorageService` for a
  possible future S3/Blob swap

## Solution structure (actual — keep this in sync with reality)

```
MeetingMind.sln
docs/                       PRD.md, SAD.md, FSD.md
frontend/                   React app (frontend/meetingmind-ui)
src/
  MeetingMind.Api/           Controllers, DTOs — thin, no business logic
  MeetingMind.Application/   Use cases, service interfaces, orchestration
  MeetingMind.Domain/        Entities, enums, business rules — no external deps
  MeetingMind.Infrastructure/ EF Core, FFmpeg wrapper, OpenAI clients, file storage
  MeetingMind.Shared/         Cross-cutting DTOs/contracts shared by Api and Worker
  MeetingMind.Worker/         Background job consumer — the process that actually
                               runs the meeting-processing workflow once a job is
                               dequeued via Hangfire. Api never processes jobs
                               itself; it only enqueues and reads status from the DB.
tests/
  MeetingMind.Unit.Tests/
  MeetingMind.Api.IntegrationTests/
  MeetingMind.Infrastructure.IntegrationTests/
  MeetingMind.Worker.Tests/
```

The `Worker` project is where Hangfire job execution lives. Api stays completely unaware of
*how* processing happens; it only creates the `MeetingJob` record, enqueues,
and returns immediately.

## Architecture rules (non-negotiable)

- **Clean Architecture layering:** `MeetingMind.Api` → `MeetingMind.Application` →
  `MeetingMind.Domain` ← `MeetingMind.Infrastructure`. `MeetingMind.Worker`
  depends on `Application`/`Infrastructure` the same way `Api` does — it is a
  second entry point into the same core, not a separate codebase.
  Domain has no external dependencies. Application defines interfaces;
  Infrastructure implements them. Don't let controllers (or the Worker's job
  handler) call FFmpeg, OpenAI, or the database directly — go through the
  abstractions.
- **Everything long-running is async.** The API must return immediately
  (job created, `jobId` returned) and never block on transcoding,
  transcription, or LLM calls. This is the core problem being solved —
  do not reintroduce synchronous chains here.
- **Every infrastructure dependency is behind an interface**
  (`IFileStorageService`, `IAudioProcessingService`, `ITranscriptionService`,
  `IMeetingMinutesGenerationClient`, `IBackgroundJobService`).
  `IMeetingMinutesService` is the Application-layer short/long transcript
  orchestrator; Infrastructure implements only the single-call generation
  client. This is what makes the cloud migration path (see SAD §14) possible
  later — don't bypass it for convenience.
- **No authentication in Phase 3.** All endpoints, including retry and the
  Hangfire dashboard, remain unauthenticated because Phase 3 is restricted to a
  trusted local deployment (see PRD §6, FSD §§1 and 7, SAD §11). Do not add auth
  scaffolding unless a later delivery cycle explicitly changes that boundary.

## Background job / queue decision

Decision recorded 2026-07-06: use **Hangfire** for queueing, background job
execution, retries, and workflow orchestration in the local application.

Hangfire is the implementation behind `IBackgroundJobService`, running inside
`MeetingMind.Worker`. Controllers must still depend on application
abstractions and must not call Hangfire, FFmpeg, Whisper, GPT/OpenAI, or
persistence directly for long-running work.

The processing chain is decoupled as:
`upload -> create MeetingJob -> enqueue Hangfire job -> FFmpeg -> local
Whisper.net transcription -> Application minutes orchestration -> bounded
OpenAI GPT generation/aggregation -> save results`.

Phase 1 manual retry remains available for failed or cancelled jobs. P2-05 adds
two configurable Hangfire retries (10 and 60 seconds by default) for classified
transient and bounded unclassified failures. Permanent failures run once;
exhausted failures remain manually retryable with a fresh automatic budget.

## Data model (summary — see SAD §7 / FSD §5 for full field lists)

- `MeetingJob` — Id, file paths, Status, Stage, Progress, ErrorCode, ErrorMessage, timestamps
- `MeetingTranscript` — Id, MeetingJobId, TranscriptText, TranscriptFilePath
- `MeetingMinutes` — Id, MeetingJobId, Title, Summary, DecisionsJson, ActionItemsJson, RisksJson, NextStepsJson, FullMinutesJson

Job status enum: `Queued | Processing | Completed | Failed | Cancelled`
Job stage enum: `Uploaded | Validating | Transcoding | Transcribing | GeneratingMinutes | SavingResults | Completed | Failed`

## Conventions

- SOLID principles, Repository Pattern, Dependency Injection throughout.
- Config is environment-based; **never commit secrets** (API keys, connection
  strings) to Git — use environment variables / user secrets locally.
- Don't log full transcripts or API keys.
- Validate file extension, MIME type, and size on every upload; prevent
  path traversal; never expose local file paths to the frontend.

## Build & test

- Build: `dotnet build MeetingMind.sln`
- Test: `dotnet test MeetingMind.sln`
- Frontend: `npm run lint` / `npm run build` / `npm test` (in
  `/frontend/meetingmind-ui`)
- Local infra: PostgreSQL via `docker compose up`

## Prompting approach for this project

Development uses a **sequential, verify-before-proceeding** style: implement
one stage or component at a time, confirm it builds/passes tests, then move
to the next. Don't batch multiple unrelated features into one change without
being asked. Always check `BACKLOG.md` at the start of a session.

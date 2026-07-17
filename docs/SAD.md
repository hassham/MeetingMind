# Software Architecture Document

**Product:** MeetingMind AI

**Version:** 2.0

**Status:** Active Phase 2 architecture

**Architecture style:** Clean Architecture modular monolith with separate API
and Worker entry points

**Deployment boundary:** Trusted local environment

**Last reconciled with code:** 2026-07-17

## 1. Architecture goals

- Keep HTTP requests responsive while long-running media and AI work proceeds
  independently.
- Keep Domain and Application behavior independent of infrastructure providers.
- Persist job state so browser refreshes and process restarts do not lose work.
- Make local infrastructure replaceable in a future cloud delivery cycle.
- Provide testable boundaries for storage, audio, transcription, minutes,
  background jobs, and persistence.
- Improve reliability and observability during Phase 2 without widening the
  trusted-local security boundary.

## 2. System context

```text
User
  -> React frontend
  -> MeetingMind.Api
       -> PostgreSQL (MeetingMind + Hangfire data)
       -> local file storage through IFileStorageService
       -> Hangfire client enqueue

MeetingMind.Worker
  -> Hangfire server reads queued job
  -> MeetingProcessingJob orchestrates Application interfaces
       -> FFmpeg through IAudioProcessingService
       -> local Whisper.net through ITranscriptionService
       -> OpenAI GPT through IMeetingMinutesService
       -> PostgreSQL through IMeetingJobRepository
       -> local file storage through IFileStorageService
```

The API and Worker are separate processes and separate entry points into the
same core. The API hosts the Hangfire dashboard and configures a Hangfire client
but does not run a Hangfire server. The Worker runs the Hangfire server and is
the only process that executes the meeting-processing pipeline.

## 3. Solution structure and dependency rules

```text
MeetingMind.sln
frontend/meetingmind-ui/       React application
src/
  MeetingMind.Domain/          Entities, enums, business rules
  MeetingMind.Application/     Use cases, result contracts, provider and repository interfaces
  MeetingMind.Infrastructure/  EF Core, local storage, FFmpeg, Whisper.net, OpenAI, Hangfire client
  MeetingMind.Api/             HTTP controllers and API composition root
  MeetingMind.Worker/          Hangfire server, processing handler, Worker composition root
  MeetingMind.Shared/          Cross-entry-point contracts when genuinely shared
tests/
  MeetingMind.Domain.Tests/    Existing Domain/Application-focused unit tests
```

Allowed project dependencies:

```text
Domain                  -> no project dependencies
Application             -> Domain, Shared
Infrastructure          -> Application, Domain
Api                     -> Application, Infrastructure, Shared
Worker                  -> Application, Infrastructure, Shared
```

Rules:

- Domain must not reference EF Core, Hangfire, FFmpeg, Whisper.net, OpenAI, ASP.NET,
  or filesystem APIs.
- Application owns use-case and infrastructure contracts.
- Controllers delegate to Application services and contain no processing or
  persistence logic.
- `MeetingProcessingJob` is an orchestration handler in the Worker. It depends
  on Application interfaces and must not instantiate providers or a database
  context directly.
- Infrastructure implementations may use provider SDKs but must not leak
  provider types into Application contracts.
- No MediatR or CQRS dependency exists. It must not be described as current
  architecture or added without an approved use case.

## 4. Component responsibilities

### 4.1 Frontend

- Upload one supported audio file.
- Read paginated history and select a job.
- Poll active jobs.
- Display status, stage, progress, errors, timestamps, and Phase 2 duration.
- Display structured minutes and transcript content.
- Download transcript and Markdown minutes.
- Initiate eligible manual retries.

The frontend performs no media, transcription, summarization, or persistence
work.

### 4.2 API

- Validate the HTTP upload shape and delegate upload behavior.
- Return HTTP 202 after validation, storage, persistence, and enqueueing.
- Expose status, history, results, downloads, and manual retry.
- Expose local Swagger, health endpoints, and the unrestricted local Hangfire
  dashboard.

The API never waits for FFmpeg, Whisper.net, or OpenAI.

### 4.3 Worker

- Host the Hangfire server.
- Resolve `IMeetingProcessingJob` and execute `ProcessMeetingAsync(jobId)`.
- Orchestrate stage/status updates and infrastructure interfaces.
- Record sanitized failures and Phase 2 retry behavior.

### 4.4 PostgreSQL

- Persist MeetingMind job, transcript, and minutes records through EF Core.
- Persist Hangfire queues, state, and execution metadata through the Hangfire
  PostgreSQL provider.

Both processes must use the same connection string.

### 4.5 Local file storage

`IFileStorageService` isolates local storage. Stored references are relative to
the configured root. The service owns safe internal filenames, directory
creation, root confinement, reads, writes, and the Phase 2 retention boundary.

```text
Storage/
  Audio/Original/
  Audio/Processed/
  Transcript/
  Minutes/
```

The API must never return these physical paths.

### 4.6 Audio processing

`IAudioProcessingService` isolates FFmpeg. The local implementation converts
accepted audio to PCM signed 16-bit, 16 kHz, mono WAV before transcription.

### 4.7 Transcription

`ITranscriptionService` isolates the transcription provider. The current
implementation uses Whisper.net and a local CPU runtime. Model path, directory,
size, optional automatic download, and language are configuration concerns.

Transcription does not use the OpenAI Whisper API.

### 4.8 Meeting-minutes generation

`IMeetingMinutesService` isolates GPT generation. The current infrastructure
implementation uses the OpenAI .NET SDK and strict JSON-schema output for title,
summary, attendees, discussion points, decisions, action items, risks, and next
steps.

The Phase 1 implementation is single-pass and rejects transcripts over 120,000
characters. P2-06 introduces Application-layer long-transcript orchestration so
short transcripts remain single-pass and long transcripts use bounded partial
generation and final aggregation.

### 4.9 Background jobs

`IBackgroundJobService` isolates enqueueing from Hangfire. Hangfire is the local
queue, execution, dashboard, and retry engine. The API is a client; the Worker
is the server.

Phase 1 manual retry is implemented. Configurable automatic retry for
classified transient failures is a P2-05 deliverable and must not be treated as
implemented until that package is complete.

## 5. Processing flows

### 5.1 Upload

```text
Frontend uploads multipart/form-data
  -> API validates request metadata
  -> UploadMeetingService validates and saves original audio
  -> repository creates MeetingJob (Queued / Uploaded)
  -> IBackgroundJobService enqueues MeetingMind job ID
  -> repository stores Hangfire job ID
  -> API returns 202 with MeetingMind job ID
  -> frontend starts polling
```

If persistence or enqueueing fails, the API must not run processing inline.

### 5.2 Worker processing

```text
Hangfire Worker loads MeetingJob
  -> Processing / Validating / 0
  -> Processing / Transcoding / 10
  -> FFmpeg conversion
  -> Processing / Transcribing / 25
  -> local Whisper.net transcription
  -> save transcript record and text file
  -> Processing / GeneratingMinutes / 60
  -> short single-pass or Phase 2 long-transcript orchestration
  -> Processing / SavingResults / 90
  -> save minutes record and Markdown file
  -> Completed / Completed / 100
```

There is no distinct transcript-cleanup stage in the implemented workflow.

### 5.3 Manual retry

```text
Failed or Cancelled MeetingJob
  -> clear error, Hangfire ID, timing, stage, and progress
  -> Queued / Uploaded / 0
  -> enqueue same MeetingMind job ID
  -> store new Hangfire job ID
```

Existing transcript or minutes records are upserted when reprocessing reaches
those stages.

### 5.4 Phase 2 automatic retry target

```text
Pipeline exception
  -> classify permanent or transient
  -> permanent: persist final Failed state; no automatic retry
  -> transient with attempts remaining: preserve safe attempt state and let
     Hangfire schedule the configured retry
  -> transient exhausted: persist final Failed state and allow safe manual retry
```

The detailed exception taxonomy and delay policy are approved during P2-05.

## 6. Job state model

Statuses:

- `Queued`
- `Processing`
- `Completed`
- `Failed`
- `Cancelled`

Stages:

- `Uploaded`
- `Validating`
- `Transcoding`
- `Transcribing`
- `GeneratingMinutes`
- `SavingResults`
- `Completed`
- `Failed`

`CreatedAt` records job creation. `StartedAt` is assigned on first transition to
Processing. `CompletedAt` is assigned for terminal status, and `UpdatedAt`
tracks persisted changes. Manual retry currently clears start/completion timing.
P2-04 defines and exposes the derived duration contract for active and terminal
jobs.

## 7. Persistence model

### MeetingJob

`Id`, `OriginalFileName`, `OriginalFilePath`, `ProcessedFilePath`, `Status`,
`Stage`, `Progress`, `ErrorMessage`, `HangfireJobId`, `CreatedAt`, `StartedAt`,
`CompletedAt`, and `UpdatedAt`.

### MeetingTranscript

One-to-one with `MeetingJob`: `Id`, `MeetingJobId`, `TranscriptText`,
`TranscriptFilePath`, and `CreatedAt`.

### MeetingMinutes

One-to-one with `MeetingJob`: `Id`, `MeetingJobId`, `Title`, `Summary`,
`DecisionsJson`, `ActionItemsJson`, `RisksJson`, `NextStepsJson`,
`FullMinutesJson`, `MinutesFilePath`, and `CreatedAt`.

Deleting a job cascades to its transcript and minutes records. Broader
repositories are added only for concrete use cases.

## 8. HTTP endpoints

| Method | Route | Responsibility |
| --- | --- | --- |
| `POST` | `/api/meetings/upload` | Validate, persist, enqueue, and return 202 |
| `GET` | `/api/meetings/{jobId}/status` | Return status, stage, progress, error, and Phase 2 duration |
| `GET` | `/api/meetings/history` | Return newest-first paginated history |
| `GET` | `/api/meetings/{jobId}/result` | Return structured minutes |
| `GET` | `/api/meetings/{jobId}/transcript/download` | Return transcript text artifact |
| `GET` | `/api/meetings/{jobId}/minutes/download` | Return Markdown minutes artifact |
| `POST` | `/api/meetings/{jobId}/retry` | Requeue failed or cancelled job |
| `GET` | `/health` | API liveness |
| `GET` | `/health/db` | Database health |
| `GET` | `/hangfire` | Unrestricted dashboard for trusted local use |

Application result contracts form the boundary below controller response
shaping. Provider types and local paths do not cross this boundary.

## 9. Error handling

- Upload validation errors return a safe 400 response.
- Unknown jobs return 404.
- Invalid manual retry state returns 409.
- Worker errors record status, stage, progress, terminal timing when applicable,
  and a bounded sanitized error.
- Logs include identifiers, stage, and exception context but not API keys, full
  transcripts, provider payloads, or physical paths.
- P2-05 separates transient exceptions that must reach Hangfire from permanent
  exceptions that are finalized immediately.

## 10. Configuration

Configuration uses standard .NET providers. Environment variables replace
section separators with double underscores. Real secrets and machine-specific
paths must not be committed.

Configuration areas:

- `ConnectionStrings:DefaultConnection`
- `Storage`
- `AudioProcessing`
- `Transcription`
- `OpenAI`
- `Processing`
- Phase 2 retry and retention settings

The API and Worker must resolve the same database and storage root. Only the
Worker requires FFmpeg, Whisper, and OpenAI generation configuration. P2-03
removes committed absolute paths and adds fail-fast validation.

## 11. Observability and operations

Current local observability consists of ASP.NET logs, Worker logs, the Hangfire
dashboard, PostgreSQL job history, and stored job error state.

P2-07 adds structured per-stage timing, safe error review, useful readiness
checks, and bounded retention. Cleanup must verify root confinement and exclude
active jobs before deleting any artifact.

## 12. Security architecture

Phase 2 is intentionally unauthenticated and trusted-local. Controls are:

- Extension, MIME, filename, and configurable-size validation.
- Safe internal filenames and path-root confinement.
- Environment variables or user secrets for sensitive configuration.
- No local paths, secrets, full transcripts, or provider payloads in API
  responses or routine logs.
- No remote exposure of the application or Hangfire dashboard.

Authentication, authorization, user ownership, encrypted cloud storage, audit
logging, and public deployment are future architecture work.

## 13. Architecture decisions

### ADR-001 — Asynchronous processing

**Decision:** The API enqueues a persistent background job and returns without
executing media or AI processing.

**Reason:** Transcription and summarization are long-running and must survive the
HTTP request lifetime.

### ADR-002 — PostgreSQL

**Decision:** Use PostgreSQL locally for MeetingMind and Hangfire persistence.

**Reason:** It is production-capable, supports the selected tooling, and has a
clear future managed-database path.

### ADR-003 — Local storage behind an interface

**Decision:** Store artifacts locally through `IFileStorageService`.

**Reason:** It keeps the trusted-local release simple while isolating a future
object-storage replacement.

### ADR-004 — External Whisper API

**Status:** Superseded.

The original draft selected the OpenAI Whisper API. Phase 1 implementation
replaced it with local Whisper.net; ADR-006 records the active decision.

### ADR-005 — Hangfire queue and workflow engine

**Decision:** Use Hangfire with PostgreSQL storage. The API is the enqueueing
client and dashboard host; the separate Worker is the Hangfire server.

**Reason:** It provides durable local queueing, inspection, and a configurable
retry mechanism without coupling controllers to execution.

### ADR-006 — Local Whisper.net transcription

**Decision:** Use Whisper.net with a local CPU runtime behind
`ITranscriptionService`.

**Reason:** It avoids per-transcription API dependency and matches the delivered
MVP. Model acquisition and local resource use are explicit operational costs.

### ADR-007 — OpenAI GPT structured minutes

**Decision:** Use OpenAI GPT behind `IMeetingMinutesService` with strict
JSON-schema output.

**Reason:** Structured output supports reliable persistence and frontend
rendering while keeping the provider replaceable.

### ADR-008 — Separate Worker entry point

**Decision:** Run job execution in `MeetingMind.Worker`, not in the API.

**Reason:** It preserves process separation and prevents API scaling or restart
behavior from silently becoming processing behavior.

### ADR-009 — Phase 2 is local hardening

**Decision:** Phase 2 covers local production-readiness, not cloud migration.

**Reason:** The delivery-cycle scope was confirmed on 2026-07-15. Earlier draft
references to “Phase 2 (Cloud)” are superseded.

## 14. Future cloud mapping

This mapping is directional and outside Phase 2:

| Local component | Possible future cloud component |
| --- | --- |
| React development/static build | S3 + CloudFront or equivalent |
| ASP.NET Core API | ECS, App Runner, container platform, or other approved host |
| PostgreSQL Docker | Managed PostgreSQL such as RDS or Azure Database |
| Local storage | S3 or Azure Blob Storage |
| Hangfire | Retain on managed PostgreSQL or replace with an approved queue/workflow service |
| FFmpeg local process | Container or media-processing workload |
| Whisper.net local CPU | Containerized model, OpenAI transcription, or managed transcription service |
| OpenAI GPT | OpenAI or an approved alternative behind the same Application boundary |
| Local logs | Centralized cloud logging and tracing |

A future cloud plan must revisit authentication, authorization, ownership,
networking, encryption, secrets, audit, cost, and data residency before any
public or shared deployment.

## 15. Architecture risks

| Risk | Current or planned mitigation |
| --- | --- |
| API becomes coupled to processing | Separate Worker and Application interfaces. |
| Long input exceeds one GPT request | P2-06 chunking and hierarchical aggregation. |
| Provider retry conflicts with job state | P2-05 classified retries and state-transition tests. |
| Machine-specific configuration | P2-03 portable defaults and validation. |
| Local storage growth | P2-07 safe retention policy. |
| Thin automated coverage | P2-02 verification foundation and P2-09 release gate. |
| Unauthenticated dashboard exposed remotely | Trusted-local boundary; no remote deployment in Phase 2. |

## 16. Governing principle

Keep business behavior stable, infrastructure replaceable, HTTP requests short,
job state persistent, and every long-running operation in the Worker.

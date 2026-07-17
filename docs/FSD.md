# Functional Specification Document

**Product:** MeetingMind AI

**Version:** 2.0

**Status:** Active Phase 2 specification

**Last reconciled with code:** 2026-07-17

## 1. Purpose and boundary

MeetingMind accepts a supported audio recording, creates a persistent job,
processes it asynchronously, and makes a transcript and structured meeting
minutes available for viewing and download.

Phase 2 remains a trusted local deployment. Authentication, user ownership,
multi-tenancy, cloud infrastructure, and public access are excluded.

## 2. Runtime components

- React, TypeScript, Material UI, Axios, and Vite frontend.
- ASP.NET Core Web API for upload, enqueueing, status, history, results,
  downloads, retry, health checks, Swagger, and the local Hangfire dashboard.
- A separate .NET Worker containing the Hangfire server and processing handler.
- PostgreSQL for MeetingMind data and Hangfire storage.
- Local filesystem storage behind `IFileStorageService`.
- FFmpeg behind `IAudioProcessingService`.
- Local Whisper.net behind `ITranscriptionService`.
- OpenAI GPT behind `IMeetingMinutesService`.

The API never executes FFmpeg, Whisper, or GPT processing. The Worker never
bypasses Application interfaces to reach infrastructure dependencies.

## 3. Functional requirements

### FR-001 — Upload meeting

The user can upload one MP3, WAV, M4A, or AAC audio file. The system validates
filename safety, extension, MIME type, non-empty content, and configurable size
before accepting it. Stored internal names are generated safely, and no local
path is returned to the user.

### FR-002 — Create and enqueue processing job

After validation, the API saves the original audio, creates a `MeetingJob`,
enqueues `ProcessMeetingAsync(jobId)` through `IBackgroundJobService`, stores
the Hangfire job ID, and returns HTTP 202 with the MeetingMind job ID, status,
and stage. It does not wait for processing.

### FR-003 — Background processing

The Worker executes this pipeline independently of the user session:

1. Load and validate the persisted job and audio reference.
2. Convert audio to PCM 16-bit, 16 kHz, mono WAV through FFmpeg.
3. Generate a transcript locally through Whisper.net.
4. Persist the transcript record and text artifact.
5. Generate structured minutes through OpenAI GPT.
6. Persist the minutes record and Markdown artifact.
7. Mark the job completed.

There is no separate transcript-cleanup stage in the current pipeline. Text
normalization needed for chunk boundaries may be introduced as part of Phase 2
long-meeting orchestration, but it must not be represented as an independent
external processing provider.

### FR-004 — Job status and duration

Supported statuses are `Queued`, `Processing`, `Completed`, `Failed`, and
`Cancelled`. Supported stages are `Uploaded`, `Validating`, `Transcoding`,
`Transcribing`, `GeneratingMinutes`, `SavingResults`, `Completed`, and `Failed`.

Each job maintains progress from 0 to 100, current stage, a sanitized error
when applicable, creation/update timestamps, and optional start/completion
timestamps. Phase 2 must expose a documented processing duration through status
and history responses and display it in the frontend. Retry must reset timing in
accordance with the approved duration semantics.

### FR-005 — Audio transcoding

FFmpeg converts accepted input to the configured standard format before
transcription. Conversion failures must identify the processing stage without
exposing unsafe paths or raw command details to the frontend.

### FR-006 — Local transcription

The Worker uses Whisper.net locally. Model size, model directory or explicit
model path, automatic model download, and language are configurable. A missing
model may be downloaded only when automatic download is enabled.

### FR-007 — Structured meeting minutes

Minutes contain:

- Title and executive summary.
- Attendees, when identifiable.
- Discussion points.
- Decisions.
- Action items with description and optional owner and due date.
- Risks or blockers.
- Next steps.

Short transcripts use one structured GPT request. Transcripts above the
configured single-pass threshold must use bounded chunks, partial structured
results, and hierarchical final aggregation. Merge behavior must preserve the
schema and avoid deterministic duplicates.

### FR-008 — View and download results

For completed jobs, users can view structured minutes and transcript content
and download a plain-text transcript and Markdown minutes artifact. Missing or
not-yet-available results return a safe not-found response.

### FR-009 — Retry failed processing

Manual retry remains available for `Failed` and `Cancelled` jobs and reuses the
same MeetingMind job ID and original upload. It resets processing state and
stores the new Hangfire job ID.

Phase 2 adds configured automatic retries for classified transient failures.
Permanent failures must not be retried automatically. When automatic attempts
are exhausted, the job must retain its final failed stage and sanitized error
and remain eligible for manual retry when the original input is still present.

### FR-010 — Processing history

The API returns persisted jobs newest first with `skip` and bounded `take`
pagination. History includes filename, status, stage, progress, sanitized error,
timestamps, and Phase 2 duration. The frontend must provide usable pagination
without breaking selected-job polling.

## 4. API contract

| Method | Endpoint | Successful response |
| --- | --- | --- |
| `POST` | `/api/meetings/upload` | `202 Accepted` with job ID, status, and stage |
| `GET` | `/api/meetings/{jobId}/status` | Current status, stage, progress, error, and Phase 2 duration |
| `GET` | `/api/meetings/history?skip=0&take=50` | Page metadata and newest-first history items |
| `GET` | `/api/meetings/{jobId}/result` | Structured minutes for a completed job |
| `GET` | `/api/meetings/{jobId}/transcript/download` | Plain-text transcript artifact |
| `GET` | `/api/meetings/{jobId}/minutes/download` | Markdown minutes artifact |
| `POST` | `/api/meetings/{jobId}/retry` | `202 Accepted` for an eligible manual retry |
| `GET` | `/health` | API liveness |
| `GET` | `/health/db` | Database check; expanded readiness is a Phase 2 deliverable |

Unknown jobs return 404. Invalid uploads return 400. A retry of a job whose
status is not retryable returns 409.

## 5. Persistence model

### MeetingJob

`Id`, `OriginalFileName`, `OriginalFilePath`, `ProcessedFilePath`, `Status`,
`Stage`, `Progress`, `ErrorMessage`, `HangfireJobId`, `CreatedAt`, `StartedAt`,
`CompletedAt`, and `UpdatedAt`.

### MeetingTranscript

One transcript per job: `Id`, `MeetingJobId`, `TranscriptText`,
`TranscriptFilePath`, and `CreatedAt`.

### MeetingMinutes

One minutes record per job: `Id`, `MeetingJobId`, `Title`, `Summary`,
`DecisionsJson`, `ActionItemsJson`, `RisksJson`, `NextStepsJson`,
`FullMinutesJson`, `MinutesFilePath`, and `CreatedAt`.

Stored file references are relative to the configured storage root. Physical
paths are infrastructure details and must not enter frontend contracts.

## 6. Error and retry behavior

The system distinguishes:

- Validation or permanent failures, such as unsupported input, corrupted audio,
  missing stored input, or invalid configuration.
- Transient failures, such as eligible timeouts, rate limiting, temporary
  network interruption, or transient persistence/provider faults.

Every final failure records job status, stage, progress, a bounded sanitized
error, and logs containing the job ID and exception context. Routine logs must
not include full transcripts, secrets, provider payloads, or local paths.

P2-05 defines the concrete exception classification, attempt count, and delay
policy before retry implementation begins.

## 7. Non-functional requirements

### Performance

- The upload request performs validation, storage, persistence, and enqueueing
  only; no long-running media or AI processing occurs in the request.
- The frontend polls active jobs without creating overlapping polling loops.
- Chunk and aggregation sizes are configurable and bounded.

### Reliability

- A successful upload is not lost when the browser closes.
- Status transitions and persisted artifacts remain internally consistent.
- Automatic retry is bounded and limited to transient failures.
- Cleanup never removes active-job data.

### Security and privacy

- Validate extension, MIME type, size, and filename safety.
- Prevent path traversal and storage-root escape.
- Keep secrets outside committed configuration.
- Do not expose local paths or sensitive diagnostic details.
- Keep the application and Hangfire dashboard on a trusted local machine.

### Maintainability

- Domain has no infrastructure dependencies.
- Application defines use cases and infrastructure interfaces.
- Infrastructure implements persistence, storage, media, transcription, and
  GPT integrations.
- API and Worker are separate entry points and remain thin composition and
  delivery layers.

## 8. Verification requirements

- Unit tests cover domain and Application behavior.
- API contract tests cover successful and invalid request paths.
- PostgreSQL integration tests verify persistence and retry state transitions.
- Worker tests use test doubles for FFmpeg, Whisper, OpenAI, and storage.
- Frontend tests cover upload, polling, history, result, duration, and retry
  states.
- Real provider and media dependencies are used only in the documented local
  end-to-end acceptance run.

Measurable acceptance criteria for each Phase 2 work package are maintained in
[`../BACKLOG.md`](../BACKLOG.md).

## 9. Future enhancements

Authentication, user ownership, multi-tenancy, cloud hosting, cloud storage and
orchestration, speaker diarization, meeting integrations, notifications,
multiple providers, and meeting-intelligence features are deferred to future
delivery cycles.

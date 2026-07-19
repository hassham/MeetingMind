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

There is no separate transcript-cleanup stage. Application-layer long-meeting
orchestration normalizes line endings only as part of bounded chunking; it is
not an independent external processing provider.

### FR-004 — Job status and duration

Supported statuses are `Queued`, `Processing`, `Completed`, `Failed`, and
`Cancelled`. Supported stages are `Uploaded`, `Validating`, `Transcoding`,
`Transcribing`, `GeneratingMinutes`, `SavingResults`, `Completed`, and `Failed`.

Each job maintains progress from 0 to 100, current stage, a sanitized error
when applicable, creation/update timestamps, and optional start/completion
timestamps. Status and history expose `processingDurationSeconds` and
`totalDurationSeconds` as non-negative whole numbers.

- Processing duration measures Worker execution. It is zero before the first
  attempt starts, live from `StartedAt` while processing, and final at
  `CompletedAt` for terminal jobs. A missing terminal completion falls back to
  `UpdatedAt`; a missing start produces zero.
- Total duration measures elapsed time from the original `CreatedAt`, including
  queue time and manual retries. It is live for queued/processing jobs and final
  for terminal jobs, using the same completion fallback.
- Manual retry retains the previous attempt's final processing duration while
  queued. The first transition into processing resets `StartedAt`, clears the
  previous completion, and starts the new attempt at zero. Total duration never
  resets because it remains anchored to the original upload.
- Automatic retries preserve `StartedAt`, so processing duration remains live
  through configured backoff and subsequent automatic attempts.

The frontend displays both values in history and processing details. The
selected active job advances locally once per second between authoritative API
polls.

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
120,000-character single-pass threshold use sequential chunks of at most 60,000
characters with up to 1,500 characters of overlap. Boundaries prefer paragraphs,
sentences, whitespace, and a final hard bound. Transcripts above the configured
1,200,000-character maximum fail permanently without truncation.

Every chunk returns the complete minutes schema. Application orchestration
normalizes and deterministically deduplicates strings case-insensitively while
preserving order. Action items with matching normalized descriptions fill
missing owner/due-date values; conflicting non-empty metadata remains separate.
Bounded groups pass through sequential OpenAI aggregation tiers until one final
result remains, with each serialized aggregation input limited to 120,000
characters.

Partial generation reports `GeneratingMinutes` progress from 60–85% and
aggregation reports 86–89%. Transient partial or aggregation failures reach the
configured Hangfire retry policy. A later attempt reuses the transcript but
regenerates partials because partial minutes are not persisted.

### FR-008 — View and download results

For completed jobs, users can view structured minutes and transcript content
and download a plain-text transcript and Markdown minutes artifact. Missing or
not-yet-available results return a safe not-found response.

### FR-009 — Retry failed processing

Manual retry remains available for `Failed` and `Cancelled` jobs and reuses the
same MeetingMind job ID and original upload. It resets processing state and
stores the new Hangfire job ID.

The Worker applies two configured automatic retries after the initial execution,
using default delays of 10 and 60 seconds. Permanent failures are excluded from
automatic retry. Unknown failures use the same bounded retry policy. A waiting
retry is `Queued` at its last stage and progress with its safe error and retry
metadata retained. Exhausted failures retain their final stage and sanitized
error and remain eligible for manual retry when the original input is present.
Manual retry resets the retry count and grants a fresh configured budget.

### FR-010 — Processing history

The API returns persisted jobs newest first with `skip` and bounded `take`
pagination. History includes filename, status, stage, progress, sanitized error,
retry metadata, timestamps, and Phase 2 duration. The frontend shows 20 jobs per
page with boundary-aware Previous/Next controls. A selected active job remains
visible and continues one non-overlapping polling loop when its history card is
on another page.

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
| `GET` | `/health/db` | PostgreSQL compatibility check |
| `GET` | `/health/ready` | Aggregate PostgreSQL, storage-write, FFmpeg, and Whisper-model readiness |

Unknown jobs return 404. Invalid uploads return 400. A retry of a job whose
status is not retryable returns 409.

## 5. Persistence model

### MeetingJob

`Id`, `OriginalFileName`, `OriginalFilePath`, `ProcessedFilePath`, `Status`,
`Stage`, `Progress`, `ErrorCode`, `ErrorMessage`, `HangfireJobId`, `AutomaticRetryCount`,
`AutomaticRetryLimit`, `NextRetryAt`, `CreatedAt`, `StartedAt`, `CompletedAt`,
and `UpdatedAt`.

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

Every final failure records job status, stage, progress, a stable error code,
and a bounded safe message. Structured logs record job ID, stage, outcome,
attempt, progress, failure classification, exception type, and elapsed time.
They do not record raw exception messages/objects, full transcripts, secrets,
provider payloads, or physical paths. Exceptions returned to Hangfire are also
replaced with safe code/message exceptions.

Temporary storage I/O, FFmpeg/Whisper interruptions, retryable OpenAI responses,
network timeouts, and transient persistence faults are eligible for automatic
retry. Missing or corrupt inputs, unsafe paths, access/capacity failures,
invalid configuration, unsupported media, provider authentication/validation
failures, malformed AI output, and non-transient persistence failures are
permanent. Unclassified exceptions are treated as transient but remain bounded
by the configured two-retry limit.

Before repeating work, the Worker validates and reuses the latest durable
checkpoint: a stored transcript first, otherwise processed audio. Invalid or
missing checkpoint artifacts fall back to the nearest safe earlier stage.

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
- Retention is disabled by default and configurable by enabled state, terminal
  age, daily Hangfire schedule, and bounded batch size. Only terminal jobs with
  no scheduled retry qualify. A PostgreSQL row lock protects the eligibility
  check through artifact and cascading record deletion. All paths are
  prevalidated within the storage root and reparse-point traversal is rejected.
- Readiness returns only dependency names and healthy/unhealthy states. It
  queries PostgreSQL, performs an in-root zero-byte write/delete probe, verifies
  FFmpeg, and opens the configured Whisper model for reading.

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
- Frontend tests cover upload, non-overlapping polling, paginated history,
  off-page selection, results, duration, retry state, focus, and responsive
  selection behavior. Axe scans fail on serious or critical violations in the
  primary empty, active, completed, failed, and paginated states; color contrast
  remains a manual browser check because jsdom has no layout model.
- Real provider and media dependencies are used only in the documented local
  end-to-end acceptance run.

Measurable acceptance criteria for each Phase 2 work package are maintained in
[`../BACKLOG.md`](../BACKLOG.md).

## 9. Future enhancements

Authentication, user ownership, multi-tenancy, cloud hosting, cloud storage and
orchestration, speaker diarization, meeting integrations, notifications,
multiple providers, and meeting-intelligence features are deferred to future
delivery cycles.

# Product Requirements Document

**Product:** MeetingMind AI

**Version:** 2.0

**Status:** Approved for Phase 2

**Product owner:** Hasham Ahmad

**Last reconciled with code:** 2026-07-17

## 1. Vision

MeetingMind AI converts uploaded meeting recordings into transcripts and
structured, actionable meeting minutes. It helps users retain decisions,
action items, risks, and next steps without preparing notes manually.

The long-term vision is a searchable meeting-intelligence platform. The current
product boundary is a trusted local application.

## 2. Current product state

Phase 1 delivered the local MVP:

- MP3, WAV, M4A, and AAC upload with extension, MIME, filename, and size
  validation.
- Immediate job creation and Hangfire enqueueing by the API.
- A separate Worker process for the long-running pipeline.
- FFmpeg conversion to PCM 16-bit, 16 kHz, mono WAV.
- Local Whisper.net transcription.
- OpenAI GPT structured meeting-minutes generation.
- Persisted status, progress, history, errors, transcripts, and minutes.
- Result viewing, downloads, and manual retry for failed or cancelled jobs.
- A React and Material UI frontend.

Phase 2 is a local production-readiness and hardening cycle. It is not the cloud
migration cycle.

## 3. Goals

### User goals

- Upload a supported meeting recording.
- Receive a job ID without waiting for processing.
- Understand the current status, stage, progress, duration, and failure state.
- Receive useful minutes for both short and long meetings.
- Recover from transient failures without uploading the recording again.
- View and download current and historical results.

### Technical goals

- Keep all long-running work outside the HTTP request.
- Make transient failures recover automatically through configurable policy.
- Make local setup portable across developer machines.
- Improve verification across API, persistence, Worker, and frontend boundaries.
- Keep Domain and Application logic independent of provider implementations.
- Retain a future cloud migration path without deploying to cloud in Phase 2.

## 4. Target users

The trusted-local release is intended for project managers, scrum masters,
engineering teams, business analysts, product owners, consultants, and small
businesses evaluating or using the application on a controlled machine.

Enterprise, regulated, shared, and public deployments require later security
and tenancy work.

## 5. Phase 2 scope

Phase 2 includes:

- Requirements and architecture reconciliation.
- Automated API, persistence, Worker-pipeline, and frontend verification.
- Portable configuration, startup validation, and safe local secret handling.
- Explicit processing-duration reporting.
- Classified and configurable automatic retry for transient failures while
  retaining manual retry.
- Transcript chunking, partial structured summaries, and hierarchical final
  aggregation for long meetings.
- Structured stage logging, readiness checks, safe errors, and configurable
  storage retention.
- Frontend duration, retry state, history pagination, error messaging, and
  accessibility improvements.
- A documented Phase 2 end-to-end acceptance run.

## 6. Out of scope

The following are not part of Phase 2:

- Authentication, authorization, user ownership, and multi-tenancy.
- Public or shared-network deployment.
- AWS, Azure, or other cloud deployment.
- S3/Blob storage, RDS, Step Functions, SQS, Lambda, or ECS implementations.
- Video processing, live recording, and live transcription.
- Speaker diarization or recognition.
- Calendar, Teams, Zoom, and Google Meet integrations.
- Email notifications, billing, mobile applications, and translation.
- Cross-meeting semantic search or organizational knowledge graphs.

All endpoints and the Hangfire dashboard remain unrestricted by design and must
only be exposed in a trusted local environment.

## 7. User stories

### Upload a meeting

As a user, I want to upload a supported meeting recording so that processing
can begin without blocking my browser.

### Track processing

As a user, I want to see status, stage, progress, duration, and useful failure
information so that I understand what the system is doing.

### Process a long meeting

As a user, I want a long transcript to be summarized in bounded sections and
aggregated into complete minutes instead of failing at the single-request
limit.

### Recover from a transient failure

As a user, I want temporary provider or network failures to retry automatically
and exhausted or permanent failures to remain manually retryable when safe.

### Review history and results

As a user, I want to page through prior jobs, view their transcript and minutes,
and download both artifacts.

## 8. Product requirements

- The API must return an accepted upload response after validation, storage,
  persistence, and enqueueing; it must not perform the processing pipeline.
- The Worker must execute the pipeline independently of the browser and API
  request lifetime.
- Transcription must run locally through Whisper.net behind
  `ITranscriptionService`.
- Minutes generation must use OpenAI GPT behind `IMeetingMinutesService`.
- Output must include title, summary, attendees, discussion points, decisions,
  action items, risks, and next steps.
- Local paths, API keys, full transcripts, and sensitive AI payloads must not be
  exposed in API responses or routine logs.
- Automatic retry must be limited to classified transient failures and governed
  by configuration.
- Manual retry must remain available for failed or cancelled jobs without a new
  upload.
- Long-meeting processing must preserve structured information across chunks
  and apply deterministic deduplication during aggregation.
- Cleanup must never remove active-job artifacts or files outside the configured
  storage root.

## 9. Phase 2 success measures

Phase 2 succeeds when:

- The backend builds and all backend unit and integration tests pass.
- Frontend lint, build, and automated tests pass.
- Contract tests cover upload, status, history, result, download, and retry
  behavior.
- Worker tests cover normal completion, transient recovery, exhausted retry,
  permanent failure, and manual retry.
- Tests demonstrate both single-pass short transcripts and multi-pass long
  transcripts producing the complete minutes schema.
- Status and history expose duration using documented semantics, and the
  frontend displays it.
- A clean local setup can be configured without editing committed source files
  or committing secrets or machine-specific absolute paths.
- The documented end-to-end run verifies short audio, long audio, retry paths,
  history, viewing, and downloads.
- Durable documentation matches the released behavior.

The detailed work-package acceptance criteria live in
[`../BACKLOG.md`](../BACKLOG.md).

## 10. Risks and mitigations

| Risk | Mitigation |
| --- | --- |
| Poor audio quality | Standardize audio through FFmpeg and document model limitations. |
| Long transcript exceeds a model request | Use bounded chunks and hierarchical aggregation. |
| Duplicate or conflicting chunk output | Use structured schemas, deterministic merge rules, and final aggregation. |
| OpenAI or network interruption | Classify transient failures and apply configured retry/backoff. |
| Automatic retry hides permanent errors | Do not automatically retry permanent failures; expose an actionable final state. |
| Growing local storage | Apply configurable retention with active-job and root-boundary safeguards. |
| Sensitive content reaches logs | Log identifiers and stages, not transcripts, secrets, paths, or full provider payloads. |
| Local-only endpoints are exposed remotely | Document and retain the trusted-local boundary until authentication is delivered. |

## 11. Roadmap terminology

- **Phase 1:** completed local MVP delivery cycle.
- **Phase 2:** active local production-readiness and hardening delivery cycle.
- **Future delivery cycles:** authentication, multi-user functionality, cloud
  deployment, integrations, and meeting-intelligence expansion.

Earlier drafts used “Phase 2” to mean AWS migration. That terminology is
superseded by the Phase 2 planning decision dated 2026-07-15. The cloud mapping
remains an architectural option, not an active commitment.

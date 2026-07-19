# BACKLOG.md - MeetingMind AI

This file contains active work only. The completed Phase 1 MVP delivery cycle
is preserved under [`docs/archive/phase1`](docs/archive/phase1/README.md).
Phase 2 decisions are preserved under
[`docs/archive/phase2`](docs/archive/phase2/README.md).

Status legend: `TODO` | `IN PROGRESS` | `BLOCKED` | `PARTIAL` | `DONE`

Last verified against code: 2026-07-19.

## Current delivery cycle: Phase 2

**Theme:** Local production-readiness and hardening.

Everything accepted below belongs to Phase 2. Work will be completed
sequentially, with verification after each work package. No Phase 3 or cloud
migration work will be implemented between Phase 2 work packages.

### Phase 2 boundaries

In scope:

- Complete all agreed local hardening work as one Phase 2 release.
- Configurable automatic retries for transient failures, plus manual retry.
- Long-transcript chunking and hierarchical meeting-minutes generation.
- Portable configuration and safe local secret handling.
- Processing duration, operational hardening, and frontend completeness.
- Backend, frontend, integration, and documented end-to-end verification.

Out of scope:

- AWS, Azure, or other cloud deployment and infrastructure replacement.
- Authentication, authorization, multi-user ownership, and public access.
- Speaker diarization, live recording, meeting-platform integrations, and
  unrelated new end-user features.
- Broad repository abstractions without a concrete Phase 2 use case.

### Phase 2 completion gate

Phase 2 is complete only when:

- The backend solution builds and all backend tests pass.
- Frontend lint, build, and automated tests pass.
- API, persistence, and worker-pipeline integration tests pass.
- A documented end-to-end run processes representative short and long audio,
  exercises a transient retry, and verifies history, results, and downloads.
- Durable requirements and operating documentation match the implementation.
- No Phase 2 item below remains `TODO`, `PARTIAL`, `IN PROGRESS`, or `BLOCKED`.

### Work-package acceptance criteria

- **P2-01:** Durable documents agree that Phase 2 is trusted-local hardening,
  transcription uses local Whisper.net, the Worker executes Hangfire jobs, no
  separate transcript-cleanup stage exists, and automatic retry remains pending
  until P2-05. Searches find no stale claim that MediatR/CQRS or OpenAI Whisper
  is the implemented architecture.
- **P2-02:** Standard repository commands execute unit, API contract,
  PostgreSQL persistence, Worker-pipeline, and frontend tests. Tests do not call
  real FFmpeg, Whisper, OpenAI, or production storage, and each critical
  workflow has at least one success and one failure assertion.
- **P2-03:** A fresh local checkout can configure API and Worker without editing
  committed files; no committed setting contains a developer-specific absolute
  path or real secret; missing or invalid required settings fail startup with a
  message naming the setting; API and Worker storage roots are proven equal in
  the documented setup.
- **P2-04:** Status and history return the same documented duration unit and
  semantics for queued, active, terminal, and manually retried jobs; live and
  final duration render in the frontend; automated tests cover every status and
  retry reset.
- **P2-05:** Configured transient failures demonstrate retry and eventual
  success; permanent failures run once; exhausted transient failures end in a
  stable failed state; manual retry still succeeds where eligible; attempt and
  delay settings contain no hard-coded policy in the handler.
- **P2-06:** Inputs at and around every chunk boundary pass automated tests; a
  transcript above the single-pass threshold completes through multiple partial
  calls and one final aggregation; a short transcript remains single-pass; all
  eight minutes sections survive aggregation without deterministic duplicates;
  partial-call failure and retry behavior is tested.
- **P2-07:** Logs identify job, stage, outcome, and elapsed time without secrets,
  transcripts, provider payloads, or physical paths; readiness detects each
  locally verifiable dependency failure; retention tests prove active artifacts
  and files outside the storage root cannot be deleted.
- **P2-08:** Frontend tests cover duration, automatic/manual retry state, page
  navigation, selection, and active polling; supported error states have
  actionable messages; keyboard-only operation and automated accessibility
  checks find no critical violations in the primary workflow.
- **P2-09:** Every command in the completion gate passes, and the recorded local
  end-to-end evidence covers short and long meetings, transient recovery,
  permanent failure, manual retry, history, results, and both downloads.

## Current focus

P2-08 is next. It requires task-specific discovery before frontend experience
changes begin.

## Ordered Phase 2 work packages

### P2-01 - Reconcile requirements and architecture

- [x] Update `AGENTS.md`, `WORKFLOW.md`, `docs/PRD.md`, `docs/FSD.md`, and
      `docs/SAD.md` to
      describe local Whisper.net, the separate Worker entry point, and the
      implemented Clean Architecture dependency direction.
      Status: DONE. Completed: 2026-07-17.
- [x] Resolve contradictions around transcript cleanup, automatic retries,
      duration, local-only security, and the meaning of cloud Phase 2 in the
      older specifications.
      Status: DONE. Completed: 2026-07-17.
- [x] Record measurable acceptance criteria for every remaining Phase 2 work
      package before its implementation begins.
      Status: DONE. Completed: 2026-07-17.

### P2-02 - Establish the automated verification foundation

- [x] Add focused test projects and fixtures for API contracts, PostgreSQL
      persistence, and the Worker processing workflow.
      Status: DONE. Completed: 2026-07-17.
- [x] Add frontend test tooling and tests for the critical upload, polling,
      result, history, and retry states.
      Status: DONE. Completed: 2026-07-17.
- [x] Keep external AI, FFmpeg, and filesystem dependencies behind test doubles
      in automated tests; reserve real integrations for documented end-to-end
      acceptance testing.
      Status: DONE. Completed: 2026-07-17.

### P2-03 - Make configuration portable and fail fast

- [x] Remove committed machine-specific storage and FFmpeg paths while keeping
      local defaults easy to use.
      Status: DONE. Completed: 2026-07-17.
- [x] Add startup validation for storage, upload, audio, transcription, OpenAI,
      database, processing, and retry settings where each setting is required.
      Status: DONE. Completed: 2026-07-17.
      Notes: Obsolete stub-processing configuration was removed. Automatic
      retry configuration remains intentionally pending until P2-05.
- [x] Ensure the API and Worker resolve the same storage root and document
      environment-variable and .NET user-secret setup.
      Status: DONE. Completed: 2026-07-17.
- [x] Remove or explicitly isolate the API stub-processing delay from normal
      operation.
      Status: DONE. Completed: 2026-07-17.

### P2-04 - Complete processing-duration tracking

- [x] Define duration semantics for queued, processing, completed, failed,
      cancelled, and manually retried jobs.
      Status: DONE. Completed: 2026-07-18.
- [x] Expose duration consistently through status and history application/API
      contracts without exposing infrastructure details.
      Status: DONE. Completed: 2026-07-18.
- [x] Display live or final duration in the frontend processing details.
      Status: DONE. Completed: 2026-07-18.
- [x] Add unit, contract, and frontend tests for duration calculation and
      formatting.
      Status: DONE. Completed: 2026-07-18.

### P2-05 - Implement reliable automatic retries

- [x] Define transient and permanent failure categories for storage, FFmpeg,
      Whisper, OpenAI, and persistence operations.
      Status: DONE. Completed: 2026-07-18.
- [x] Add configurable Hangfire retry attempts and delays without removing the
      existing manual retry workflow.
      Status: DONE. Completed: 2026-07-18.
- [x] Ensure exceptions reach Hangfire when retry is appropriate while job
      status, stage, progress, timestamps, and sanitized errors remain correct.
      Status: DONE. Completed: 2026-07-18.
- [x] Prevent permanent failures from being retried automatically and make
      exhausted-retry state clear to users and operators.
      Status: DONE. Completed: 2026-07-18.
- [x] Add tests for transient success-after-retry, permanent failure, exhausted
      retries, and later manual retry.
      Status: DONE. Completed: 2026-07-18.

### P2-06 - Support long meetings

- [x] Define configurable chunk size, overlap/boundary behaviour, maximum
      supported input, and final aggregation limits.
      Status: DONE. Completed: 2026-07-18.
- [x] Split long transcripts on safe textual boundaries and generate partial
      structured summaries through Application-layer orchestration.
      Status: DONE. Completed: 2026-07-18.
- [x] Aggregate partial results into final minutes while deduplicating and
      preserving attendees, discussion points, decisions, action items, risks,
      and next steps.
      Status: DONE. Completed: 2026-07-18.
- [x] Define progress reporting and retry/failure behaviour for multi-call
      minutes generation.
      Status: DONE. Completed: 2026-07-18.
- [x] Add boundary, aggregation, deduplication, failure, and regression tests
      for both short and long transcripts.
      Status: DONE. Completed: 2026-07-18.

### P2-07 - Operational hardening

- [x] Add structured per-stage logs and timing without logging full transcripts,
      local paths, secrets, or sensitive AI payloads.
      Status: DONE. Completed: 2026-07-19.
- [x] Add useful health/readiness checks for the database and locally verifiable
      dependencies such as storage, FFmpeg, and Whisper model configuration.
      Status: DONE. Completed: 2026-07-19.
- [x] Define and implement a configurable storage-retention and cleanup policy
      that cannot delete active jobs or escape the configured storage root.
      Status: DONE. Completed: 2026-07-19.
- [x] Review user-facing versus technical errors and keep exposed messages safe
      and actionable.
      Status: DONE. Completed: 2026-07-19.

### P2-08 - Complete the local frontend experience

- [ ] Display processing duration and automatic/manual retry state clearly.
      Status: TODO.
- [ ] Add usable pagination for processing history while preserving selection
      and polling behaviour.
      Status: TODO.
- [ ] Improve messages for unsupported/oversized input, long-meeting processing,
      unavailable results, and exhausted retries.
      Status: TODO.
- [ ] Verify responsive layout, keyboard operation, labels, focus behaviour,
      and accessible status communication.
      Status: TODO.

### P2-09 - Phase 2 release verification and documentation

- [ ] Run the complete backend and frontend automated verification suites.
      Status: TODO.
- [ ] Execute and record the agreed end-to-end scenarios using representative
      short audio, long audio, transient failure/recovery, permanent failure,
      manual retry, history, results, and downloads.
      Status: TODO.
- [ ] Refresh `README.md` and durable documentation with the final portable
      setup, troubleshooting, configuration, and verification instructions.
      Status: TODO.
- [ ] Archive Phase 2 implementation decisions and the completed backlog without
      starting Phase 3 work.
      Status: TODO.

## Recently completed

- [x] Phase 1 MVP delivery cycle.
      Status: DONE.
      Completed: 2026-07-13.
      Notes: Upload, asynchronous Hangfire processing, FFmpeg conversion, local
      Whisper transcription, GPT minutes generation, history, retry, downloads,
      and the React frontend were implemented and verified. See the
      [Phase 1 archive](docs/archive/phase1/README.md) for the complete record.

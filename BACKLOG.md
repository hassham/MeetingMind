# BACKLOG.md - MeetingMind AI

This file contains active work only. The completed Phase 1 MVP delivery cycle
is preserved under [`docs/archive/phase1`](docs/archive/phase1/README.md).
Phase 2 decisions are preserved under
[`docs/archive/phase2`](docs/archive/phase2/README.md).

Status legend: `TODO` | `IN PROGRESS` | `BLOCKED` | `PARTIAL` | `DONE`

Last verified against code: 2026-07-15.

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

## Ordered Phase 2 work packages

### P2-01 - Reconcile requirements and architecture

- [ ] Update `AGENTS.md`, `docs/PRD.md`, `docs/FSD.md`, and `docs/SAD.md` to
      describe local Whisper.net, the separate Worker entry point, and the
      implemented Clean Architecture dependency direction.
      Status: TODO.
- [ ] Resolve contradictions around transcript cleanup, automatic retries,
      duration, local-only security, and the meaning of cloud Phase 2 in the
      older specifications.
      Status: TODO.
- [ ] Record measurable acceptance criteria for every remaining Phase 2 work
      package before its implementation begins.
      Status: TODO.

### P2-02 - Establish the automated verification foundation

- [ ] Add focused test projects and fixtures for API contracts, PostgreSQL
      persistence, and the Worker processing workflow.
      Status: TODO.
- [ ] Add frontend test tooling and tests for the critical upload, polling,
      result, history, and retry states.
      Status: TODO.
- [ ] Keep external AI, FFmpeg, and filesystem dependencies behind test doubles
      in automated tests; reserve real integrations for documented end-to-end
      acceptance testing.
      Status: TODO.

### P2-03 - Make configuration portable and fail fast

- [ ] Remove committed machine-specific storage and FFmpeg paths while keeping
      local defaults easy to use.
      Status: TODO.
- [ ] Add startup validation for storage, upload, audio, transcription, OpenAI,
      database, processing, and retry settings where each setting is required.
      Status: TODO.
- [ ] Ensure the API and Worker resolve the same storage root and document
      environment-variable and .NET user-secret setup.
      Status: TODO.
- [ ] Remove or explicitly isolate the API stub-processing delay from normal
      operation.
      Status: TODO.

### P2-04 - Complete processing-duration tracking

- [ ] Define duration semantics for queued, processing, completed, failed,
      cancelled, and manually retried jobs.
      Status: TODO.
- [ ] Expose duration consistently through status and history application/API
      contracts without exposing infrastructure details.
      Status: TODO.
- [ ] Display live or final duration in the frontend processing details.
      Status: TODO.
- [ ] Add unit, contract, and frontend tests for duration calculation and
      formatting.
      Status: TODO.

### P2-05 - Implement reliable automatic retries

- [ ] Define transient and permanent failure categories for storage, FFmpeg,
      Whisper, OpenAI, and persistence operations.
      Status: TODO.
- [ ] Add configurable Hangfire retry attempts and delays without removing the
      existing manual retry workflow.
      Status: TODO.
- [ ] Ensure exceptions reach Hangfire when retry is appropriate while job
      status, stage, progress, timestamps, and sanitized errors remain correct.
      Status: TODO.
- [ ] Prevent permanent failures from being retried automatically and make
      exhausted-retry state clear to users and operators.
      Status: TODO.
- [ ] Add tests for transient success-after-retry, permanent failure, exhausted
      retries, and later manual retry.
      Status: TODO.

### P2-06 - Support long meetings

- [ ] Define configurable chunk size, overlap/boundary behaviour, maximum
      supported input, and final aggregation limits.
      Status: TODO.
- [ ] Split long transcripts on safe textual boundaries and generate partial
      structured summaries through Application-layer orchestration.
      Status: TODO.
- [ ] Aggregate partial results into final minutes while deduplicating and
      preserving attendees, discussion points, decisions, action items, risks,
      and next steps.
      Status: TODO.
- [ ] Define progress reporting and retry/failure behaviour for multi-call
      minutes generation.
      Status: TODO.
- [ ] Add boundary, aggregation, deduplication, failure, and regression tests
      for both short and long transcripts.
      Status: TODO.

### P2-07 - Operational hardening

- [ ] Add structured per-stage logs and timing without logging full transcripts,
      local paths, secrets, or sensitive AI payloads.
      Status: TODO.
- [ ] Add useful health/readiness checks for the database and locally verifiable
      dependencies such as storage, FFmpeg, and Whisper model configuration.
      Status: TODO.
- [ ] Define and implement a configurable storage-retention and cleanup policy
      that cannot delete active jobs or escape the configured storage root.
      Status: TODO.
- [ ] Review user-facing versus technical errors and keep exposed messages safe
      and actionable.
      Status: TODO.

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

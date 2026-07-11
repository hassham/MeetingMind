# BACKLOG.md - MeetingMind AI

Living task list. Update the status of an item as soon as it is done. This is
how Codex or any agent picks up where the last session left off without
re-explaining state. Keep completed items here briefly, then move them to the
"Done (archive)" section at the bottom so the active list stays short.

Status legend: `TODO` | `IN PROGRESS` | `BLOCKED` | `PARTIAL` | `DONE`

Last verified against code: 2026-07-11.

## Current focus

- [x] Phase 3 - Local storage and upload job creation.
      Status: DONE.
      Notes: Added storage options, local file storage, upload use case, and
      `POST /api/meetings/upload`. Upload saves the original file, creates a
      queued `MeetingJob`, and returns `jobId` immediately. No Hangfire enqueue
      yet.
- [x] Refactor API endpoints from Minimal APIs into controllers.
      Status: DONE.
      Notes: Move `/health`, `/health/db`, and `/api/meetings/upload` out of
      `Program.cs` into controller classes under `MeetingMind.Api/Controllers`.
      Keep `Program.cs` for service registration, middleware, Swagger, HTTPS,
      and `MapControllers()` only. Do this before adding more meeting endpoints.
- [x] Phase 4 - Hangfire background job integration.
      Status: DONE.
      Notes: Added Hangfire PostgreSQL storage, API dashboard at `/hangfire`,
      Worker Hangfire server, `IBackgroundJobService`, upload enqueueing, and a
      stub `MeetingProcessingJob` that marks jobs Processing then Failed with
      "Processing not yet implemented".
- [x] Phase 5 - Background job shell and status tracking.
      Status: DONE.
      Notes: Added `GET /api/meetings/{jobId}/status`, status query service,
      repository read support, configurable Worker stub delay, and focused
      status mapping tests.
- [ ] Phase 6 - FFmpeg audio processing.
      Status: TODO.
      Notes: Next active focus. Add `IAudioProcessingService` and FFmpeg-backed
      validation/transcoding, without Whisper or GPT work.

## Verified current scaffold

- [x] Solution file exists: `MeetingMind.sln`.
      Status: DONE.
- [x] Backend projects exist: `MeetingMind.Api`, `MeetingMind.Application`,
      `MeetingMind.Domain`, `MeetingMind.Infrastructure`, `MeetingMind.Shared`,
      `MeetingMind.Worker`.
      Status: DONE.
      Notes: `MeetingMind.Worker` exists in the skeleton even though SAD's
      recommended structure names `MeetingMind.Web` instead.
- [x] Docker PostgreSQL service exists in `docker-compose.yml`.
      Status: DONE.
- [x] API is scaffolded with Swagger/OpenAPI.
      Status: DONE.
- [x] API has basic health endpoints: `/health` and `/health/db`.
      Status: DONE.
- [x] API still has default `/weatherforecast` endpoint.
      Status: DONE cleanup.
      Notes: Removed the default template endpoint and `WeatherForecast` record.
- [x] Domain/Application/Infrastructure/Shared still contain template
      `Class1.cs` placeholder files.
      Status: DONE cleanup.
      Notes: Removed placeholders after adding real domain entities/enums.
- [x] `MeetingMindDbContext` exists and is registered in `Program.cs`.
      Status: DONE.
      Notes: DbContext now exposes `MeetingJobs`, `MeetingTranscripts`, and
      `MeetingMinutes` DbSets with initial EF model configuration.
- [x] EF provider dependency for API compile-time `UseNpgsql` is present.
      Status: DONE.
- [x] EF package versions should be reviewed/aligned before migrations.
      Status: DONE.
      Notes: Infrastructure `Microsoft.EntityFrameworkCore` is aligned to
      9.0.4 to match the API's `Npgsql.EntityFrameworkCore.PostgreSQL` 9.0.4.
- [x] React frontend skeleton exists under `frontend/meetingmind-ui`.
      Status: DONE.
      Notes: Replaced the default Vite starter screen with a MeetingMind UI
      shell.
- [ ] Frontend dependencies are incomplete for the documented stack.
      Status: PARTIAL.
      Notes: `package.json` now lists Material UI, Emotion, MUI icons, and
      Axios. `npm` is not available on PATH in the current shell, so
      `package-lock.json` and `node_modules` still need `npm install`.
- [x] Tests are not scaffolded.
      Status: DONE.
      Notes: Added `tests/MeetingMind.Domain.Tests` and included it in
      `MeetingMind.sln`.

## Core pipeline (FR-001 to FR-010, see docs/FSD.md)

- [x] FR-001 Upload meeting: MP3/WAV/M4A/AAC, configurable max size.
      Status: DONE for backend API.
      Notes: `POST /api/meetings/upload` validates extension, MIME type, file
      size, and file name before saving the original file.
- [x] FR-002 Create processing job: save file, DB record, job ID, enqueue,
      return immediately.
      Status: DONE.
      Notes: Upload saves the file, creates a queued DB record, enqueues a
      Hangfire job, stores `HangfireJobId`, and returns `jobId` immediately.
- [ ] FR-003 Background processing pipeline: validate -> transcode ->
      transcript -> clean -> minutes -> save -> complete.
      Status: PARTIAL.
      Notes: Hangfire now executes a stub job and updates status. Actual
      validation, FFmpeg, Whisper, transcript cleanup, GPT, and save-result
      stages remain for later phases.
- [ ] FR-004 Job status tracking: status, progress %, stage, error message,
      duration.
      Status: PARTIAL.
      Notes: `GET /api/meetings/{jobId}/status` exposes job ID, status, stage,
      progress, and error message. Explicit duration is not exposed yet.
- [ ] FR-005 Audio transcoding via FFmpeg.
      Status: TODO.
- [ ] FR-006 Transcription via Whisper API.
      Status: TODO.
- [ ] FR-007 Meeting minutes generation via GPT: title, summary, attendees,
      discussion, decisions, action items, risks, next steps.
      Status: TODO.
- [ ] FR-008 View/download transcript and minutes.
      Status: TODO.
- [ ] FR-009 Retry failed jobs: no auth check, any user.
      Status: TODO.
- [ ] FR-010 Processing history.
      Status: TODO.

## Backend infrastructure setup

- [x] Solution structure: `Api`, `Application`, `Domain`, `Infrastructure`.
      Status: DONE.
      Notes: `Shared` and `Worker` are also present.
- [x] PostgreSQL via Docker.
      Status: DONE.
- [x] Domain entities and enums: `MeetingJob`, `MeetingTranscript`,
      `MeetingMinutes`, `MeetingJobStatus`, `MeetingJobStage`.
      Status: DONE.
- [x] EF Core mappings and `DbSet`s for `MeetingJob`, `MeetingTranscript`,
      `MeetingMinutes`.
      Status: DONE.
- [x] EF Core migrations.
      Status: DONE.
      Notes: Added initial PostgreSQL migration under
      `src/MeetingMind.Infrastructure/Persistence/Migrations`.
- [ ] Repository pattern / persistence abstractions.
      Status: PARTIAL.
      Notes: Added `IMeetingJobRepository` for upload job creation. Broader
      persistence abstractions can be added as future use cases need them.
- [x] `IFileStorageService` interface.
      Status: DONE.
- [x] Local `IFileStorageService` implementation.
      Status: DONE.
- [x] API controller structure.
      Status: DONE.
      Notes: Replace endpoint mappings in `Program.cs` with controllers:
      `HealthController` and `MeetingsController`.
- [ ] `IAudioProcessingService` interface.
      Status: TODO.
- [ ] FFmpeg `IAudioProcessingService` implementation.
      Status: TODO.
- [ ] `ITranscriptionService` interface.
      Status: TODO.
- [ ] Whisper `ITranscriptionService` implementation.
      Status: TODO.
- [ ] `IMeetingMinutesService` interface.
      Status: TODO.
- [ ] GPT `IMeetingMinutesService` implementation with structured JSON output.
      Status: TODO.
- [x] `IBackgroundJobService` interface.
      Status: DONE.
- [x] `IBackgroundJobService` implementation.
      Status: DONE.
      Notes: Implemented with Hangfire.
- [ ] Upload/status/result/retry/download API endpoints.
      Status: PARTIAL.
      Notes: Upload and status endpoints exist. Result, retry, and download
      endpoints remain TODO.
- [x] Remove placeholder weather endpoint and template classes.
      Status: DONE.

## Frontend

- [x] Replace Vite starter screen with MeetingMind application shell.
      Status: DONE.
- [ ] Install/add documented frontend stack dependencies: Material UI and Axios.
      Status: PARTIAL.
      Notes: Dependencies are listed in `package.json`; run `npm install` once
      Node/npm is available to update `package-lock.json` and install packages.
- [ ] Upload UI.
      Status: TODO.
- [ ] Progress polling / status display.
      Status: TODO.
- [ ] Transcript + minutes view.
      Status: TODO.
- [ ] Download buttons.
      Status: TODO.
- [ ] Processing history view.
      Status: TODO.
- [ ] Retry failed job action.
      Status: TODO.

## Known gaps / doc mismatches to revisit

- [x] Queue decision mismatch.
      Status: DONE.
      Notes: Resolved 2026-07-06. Hangfire is the selected queue/workflow
      technology for the local MVP.
- [ ] No auth in v1 is intentional.
      Status: TODO verify when retry endpoint exists.
      Notes: Confirm retry endpoint has no access control anywhere in code,
      matching PRD/FSD/SAD. Do not add auth prematurely.
- [ ] Long-transcript chunking / hierarchical summarization.
      Status: TODO design later.
      Notes: Identified as a PRD risk and needed before testing long meetings.
- [x] Local storage folder structure is not created/configured yet.
      Status: DONE.
      Expected: `Storage/Audio/Original`, `Storage/Audio/Processed`,
      `Storage/Transcript`, `Storage/Minutes`.
      Notes: Folders are configured and created by `LocalFileStorageService`.
- [ ] Secrets/configuration strategy is incomplete.
      Status: TODO.
      Notes: OpenAI settings, storage settings, processing retry settings, and
      max upload size are not present yet.

## Proposed phase plan

Each phase should be implemented, built/tested, reported, and reflected in this
backlog before continuing to the next phase.

1. Phase 1 - Clean foundation and contracts
   - Remove template placeholder classes and default weather endpoint.
   - Add domain entities/enums.
   - Add Application interfaces and DTO/result contracts only.
   - Verify backend build.

2. Phase 2 - Persistence model and database migration
   - Add `DbSet`s and EF configuration for the domain model.
   - Align EF/Npgsql package versions if needed.
   - Add initial EF migration.
   - Verify build and migration creation.

3. Phase 3 - Local storage and upload job creation
   - Add storage options and local file storage implementation.
   - Add upload endpoint with file validation.
   - Save original file, create `MeetingJob`, return `jobId` immediately.
   - Do not enqueue processing yet.
   - Verify upload path and DB job creation.

4. Phase 4 - Hangfire background job integration
   - Add Hangfire packages/configuration.
   - Implement `IBackgroundJobService` with Hangfire.
   - Add Hangfire job entry point for the meeting-processing workflow.
   - Keep workflow steps stubbed until later phases.
   - Verify upload can enqueue a job and the API still returns immediately.

5. Phase 5 - Background job shell and status tracking
   - Enqueue Hangfire jobs from upload.
   - Add status updates for queued/processing/failed/completed stages using
     stub processing steps.
   - Add status endpoint.
   - Verify API returns immediately and status can be polled.

6. Phase 6 - FFmpeg audio processing
   - Implement `IAudioProcessingService`.
   - Add audio validation/transcoding step.
   - Persist processed file path and stage/progress updates.
   - Verify with a small sample audio file.

7. Phase 7 - Transcription integration
   - Implement Whisper transcription client behind `ITranscriptionService`.
   - Save transcript record and transcript file.
   - Add transcript download path.
   - Verify with configured secrets/environment variables.

8. Phase 8 - Meeting minutes generation
   - Implement GPT minutes client behind `IMeetingMinutesService`.
   - Persist structured minutes record and file.
   - Add result and minutes download endpoints.
   - Verify completed end-to-end processing for a short sample.

9. Phase 9 - Retry and history
   - Add failed-job retry endpoint with no auth.
   - Add processing history endpoint.
   - Verify retry behavior and unauthenticated access.

10. Phase 10 - Frontend MVP
    - Replace starter UI with upload/status/result/history flows.
    - Add Material UI and Axios.
    - Implement polling, downloads, retry.
    - Verify frontend build and basic browser flow.

## Done (archive)

- [x] Solution scaffold created - verified 2026-07-06.
- [x] Docker PostgreSQL service added - verified 2026-07-06.
- [x] API `UseNpgsql` dependency issue fixed - verified 2026-07-06.
- [x] Queue technology decided: Hangfire - done 2026-07-06.
- [x] Processing decoupling plan unblocked by Hangfire decision - done 2026-07-06.
- [x] Removed default `/weatherforecast` endpoint and template classes - done 2026-07-06.
- [x] Added domain entities/enums and DbContext DbSets/mappings - done 2026-07-06.
- [x] Added initial EF Core PostgreSQL migration - done 2026-07-06.
- [x] Added local storage and backend upload job creation - done 2026-07-06.
- [x] Refactored API endpoints into controllers - done 2026-07-07.
- [x] Added Hangfire PostgreSQL enqueue/server/dashboard integration - done 2026-07-07.
- [x] Added job status endpoint and configurable stub delay - done 2026-07-11.

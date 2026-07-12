# BACKLOG.md - MeetingMind AI

Living task list. Update the status of an item as soon as it is done. This is
how Codex or any agent picks up where the last session left off without
re-explaining state. Keep completed items here briefly, then move them to the
"Done (archive)" section at the bottom so the active list stays short.

Status legend: `TODO` | `IN PROGRESS` | `BLOCKED` | `PARTIAL` | `DONE`

Last verified against code: 2026-07-12.

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
- [x] Phase 6 - FFmpeg audio processing.
      Status: DONE.
      Notes: Added `IAudioProcessingService`, FFmpeg-backed Whisper-ready WAV
      transcoding via `FFMpegCore`, Worker validation/transcoding status
      updates, processed file path persistence, and a clear Phase 7 boundary
      failure message: "Transcription not yet implemented".
- [x] Correction - Phase 6 should output Whisper-ready WAV, not MP3.
      Status: DONE.
      Description: Phase 6 currently outputs a normalized MP3, while Phase 7
      performs a second internal FFmpeg conversion to produce a Whisper-ready
      WAV. This is the wrong ownership boundary. Phase 6 must own all audio
      conversion and persist a Whisper-compatible WAV directly:
      `pcm_s16le`, `16000Hz`, mono. Phase 7 should receive the processed WAV
      from `MeetingJob.ProcessedFilePath` and feed it directly to Whisper with
      no additional FFmpeg conversion.
      Acceptance criteria:
      - `IAudioProcessingService` outputs `.wav` files under
        `Storage/Audio/Processed`.
      - Processed audio is encoded as `pcm_s16le`, `16000Hz`, mono.
      - `MeetingJob.ProcessedFilePath` points to the WAV output.
      - Phase 7 transcription does not run FFmpeg or create a temporary WAV.
      - Manual test confirms upload -> Phase 6 processed WAV -> Phase 7
        Whisper transcription works without a second conversion.
- [x] Phase 7 - Transcription integration.
      Status: DONE.
      Notes: Added local Whisper transcription behind `ITranscriptionService`
      using `Whisper.net`, CPU runtime, configurable/autodownloaded local
      model files, transcript `.txt` storage, transcript DB persistence, and
      transcript download endpoint. GPT minutes generation remains out of
      scope.
- [x] Phase 8 - Meeting minutes generation.
      Status: DONE.
      Notes: Added OpenAI-backed GPT meeting minutes generation behind
      `IMeetingMinutesService`, configurable OpenAI settings, structured JSON
      persistence, Markdown minutes file storage, result endpoint, minutes
      download endpoint, and Worker completion flow.
- [x] Phase 9 - Retry and history.
      Status: DONE.
      Notes: Added unauthenticated manual retry endpoint for Failed/Cancelled
      jobs, reset-and-requeue behavior using the same job ID, and paginated
      processing history endpoint.
- [ ] Phase 10 - Frontend MVP.
      Status: TODO.
      Notes: Next active focus. Build upload, status polling, transcript/minutes
      viewing, downloads, retry, and history flows in the React app.

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
- [x] FR-003 Background processing pipeline: validate -> transcode ->
      transcript -> clean -> minutes -> save -> complete.
      Status: DONE for backend pipeline.
      Notes: Hangfire now executes the Worker flow through upload, validation,
      FFmpeg WAV transcoding, processed path persistence, local Whisper
      transcription, transcript file storage, transcript DB persistence,
      OpenAI GPT meeting-minutes generation, Markdown minutes storage,
      structured minutes DB persistence, and completion status updates.
- [ ] FR-004 Job status tracking: status, progress %, stage, error message,
      duration.
      Status: PARTIAL.
      Notes: `GET /api/meetings/{jobId}/status` exposes job ID, status, stage,
      progress, and error message. Explicit duration is not exposed yet.
- [x] FR-005 Audio transcoding via FFmpeg.
      Status: DONE.
      Notes: Worker converts uploaded audio to Whisper-ready WAV
      (`pcm_s16le`, `16000Hz`, mono) through `IAudioProcessingService` and
      saves `ProcessedFilePath`.
- [x] FR-006 Transcription via local Whisper.
      Status: DONE.
      Notes: Uses local `Whisper.net` with CPU runtime and configurable model
      provisioning. This intentionally replaces the earlier OpenAI Whisper API
      wording for the local MVP.
- [x] FR-007 Meeting minutes generation via GPT: title, summary, attendees,
      discussion, decisions, action items, risks, next steps.
      Status: DONE for backend Worker.
      Notes: Attendees and discussion points are stored in `FullMinutesJson`;
      dedicated DB columns were intentionally deferred.
- [x] FR-008 View/download transcript and minutes.
      Status: DONE for backend API.
      Notes: Transcript download, minutes result, and minutes Markdown download
      endpoints exist. Frontend result screens remain TODO under Frontend.
- [x] FR-009 Retry failed jobs: no auth check, any user.
      Status: DONE for backend API.
      Notes: `POST /api/meetings/{jobId}/retry` allows Failed/Cancelled jobs,
      reuses the same job ID, resets status to Queued/Uploaded, clears error
      and run timestamps, stores a new Hangfire job ID, and returns 202.
- [x] FR-010 Processing history.
      Status: DONE for backend API.
      Notes: `GET /api/meetings/history?skip=0&take=50` returns a bounded job
      list with status, stage, progress, timestamps, original filename, and
      error message. Default take is 50; max take is 100.

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
      Notes: Added `IMeetingJobRepository` for upload job creation, status
      updates, Hangfire job ID updates, reads, and processed file path
      persistence. Broader persistence abstractions can be added as future use
      cases need them.
- [x] `IFileStorageService` interface.
      Status: DONE.
- [x] Local `IFileStorageService` implementation.
      Status: DONE.
- [x] API controller structure.
      Status: DONE.
      Notes: Replace endpoint mappings in `Program.cs` with controllers:
      `HealthController` and `MeetingsController`.
- [x] `IAudioProcessingService` interface.
      Status: DONE.
- [x] FFmpeg `IAudioProcessingService` implementation.
      Status: DONE.
      Notes: Implemented with `FFMpegCore`; FFmpeg binaries must be available
      on PATH or configured via `AudioProcessing:FfmpegBinaryFolder`.
- [x] `ITranscriptionService` interface.
      Status: DONE.
- [x] Local Whisper `ITranscriptionService` implementation.
      Status: DONE.
      Notes: Implemented with `Whisper.net` and `Whisper.net.Runtime`.
- [x] `IMeetingMinutesService` interface.
      Status: DONE.
- [x] GPT `IMeetingMinutesService` implementation with structured JSON output.
      Status: DONE.
      Notes: Implemented with the official OpenAI .NET SDK, strict structured
      JSON response format, configurable model defaulting to `gpt-4.1`, API key
      lookup via `OpenAI:ApiKey` then `OPENAI_API_KEY`, and a configurable
      transcript length guard defaulting to 120000 characters.
- [x] `IBackgroundJobService` interface.
      Status: DONE.
- [x] `IBackgroundJobService` implementation.
      Status: DONE.
      Notes: Implemented with Hangfire.
- [x] Upload/status/result/retry/download API endpoints.
      Status: DONE for backend API.
      Notes: Upload, status, result, transcript download, and minutes download
      endpoints exist. Retry and history endpoints also exist. Frontend API
      usage remains TODO under Frontend.
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
      Status: DONE verified for backend retry endpoint.
      Notes: Retry endpoint has no access control, matching PRD/FSD/SAD.
- [ ] Long-transcript chunking / hierarchical summarization.
      Status: TODO design later.
      Notes: Identified as a PRD risk and needed before testing long meetings.
- [x] Local storage folder structure is not created/configured yet.
      Status: DONE.
      Expected: `Storage/Audio/Original`, `Storage/Audio/Processed`,
      `Storage/Transcript`, `Storage/Minutes`.
      Notes: Folders are configured and created by `LocalFileStorageService`.
- [ ] Secrets/configuration strategy is incomplete.
      Status: PARTIAL.
      Notes: Storage, max upload size, and audio processing settings are
      present. Local transcription and GPT/OpenAI settings are present.
      Hangfire automatic retry settings are not present; Phase 9 intentionally
      added manual retry only.

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
   - Implement local Whisper transcription behind `ITranscriptionService`.
   - Save transcript record and transcript file.
   - Add transcript download path.
   - Verify with configured or downloaded local Whisper model.

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

# BACKLOG.md - MeetingMind AI

Living task list. Update the status of an item as soon as it is done. This is
how Codex or any agent picks up where the last session left off without
re-explaining state. Keep completed items here briefly, then move them to the
"Done (archive)" section at the bottom so the active list stays short.

Status legend: `TODO` | `IN PROGRESS` | `BLOCKED` | `PARTIAL` | `DONE`

Last verified against code: 2026-07-06.

## Current focus

- [x] Decide queue technology: Hangfire vs MSMQ vs SQLite-backed queue.
      Status: DONE.
      Notes: Decision recorded 2026-07-06. Use Hangfire for queueing,
      background job execution, retries, and workflow orchestration.
- [x] Decouple the synchronous `ffmpeg -> transcription -> minutes` chain into
      distinct queued stages to resolve API timeout errors.
      Status: DONE for planning/decision.
      Notes: Unblocked by Hangfire decision. Implementation is tracked under
      FR-003 and Phases 4-8. Transcription will use Whisper; meeting-minutes
      generation will use OpenAI GPT.

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
- [ ] API still has default `/weatherforecast` endpoint.
      Status: TODO cleanup.
- [ ] Domain/Application/Infrastructure/Shared still contain template
      `Class1.cs` placeholder files.
      Status: TODO cleanup once real types are added.
- [ ] `MeetingMindDbContext` exists and is registered in `Program.cs`.
      Status: PARTIAL.
      Notes: The DbContext has no `DbSet`s because the domain entities do not
      exist yet.
- [ ] EF provider dependency for API compile-time `UseNpgsql` is present.
      Status: DONE.
- [ ] EF package versions should be reviewed/aligned before migrations.
      Status: TODO.
      Notes: API references `Npgsql.EntityFrameworkCore.PostgreSQL` 9.0.4;
      Infrastructure references `Microsoft.EntityFrameworkCore` 9.0.17.
- [ ] React frontend skeleton exists under `frontend/meetingmind-ui`.
      Status: PARTIAL.
      Notes: It is still the default Vite starter screen, not MeetingMind UI.
- [ ] Frontend dependencies are incomplete for the documented stack.
      Status: TODO.
      Notes: React is installed, but Material UI and Axios are not currently in
      `package.json`.
- [ ] Tests are not scaffolded.
      Status: TODO.

## Core pipeline (FR-001 to FR-010, see docs/FSD.md)

- [ ] FR-001 Upload meeting: MP3/WAV/M4A/AAC, configurable max size.
      Status: TODO.
- [ ] FR-002 Create processing job: save file, DB record, job ID, enqueue,
      return immediately.
      Status: TODO.
- [ ] FR-003 Background processing pipeline: validate -> transcode ->
      transcript -> clean -> minutes -> save -> complete.
      Status: TODO.
      Notes: Use Hangfire to execute the workflow asynchronously.
- [ ] FR-004 Job status tracking: status, progress %, stage, error message,
      duration.
      Status: TODO.
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
- [ ] Domain entities and enums: `MeetingJob`, `MeetingTranscript`,
      `MeetingMinutes`, `MeetingJobStatus`, `MeetingJobStage`.
      Status: TODO.
- [ ] EF Core mappings and `DbSet`s for `MeetingJob`, `MeetingTranscript`,
      `MeetingMinutes`.
      Status: TODO.
- [ ] EF Core migrations.
      Status: TODO.
- [ ] Repository pattern / persistence abstractions.
      Status: TODO.
- [ ] `IFileStorageService` interface.
      Status: TODO.
- [ ] Local `IFileStorageService` implementation.
      Status: TODO.
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
- [ ] `IBackgroundJobService` interface.
      Status: TODO.
- [ ] `IBackgroundJobService` implementation.
      Status: TODO.
      Notes: Implement with Hangfire.
- [ ] Upload/status/result/retry/download API endpoints.
      Status: TODO.
- [ ] Remove placeholder weather endpoint and template classes.
      Status: TODO.

## Frontend

- [ ] Replace Vite starter screen with MeetingMind application shell.
      Status: TODO.
- [ ] Install/add documented frontend stack dependencies: Material UI and Axios.
      Status: TODO.
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
- [ ] Local storage folder structure is not created/configured yet.
      Status: TODO.
      Expected: `Storage/Audio/Original`, `Storage/Audio/Processed`,
      `Storage/Transcript`, `Storage/Minutes`.
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

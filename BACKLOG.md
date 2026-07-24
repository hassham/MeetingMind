# MeetingMind backlog

Phase 2 was completed and verified on 2026-07-19. Its complete backlog and
acceptance record are preserved in
[`docs/archive/phase2/BACKLOG_completed.md`](docs/archive/phase2/BACKLOG_completed.md).

This file is the authoritative tracker for active Phase 3 work. Product scope
and the delivery sequence were approved on 2026-07-22. The approved plan is
preserved in [`docs/phase3/PLAN.md`](docs/phase3/PLAN.md).

Status legend: `TODO` | `IN PROGRESS` | `BLOCKED` | `PARTIAL` | `DONE`

Last updated: 2026-07-22.

## Current delivery cycle: Phase 3

**Theme:** Flexible meeting inputs, useful dashboards, readable transcripts,
and independently trackable actions.

**Overall status:** `IN PROGRESS` — P3-02 mode-aware jobs and safe migration
completed on 2026-07-22. P3-03 is next and has not started.

### Phase 3 outcome

Deliver three asynchronous workflows:

1. Audio to transcript only.
2. Audio to transcript and meeting minutes.
3. Uploaded transcript to meeting minutes.

Add a dashboard landing page, readable timestamp-aware transcripts, a
meeting-minutes library/detail experience, and a standalone Actions domain.
Meetings remain completed immutable records. Actions have their own lifecycle;
they may retain or optionally add a meeting link for provenance without
changing meeting status, progress, completion, or generated minutes.

### Approved scope baseline

In scope:

- Dashboard with all-time aggregate metrics and recent activity.
- Explicit processing mode persisted on every job.
- `.txt` and `.md` UTF-8 transcript upload for minutes generation.
- Structured Whisper segments and deterministic transcript paragraphing.
- Completed-minutes library and deep-linked meeting detail screen.
- Independent generated and manually created actions.
- Optional action-to-meeting provenance links.
- Action status, assignee, due date, notes, filtering, concurrency protection,
  and CSV/JSON export.
- Safe Phase 2 data migration and idempotent legacy action backfill.
- Backend, frontend, persistence, Worker, accessibility, migration, and local
  end-to-end verification.

Out of scope:

- Authentication, authorization, ownership, multi-tenancy, public deployment,
  or cloud migration.
- Direct Jira or other third-party connectors. Phase 3 provides a stable export
  boundary only.
- Speaker diarization or recognition.
- LLM transcript rewriting/polishing.
- PDF/DOCX transcript import.
- Editing generated meeting minutes.
- Semantic search, notifications, live recording, or live transcription.
- Reprocessing a completed job into another mode; users create a new job.

### Approved product decisions

| Decision | Approved Phase 3 behavior |
| --- | --- |
| Legacy actions | Idempotently backfill independent actions from existing generated minutes. |
| Transcript timestamps | Show paragraph-start timestamps in the viewer and provide a clean text download. |
| Transcript upload | Accept `.txt` and `.md` UTF-8 text only. |
| Meeting details | Use generated title, source filename/type, and processing timestamps; no new editable metadata in Phase 3. |
| Action export | Deliver provider-neutral CSV and JSON; direct Jira integration is deferred. |
| Dashboard period | Use clearly labelled all-time aggregates; date filtering is deferred. |
| Action ownership | Actions are standalone; meeting links are optional provenance/context and do not drive meeting lifecycle. |
| Compatibility | Preserve `/api/meetings/upload` as a deprecated `FullMeeting` compatibility alias during Phase 3. |

## Workflow and tracking rules

Every work package follows [`WORKFLOW.md`](WORKFLOW.md):

1. Complete Discovery and resolve ambiguities before implementation.
2. Obtain confirmation for that package's exact implementation plan.
3. Implement only the approved package.
4. Verify the package before starting the next one.
5. Add a Completion Report and update this backlog with completion date and
   evidence.

Additional tracking rules:

- Work packages execute sequentially unless this backlog explicitly records an
  approved exception.
- Only one package may be `IN PROGRESS` at a time.
- A checked item means its acceptance evidence exists; partial implementation
  remains unchecked and is described in package Notes.
- No package becomes `DONE` merely because code exists. Its acceptance criteria,
  automated verification, documentation, and applicable migration checks must
  all pass.
- New decisions are recorded in the Phase 3 decision archive before work
  depending on them proceeds.
- `BACKLOG.md` is updated after each Completion Report, including exact commands
  or evidence paths where practical.

## Current focus

P3-02 is complete. P3-03 is next and must begin with its own `WORKFLOW.md`
Discovery Report and confirmation before new upload endpoints or frontend
workflow changes are implemented.

### P3-01 — Reconcile Phase 3 requirements, UX, and contracts

**Status:** `DONE`

**Depends on:** Approved Phase 3 plan — satisfied 2026-07-22.

**Started:** 2026-07-22.

**Owner:** Unassigned.
**Completed:** 2026-07-22.

- [x] Approve the Phase 3 product scope and ordered delivery sequence.
      Status: `DONE`. Completed: 2026-07-22.
- [x] Confirm that Actions are an independent domain and meeting links are
      optional provenance only.
      Status: `DONE`. Completed: 2026-07-22.
- [x] Approve the recommended plan decisions recorded in the scope baseline.
      Status: `DONE`. Completed: 2026-07-22.
- [x] Update `docs/PRD.md` for Phase 3 goals, users, scope, stories, success
      measures, and explicit exclusions.
      Status: `DONE`. Completed: 2026-07-22.
- [x] Update `docs/FSD.md` with numbered Phase 3 functional requirements,
      validation rules, status/progress semantics, action behavior, API
      contracts, and verification requirements.
      Status: `DONE`. Completed: 2026-07-22.
- [x] Update `docs/SAD.md` with mode dispatch, structured transcription,
      independent Action aggregate, data relationships, retention behavior,
      query/index strategy, and compatibility decisions.
      Status: `DONE`. Completed: 2026-07-22.
- [x] Define exact dashboard formulas, including terminal denominator, active
      states, audio-duration availability, overdue date/timezone behavior, and
      recent-item limits.
      Status: `DONE`. Completed: 2026-07-22.
- [x] Define exact transcript upload limits, MIME handling, UTF-8 validation,
      filename behavior, and safe error responses.
      Status: `DONE`. Completed: 2026-07-22.
- [x] Define exact action fields, maximum lengths, nullable fields, status
      transitions, sorting/filtering, link/unlink rules, optimistic concurrency,
      and meeting retention behavior.
      Status: `DONE`. Completed: 2026-07-22.
- [x] Finalize request/response DTOs and compatibility semantics for all new
      and changed endpoints before implementation.
      Status: `DONE`. Completed: 2026-07-22.
- [x] Produce low-fidelity UX states for Dashboard, New Processing Job, Meeting
      Minutes, Meeting Detail, Actions, All Processing, and loading/empty/error
      states.
      Status: `DONE`. Completed: 2026-07-22.
- [x] Reconcile `AGENTS.md`, `WORKFLOW.md`, README guidance, and durable docs so
      no active statement incorrectly describes Phase 2 as the current cycle.
      Status: `DONE`. Completed: 2026-07-22.

Acceptance criteria:

- PRD, FSD, SAD, AGENTS.md, BACKLOG.md, and the approved Phase 3 plan use the
  same terminology and scope.
- Every processing mode, metric, action status, validation boundary, relation,
  endpoint, and compatibility rule has one testable definition.
- UX states cover success, active, loading, empty, validation, retry, conflict,
  and not-found behavior.
- No database or production-code work begins with an unresolved schema or API
  decision.
- The package Completion Report records the reconciled documents and confirms
  P3-02 is unblocked.

Evidence:

- Scope approval: user approval on 2026-07-22.
- Approved plan: [`docs/phase3/PLAN.md`](docs/phase3/PLAN.md).
- Decision record:
  [`docs/archive/phase3/QUESTIONS_p3_01_requirements_ux_contracts.md`](docs/archive/phase3/QUESTIONS_p3_01_requirements_ux_contracts.md).
- Product requirements: [`docs/PRD.md`](docs/PRD.md).
- Functional contracts: [`docs/FSD.md`](docs/FSD.md).
- Architecture and ADRs: [`docs/SAD.md`](docs/SAD.md).
- UX states: [`docs/phase3/UX.md`](docs/phase3/UX.md).
- Consistency verification: `git diff --check` passed; active-document searches
  found no stale Active/Approved Phase 2 status or unresolved Phase 3 decision
  heading. Historical Phase 2 archive wording was intentionally preserved.

Notes:

- No production code, package, or migration was changed in P3-01.
- P3-02 remains `TODO` until its Discovery and Confirmation phases complete.

## Ordered Phase 3 work packages

### P3-02 — Introduce mode-aware jobs and safe migrations

**Status:** `DONE`

**Depends on:** P3-01.

**Started:** 2026-07-22.

**Completed:** 2026-07-22.

- [x] Add `MeetingProcessingMode`: `TranscriptOnly`, `FullMeeting`, and
      `MinutesFromTranscript`.
- [x] Add approved mode/source metadata and mode-valid input rules to
      `MeetingJob`.
- [x] Make audio-specific fields nullable only where required and enforce valid
      combinations in Domain/Application and database constraints where
      practical.
- [x] Add EF Core migration and indexes for processing mode and the approved
      query patterns.
- [x] Migrate every existing job to `FullMeeting` without altering artifacts,
      timestamps, status, retry eligibility, or results.
- [x] Update status/history Application and API contracts to expose mode safely.
- [x] Make retry and durable-checkpoint rules mode-aware.
- [x] Update retention eligibility/path handling for mode-specific inputs.
- [x] Add Domain, Application, repository, migration, API contract, and Worker
      regression tests.
- [x] Run the complete existing backend and frontend regression suites.

Acceptance criteria:

- Existing Phase 2 jobs remain readable, downloadable, and retryable.
- Invalid mode/input combinations cannot be created or persisted through normal
  application paths.
- The current full-audio pipeline still completes end to end.
- Migrations upgrade representative Phase 2 data without loss and can create a
  clean database successfully.
- Mode-specific jobs never require an irrelevant audio or transcript path.

Evidence:

- Decision record:
  [`docs/archive/phase3/QUESTIONS_p3_02_mode_aware_jobs_migration.md`](docs/archive/phase3/QUESTIONS_p3_02_mode_aware_jobs_migration.md).
- Migration `20260722021323_AddProcessingModeAndAudioDuration` adds the required
  string mode with `FullMeeting` backfill/default, nullable whole-second audio
  duration, three check constraints, and three approved indexes.
- `dotnet build MeetingMind.sln -c Release --no-restore` passed with zero
  warnings and errors. Release output was used because the developer's running
  Debug API/Worker processes held Debug assemblies open.
- `dotnet test MeetingMind.sln -c Release --no-build --no-restore` passed 99
  backend tests: 39 unit, 34 Worker, 17 Infrastructure integration, and 9 API
  integration.
- PostgreSQL tests applied migrations to a clean database and upgraded a schema
  stopped at `20260719045819_AddSafeErrorCode`; the legacy row retained its data
  and received `FullMeeting` plus null audio duration.
- `npm.cmd run lint`, `npm.cmd test -- --run` (19 tests), and
  `npm.cmd run build` passed.
- `git diff --check` passed.

Notes:

- Existing input field names remain generic source fields. Audio duration is
  nullable `long` seconds. Mode persists as a constrained readable string.
- Worker tests prove FullMeeting retains its pipeline, TranscriptOnly skips
  minutes, and MinutesFromTranscript skips FFmpeg/Whisper and requires a valid
  transcript checkpoint.
- Retention labels transcript sources correctly, ignores irrelevant processed
  audio, and deletes a duplicated source/transcript path only once.
- P3-03 remains `TODO`; no new creation endpoint, transcript upload validation,
  or frontend workflow selector was added.

### P3-03 — Deliver all three creation workflows

**Status:** `TODO`

**Depends on:** P3-02.
**Completion:** Not started.

- [ ] Add the explicit audio creation endpoint and processing-mode validation.
- [ ] Preserve `/api/meetings/upload` as a documented deprecated
      `FullMeeting` alias.
- [ ] Add `.txt`/`.md` transcript upload with safe filename, extension, MIME,
      size, UTF-8, binary, non-empty, whitespace-only, and character-limit
      validation.
- [ ] Store transcript uploads behind `IFileStorageService`; never expose local
      paths.
- [ ] Dispatch the Worker pipeline by processing mode through Application
      abstractions.
- [ ] Implement `TranscriptOnly` without an OpenAI minutes call.
- [ ] Implement `FullMeeting` without regressing the current pipeline.
- [ ] Implement `MinutesFromTranscript` without FFmpeg or Whisper calls.
- [ ] Define and implement monotonic stages/progress for every mode without
      presenting skipped work as executed.
- [ ] Make failure classification, automatic retry, manual retry, cancellation,
      and checkpoint reuse correct for every mode.
- [ ] Build the New Processing Job UI with three clear choices and mode-specific
      upload guidance.
- [ ] Add unit, persistence, API, Worker, and frontend tests for success and
      failure paths in every mode.

Acceptance criteria:

- Every accepted request returns `202 Accepted` after validation, storage,
  persistence, and enqueueing only.
- Test doubles prove that each mode invokes only its required providers.
- Browser refresh or closure does not interrupt accepted work.
- Invalid transcript files fail before enqueue and leave no active job.
- Retry resumes from a valid, mode-relevant checkpoint.
- Existing full-meeting clients continue to work through the compatibility
  endpoint.

Evidence: Pending.

### P3-04 — Produce readable structured transcripts

**Status:** `TODO`

**Depends on:** P3-03.
**Completion:** Not started.

- [ ] Change `ITranscriptionService` to return an approved structured result
      without leaking Whisper provider types.
- [ ] Preserve normalized segment start time, end time, and text.
- [ ] Persist structured segments and formatting metadata using the approved
      storage model.
- [ ] Implement deterministic whitespace normalization and paragraph rendering.
- [ ] Break paragraphs using configurable silence, preferred-size punctuation,
      and hard-size rules while avoiding short one-line fragments.
- [ ] Default to a 1.5-second silence gap, 300-character preferred size, and
      700-character hard bound unless P3-01 evidence records approved tuning.
- [ ] Validate all formatting configuration at API/Worker startup where used.
- [ ] Show paragraph-start timestamps in the viewer and provide a clean text
      transcript download.
- [ ] Reuse valid structured transcript checkpoints after retry without calling
      Whisper again.
- [ ] Add tests for punctuation, silence, hard bounds, empty segments,
      cancellation, persistence, retry, and deterministic rerendering.
- [ ] Verify representative audio manually without claiming speaker identity.

Acceptance criteria:

- Formatting loses or invents no recognized words.
- Identical structured segments and configuration produce identical output.
- Paragraph hard bounds hold except for one indivisible recognized token whose
  handling is explicitly documented.
- Timestamps are monotonic and remain provider-neutral outside Infrastructure.
- Valid checkpoints avoid retranscription during retry.
- No UI or download labels paragraphs as speaker turns.

Evidence: Pending.

### P3-05 — Build the dashboard and application navigation

**Status:** `TODO`

**Depends on:** P3-04.
**Completion:** Not started.

- [ ] Add bounded aggregate repository queries rather than loading all history.
- [ ] Add Dashboard Application service and `GET /api/dashboard/summary`.
- [ ] Return approved all-time job totals, counts by mode/status, success rate,
      duration/audio measures, transcript/minutes totals, action counts, and
      bounded recent items.
- [ ] Add client-side routes for Dashboard, New Processing Job, Meeting Minutes,
      Meeting Detail, Actions, and All Processing.
- [ ] Make Dashboard the initial route and preserve deep links after refresh.
- [ ] Render loading, zero-data, partial-data, error, and mixed-status states.
- [ ] Link dashboard cards/recent items to their relevant destinations without
      coupling action state to meeting state.
- [ ] Add query, Application, API contract, frontend, responsive, keyboard, and
      automated accessibility tests.

Acceptance criteria:

- Every metric matches seeded database truth, including zero denominator and
  mixed mode/status/action cases.
- Dashboard requests are bounded and do not retrieve every history/action row.
- All-time scope and unavailable metrics are labelled clearly.
- Direct navigation and refresh work for every primary route.
- Primary dashboard states have no serious or critical automated accessibility
  violations; manual contrast and responsive checks are recorded.

Evidence: Pending.

### P3-06 — Deliver the meeting-minutes library and detail screen

**Status:** `TODO`

**Depends on:** P3-05.
**Completion:** Not started.

- [ ] Add a newest-first, server-paginated completed-minutes query.
- [ ] Expose generated title, source filename/type, and processing timestamps
      without local paths.
- [ ] Build the Meeting Minutes library with loading, empty, error, pagination,
      and not-found behavior.
- [ ] Build a deep-linked Meeting Detail view containing all eight structured
      minutes sections, meeting details, transcript access, and downloads.
- [ ] Show read-only links to actions with that meeting as provenance.
- [ ] Keep active, failed, cancelled, and transcript-only work in All
      Processing rather than the minutes library.
- [ ] Ensure action edits never mutate meeting status, progress, timestamps, or
      generated-minutes JSON.
- [ ] Add query, API, frontend, deep-link refresh, pagination, keyboard, and
      accessibility tests.

Acceptance criteria:

- Only records with persisted minutes appear in the library.
- Direct meeting links survive browser refresh and return a safe not-found state
  for unknown or unavailable records.
- Meeting details and all eight sections match persisted immutable data.
- Action provenance is navigable but never presented as meeting progress.
- Existing transcript and minutes downloads remain correct.

Evidence: Pending.

### P3-07 — Deliver independent action management and export

**Status:** `TODO`

**Depends on:** P3-06.
**Completion:** Not started.

- [ ] Add the independent `Action` aggregate using the exact P3-01-approved
      fields, validation, status, source, and concurrency rules.
- [ ] Add an optional meeting provenance relationship that does not own action
      lifecycle.
- [ ] Add EF migration, constraints, status/due-date/source/meeting indexes, and
      restrictive or link-nulling retention behavior.
- [ ] Idempotently seed generated actions after minutes persistence and retries.
- [ ] Backfill existing generated minutes once without duplicate actions.
- [ ] Add standalone Application services for action create, get, paginated
      list/filter, update, link, unlink, and export.
- [ ] Add thin `/api/actions` endpoints and the read-only
      `/api/meetings/{jobId}/actions` context query.
- [ ] Support server-side filtering by status, assignee, due/overdue state,
      source, and linked meeting with deterministic ordering.
- [ ] Return a conflict for stale optimistic-concurrency updates.
- [ ] Define `IActionItemExporter` in Application and implement safe CSV and
      JSON exports without provider credentials or local paths.
- [ ] Build the standalone Actions screen with creation, editing, status,
      filters, optional meeting linking, meeting-context navigation, and export.
- [ ] Add independent action counts to the dashboard without deriving a meeting
      workflow status.
- [ ] Add unit, persistence, migration/backfill, API contract, frontend,
      accessibility, concurrency, retention, and export tests.

Acceptance criteria:

- Generated actions are seeded once even when generation or persistence retries.
- Manual actions work with or without a meeting link.
- Linking, unlinking, editing, completing, or cancelling an action never changes
  its meeting or generated-minutes snapshot.
- Approved retention behavior never cascade-deletes an independent action.
- Overdue and status counts use the same documented formulas everywhere.
- Stale updates return a safe conflict and preserve the newer stored values.
- CSV and JSON correctly round-trip commas, quotes, newlines, Unicode, nullable
  fields, and optional meeting provenance.
- No export contains physical paths, secrets, or unsupported Jira claims.

Evidence: Pending.

### P3-08 — Phase 3 release verification and documentation

**Status:** `TODO`

**Depends on:** P3-07.
**Completion:** Not started.

- [ ] Run `dotnet build MeetingMind.sln`.
- [ ] Run `dotnet test MeetingMind.sln`.
- [ ] Run frontend lint, production build, and automated tests.
- [ ] Execute and record local end-to-end success/failure/retry scenarios for
      all three processing modes.
- [ ] Verify structured transcript formatting and timestamps with representative
      audio.
- [ ] Verify dashboard totals against known seeded/local records.
- [ ] Verify minutes navigation, immutable meeting behavior, manual/generated
      actions, optional links, conflicts, filtering, and CSV/JSON exports.
- [ ] Upgrade a copy of representative Phase 2 data and verify job migration and
      idempotent legacy action backfill.
- [ ] Verify a clean database can be created from migrations.
- [ ] Complete manual responsive, keyboard, focus, screen-reader announcement,
      and color-contrast checks for primary Phase 3 states.
- [ ] Update README, local run/test guidance, API documentation, PRD, FSD, SAD,
      AGENTS.md, and WORKFLOW.md where final behavior requires it.
- [ ] Write the Phase 3 Completion Report and archive the completed backlog and
      decision evidence under `docs/archive/phase3/`.

Acceptance criteria:

- Every standard backend and frontend verification command passes.
- Recorded end-to-end evidence covers every Phase 3 mode and user-facing area.
- Representative Phase 2 data upgrades without loss or duplicated actions.
- Clean setup and upgrade setup are both documented and repeatable.
- Durable documentation matches released behavior.
- No Phase 3 package or checklist item remains `TODO`, `PARTIAL`, `IN PROGRESS`,
  or `BLOCKED`.

Evidence: Pending.

## Phase 3 completion gate

Phase 3 is complete only when:

- P3-01 through P3-08 are all `DONE` with completion dates and evidence.
- All three workflows remain asynchronous and invoke only required providers.
- Dashboard figures agree with persisted acceptance data.
- Structured Whisper output renders readable paragraphs without changing
  recognized words or implying speaker identity.
- Completed meetings/minutes are immutable, discoverable, and deep-linkable.
- Actions are independently creatable, trackable, linkable, unlinkable,
  filterable, concurrency-safe, and exportable.
- Action changes never alter meeting workflow state or generated minutes.
- Existing Phase 2 data and legacy upload clients remain usable.
- All automated, accessibility, migration, and local end-to-end gates pass.
- Final documentation and archived evidence match the implementation.

## Recently completed

- [x] Phase 3 scope, delivery sequence, and recommended plan decisions approved.
      Status: `DONE`. Completed: 2026-07-22.
- [x] Independent Actions domain clarification incorporated into the approved
      plan.
      Status: `DONE`. Completed: 2026-07-22.
- [x] Phase 2 local production-readiness and hardening cycle.
      Status: `DONE`. Completed: 2026-07-19. See
      [`docs/archive/phase2/BACKLOG_completed.md`](docs/archive/phase2/BACKLOG_completed.md).

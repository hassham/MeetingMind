# Functional Specification Document

**Product:** MeetingMind AI

**Version:** 3.0

**Status:** Active Phase 3 specification

**Approved:** 2026-07-22

## 1. Purpose and boundary

MeetingMind accepts supported audio or an existing UTF-8 transcript, creates a
persistent asynchronous job, and produces the result selected by the user.
Phase 3 adds dashboard/navigation, readable structured transcripts, a
minutes-only library, and independent actions.

The deployment remains trusted-local and unauthenticated. Meetings/minutes are
completed immutable records. Actions are separate mutable records whose
optional meeting link is provenance only.

## 2. Runtime components and constraints

- React, TypeScript, Material UI, Axios, and client-side routing frontend.
- ASP.NET Core Web API for validation, enqueueing, reads, action commands,
  exports, health, Swagger, and the local Hangfire dashboard.
- Separate .NET Worker hosting Hangfire and long-running processing.
- PostgreSQL via EF Core migrations.
- Local filesystem through `IFileStorageService`.
- FFmpeg behind `IAudioProcessingService`.
- local Whisper.net behind `ITranscriptionService`.
- OpenAI GPT behind `IMeetingMinutesService` and
  `IMeetingMinutesGenerationClient`.

Controllers contain no workflow/persistence/provider logic. The API never
waits for FFmpeg, Whisper, or OpenAI. The Worker uses Application abstractions.

## 3. Processing modes

| Mode | Accepted input | Required Worker operations | Available result |
| --- | --- | --- | --- |
| `TranscriptOnly` | MP3, WAV, M4A, AAC | validate checkpoint, FFmpeg, Whisper, format/save transcript | transcript |
| `FullMeeting` | MP3, WAV, M4A, AAC | validate checkpoint, FFmpeg, Whisper, format/save transcript, generate/save minutes, seed actions | transcript and minutes |
| `MinutesFromTranscript` | UTF-8 `.txt` or `.md` | validate stored transcript checkpoint, generate/save minutes, seed actions | uploaded transcript and minutes |

Every accepted request stores input, creates `MeetingJob`, enqueues through
`IBackgroundJobService`, and returns HTTP 202. A mode never calls a provider it
does not require. Reprocessing into another mode creates a new job.

## 4. Functional requirements

### FR-001 — Create an audio job

`POST /api/meetings/audio` accepts one supported audio file and a required mode
of `TranscriptOnly` or `FullMeeting`. Validate non-empty content, safe filename,
extension, MIME, and configured audio byte limit before enqueueing.

`POST /api/meetings/upload` remains a deprecated compatibility alias that
always creates `FullMeeting`. It retains the Phase 2 success response during
Phase 3.

### FR-002 — Create a transcript job

`POST /api/meetings/transcript` accepts one `.txt` or `.md` file and creates
`MinutesFromTranscript`.

Validation must:

- Require a safe non-empty filename and allowed extension.
- Accept documented plain-text/Markdown MIME values; reject incompatible
  declared content types.
- Reject empty, whitespace-only, binary/NUL-containing, or invalid UTF-8 input.
- Reject content above configurable 10 MB before decoding.
- Normalize CRLF/CR to LF and remove a UTF-8 BOM without changing other text.
- Reject normalized content above configured `MaxTranscriptCharacters`
  (default 1,200,000); never truncate.
- Store the original validated text as a transcript checkpoint behind
  `IFileStorageService` without exposing its path.

### FR-003 — Mode-aware processing

The Worker dispatches by persisted mode and reports monotonic progress. Skipped
providers/stages must not be presented as executed. Mode-invalid persisted
input is a permanent safe failure.

Retry reuses the most advanced valid mode-relevant durable checkpoint:

- Audio modes: structured transcript, then processed audio, then original
  audio.
- Transcript mode: stored transcript.

The Phase 2 configured automatic/manual retry behavior remains in force.

### FR-004 — Job status, mode, progress, and duration

Statuses remain `Queued`, `Processing`, `Completed`, `Failed`, and `Cancelled`.
Existing stages remain valid. A new stage may be added only if the final SAD and
implementation give it meaningful user-visible work; transcript import itself
is validated/stored before enqueue and generation begins at
`GeneratingMinutes` by default.

Status/history expose mode, progress, sanitized error/retry data, timestamps,
`processingDurationSeconds`, and `totalDurationSeconds`. Existing duration
semantics remain unchanged.

Audio jobs persist nullable non-negative `sourceAudioDurationSeconds` after
media inspection. Transcript jobs keep it null.

### FR-005 — Structured readable transcription

`ITranscriptionService` returns provider-neutral ordered segments containing:

- Non-negative start and end times with end not earlier than start.
- Recognized text.

The formatter:

- Normalizes whitespace without changing recognized word content/order.
- Combines short adjacent segments.
- Starts a paragraph for a configured silence gap (default 1.5 seconds), or at
  sentence-ending punctuation after a preferred size (default 300 characters),
  or before exceeding a hard size (default 700 characters).
- Is deterministic for identical segments/configuration.
- Preserves paragraph start/end timing.

Structured segments and formatting metadata are persisted as the durable
source. The viewer shows paragraph-start timestamps. The plain-text download
contains clean paragraphs without timestamps. Formatting never claims speaker
identity. All configuration must be positive, internally valid, and validated
at startup where consumed.

### FR-006 — Structured minutes

Existing single-pass and long-transcript generation rules remain: complete
schema, bounded chunking/aggregation, deterministic merging, safe maximum, and
retry behavior.

Minutes contain title, summary, attendees, discussion points, decisions,
generated action items, risks, and next steps. Persisted minutes and their JSON
snapshot are immutable with respect to later independent action changes.

### FR-007 — Dashboard

`GET /api/dashboard/summary` returns one bounded all-time summary:

- Total jobs and counts per processing mode.
- Counts for `Completed`, `Failed`, `Cancelled`, and active
  (`Queued` + `Processing`).
- Success rate = `Completed / (Completed + Failed) * 100`; cancelled and active
  jobs are excluded. Return null when denominator is zero.
- Sum of `sourceAudioDurationSeconds` for audio jobs with known values.
- Average processing duration for completed jobs with valid values.
- Persisted transcript and minutes counts.
- Independent action counts by status and overdue count.
- Five recent jobs and five recent minutes, newest first.

The endpoint does not retrieve all history/actions and returns no local paths,
transcript body, minutes body, or sensitive provider data.

### FR-008 — Navigation and All Processing

Dashboard is the default route. Routes exist for Dashboard, New Processing
Job, Meeting Minutes, Meeting Detail, Actions, and All Processing. Direct links
survive refresh.

All Processing evolves existing history. It uses server paging, 20 items by
default, maximum 100, newest-first ordering, mode display, active polling, and
existing retry behavior.

### FR-009 — Meeting-minutes library and detail

`GET /api/meetings/minutes` returns only jobs with persisted minutes,
newest-first, 20 per page by default and at most 100. Items include meeting/job
ID, generated title, source filename, source type/mode, and processing
timestamps.

`GET /api/meetings/{jobId}/result` supplies the immutable meeting details and
all eight structured sections. The view provides transcript/minutes downloads
and read-only navigation to linked actions. Action state is never presented as
meeting progress.

### FR-010 — Independent action creation and provenance

Minutes persistence seeds one independent action for every generated action
item. Seeding and legacy backfill are idempotent and cannot duplicate a source
item after retries or repeated backfill execution.

`POST /api/actions` creates a `Manual` action with an optional valid meeting
link. Generated actions use source `Generated` and their origin meeting.

An action contains:

- ID.
- Required trimmed description, 1–2,000 characters.
- Optional trimmed assignee, maximum 200 characters.
- Optional notes, maximum 10,000 characters.
- Optional due date represented as a UTC calendar date without time.
- Status: `Open`, `InProgress`, `Blocked`, `Completed`, or `Cancelled`.
- Source: `Generated` or `Manual`.
- Optional live meeting ID.
- Optional provenance meeting title and source filename snapshots.
- Created, updated, optional completed timestamps.
- Opaque concurrency `version`.

Manual actions default to `Open`. Generated action description/owner/due-date
map from the immutable minutes snapshot; unparseable generated due-date text may
remain null in the tracked action while the original text remains in minutes.

### FR-011 — Action queries, updates, links, and deletion

`GET /api/actions` returns deterministic server-paged results, 25 by default
and at most 100. Filters support status, assignee, due/overdue state, source,
and live meeting ID. The documented default order is newest created first with
ID as a stable tie-breaker.

`GET /api/actions/{actionId}` returns one action and optional provenance.
`GET /api/meetings/{jobId}/actions` returns actions currently linked to a
meeting as read-only context.

`PATCH /api/actions/{actionId}` may change description, assignee, notes, due
date, status, and optional meeting link. It requires the opaque version from the
latest DTO. A stale version returns HTTP 409 with a safe conflict code and the
stored record remains unchanged.

Any status may move to any other status. Entering `Completed` sets
`CompletedAt` to the current time; leaving `Completed` clears it. Re-saving an
already completed action does not replace its completion time unless it was
first reopened.

`DELETE /api/actions/{actionId}` permanently deletes a generated or manual
action only after the UI obtains explicit user confirmation. The API performs
the delete request directly and returns 204 for success; confirmation is a UI
responsibility. Deletion never edits a meeting or generated-minutes JSON.

Linking requires an existing meeting. Unlinking sets the live meeting ID to
null and preserves provenance snapshots. Relinking refreshes snapshots from the
new meeting.

### FR-012 — Action overdue and retention behavior

The system uses UTC date consistently. An action is overdue when:

- `DueDate < current UTC date`, and
- status is not `Completed` or `Cancelled`.

When meeting retention deletes a linked meeting, it must atomically preserve or
populate meeting-title/source-filename snapshots, set the action meeting ID to
null, then delete meeting artifacts/records. No action cascades from meeting
deletion. Deleting an action does not affect retention eligibility.

### FR-013 — Action export

`GET /api/actions/export?format=csv|json` exports selected IDs or the same
documented filters as the action list. Selection/filter bounds must prevent
unbounded memory use; implementation limits are finalized with P3-07 contracts.

Exports include action fields, source, status, UTC due date, timestamps, and
optional meeting/provenance context. CSV uses UTF-8 and correctly quotes commas,
quotes, newlines, and Unicode. JSON uses a versioned provider-neutral schema.
Exports contain no physical paths, secrets, credentials, or claim of Jira issue
creation. `IActionItemExporter` is the Application boundary.

### FR-014 — Backfill

An idempotent, restart-safe backfill creates independent actions for existing
minutes. It records enough source identity to prevent duplicates across partial
runs, deployment retry, Worker retry, or later minutes reads. Backfill does not
modify existing minutes JSON.

## 5. API contract summary

| Method | Endpoint | Success |
| --- | --- | --- |
| `POST` | `/api/meetings/audio` | 202 with job ID, mode, status, stage |
| `POST` | `/api/meetings/transcript` | 202 with job ID, mode, status, stage |
| `POST` | `/api/meetings/upload` | 202; deprecated FullMeeting alias |
| `GET` | `/api/meetings/{jobId}/status` | mode-aware status/progress/duration |
| `GET` | `/api/meetings/history?skip=0&take=20` | All Processing page |
| `GET` | `/api/dashboard/summary` | all-time aggregates and recent items |
| `GET` | `/api/meetings/minutes?skip=0&take=20` | completed-minutes page |
| `GET` | `/api/meetings/{jobId}/result` | immutable minutes and meeting details |
| `GET` | `/api/meetings/{jobId}/transcript/download` | clean text transcript |
| `GET` | `/api/meetings/{jobId}/minutes/download` | Markdown minutes |
| `POST` | `/api/meetings/{jobId}/retry` | 202 for eligible mode-aware retry |
| `GET` | `/api/actions` | filtered/paged independent actions |
| `POST` | `/api/actions` | 201 with created manual action |
| `GET` | `/api/actions/{actionId}` | one action |
| `PATCH` | `/api/actions/{actionId}` | updated action with new version |
| `DELETE` | `/api/actions/{actionId}` | 204 |
| `GET` | `/api/meetings/{jobId}/actions` | linked action context |
| `GET` | `/api/actions/export?format=csv|json` | export download |

Common responses: 400 invalid request, 404 unknown resource, 409 invalid state
or stale action version, 413 byte-size limit, 415 unsupported media type, and
safe 500-class responses for unexpected failure. Exact safe codes are stable
Application/API contracts and must be tested.

## 6. Persistence requirements

### MeetingJob additions

- `ProcessingMode` (required; existing rows migrate to `FullMeeting`).
- Approved source/input metadata and nullable mode-valid paths.
- Nullable `SourceAudioDurationSeconds`.

### MeetingTranscript additions

- Structured provider-neutral segments and formatting version/configuration, or
  an equivalent normalized child model selected in SAD/implementation.
- Existing transcript text/path remain available for result/download
  compatibility.

### Action

Independent table with the FR-010 fields. Meeting foreign key is nullable and
uses set-null/non-cascade behavior. Database constraints/indexes support
required lengths/status/source, stable generation identity, filters, overdue
queries, meeting context, and newest-first paging. Concurrency uses a storage
token encoded as an opaque DTO value.

Physical file references remain relative and never enter frontend contracts.

## 7. Non-functional requirements

### Performance

- Creation requests perform bounded validation/storage/persistence/enqueue only.
- Dashboard uses database aggregates and bounded recent queries.
- All lists are server-paged and capped at 100.
- Exports are bounded and do not load unlimited actions.
- Frontend polling remains non-overlapping.

### Reliability

- Accepted jobs survive browser closure.
- Retry/checkpoints are mode-aware and provider-minimal.
- Action seeding/backfill is idempotent.
- Action updates are optimistic-concurrency safe.
- Meeting retention never deletes independent actions.
- Existing safe retention, retry, readiness, and logging rules remain.

### Security and privacy

- Validate every audio/text upload and prevent traversal/root escape.
- Do not expose transcripts in logs, secrets, provider payloads, or local paths.
- Treat CSV cells as data and mitigate formula injection for values beginning
  with spreadsheet formula control characters.
- Keep the application restricted to a trusted local machine.

### Accessibility

- All routes, forms, filters, paging, confirmations, conflicts, progress, empty,
  and error states are keyboard operable and labelled.
- Use deliberate focus movement and a single polite live-status strategy.
- Automated scans fail on serious/critical violations; manual contrast and
  responsive checks remain required.

## 8. Verification requirements

- Unit tests cover mode rules, formatting, metrics, action validation/status,
  overdue UTC logic, concurrency mapping, and export escaping.
- API tests cover every endpoint, validation boundary, paging/filtering,
  compatibility alias, conflicts, deletion, and safe errors.
- PostgreSQL tests cover migrations, indexes/constraints, set-null retention,
  concurrency, seeding, and idempotent backfill.
- Worker tests prove each mode calls only required providers and reuses correct
  checkpoints through retries.
- Frontend tests cover routes/refresh, modes, dashboard, minutes library/detail,
  actions, delete confirmation, conflicts, filters, exports, accessibility, and
  non-overlapping polling.
- Local acceptance covers all modes, representative structured transcription,
  dashboard truth, immutable meetings, independent actions, export, Phase 2
  upgrade/backfill, and clean database creation.

Measurable package criteria and evidence live in
[`../BACKLOG.md`](../BACKLOG.md).

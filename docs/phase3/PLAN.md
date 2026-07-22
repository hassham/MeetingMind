# Phase 3 plan — MeetingMind AI

**Status:** Approved by product owner  
**Prepared:** 2026-07-22  
**Approved:** 2026-07-22  
**Theme:** Flexible meeting inputs, useful dashboards, readable transcripts,
and trackable outcomes.

## 1. Outcome

Phase 3 turns the current single audio-to-minutes workflow into three explicit
workflows and makes completed work easier to find and act on:

1. Audio to transcript only.
2. Audio to transcript and meeting minutes.
3. Uploaded transcript to meeting minutes.

The application opens on a dashboard, provides a dedicated meeting-minutes
library and detail screen, improves transcript readability, and promotes
generated action items into independently trackable records that can be
exported to external systems later.

## 2. Scope boundaries and assumptions

In scope:

- A dashboard landing page with useful aggregate and recent-activity metrics.
- An explicit processing mode on every job.
- Transcript-file upload for minutes generation.
- Readable, timestamp-aware transcripts from local Whisper output.
- A completed-minutes library and meeting detail screen.
- Extraction, editing, status tracking, and portable export of action items.
- Database migrations, API contracts, automated tests, accessibility, and
  documentation for the above.

Out of scope unless separately approved:

- Authentication, ownership, multi-tenancy, cloud or public deployment.
- Direct Jira or other third-party API integration. Phase 3 prepares a stable
  export/integration boundary and ships CSV/JSON export; provider-specific
  connectors are a later package.
- Speaker diarization or speaker recognition. The UI must not imply that
  paragraph breaks identify speakers.
- Editing generated minutes, semantic search, notifications, live recording,
  and live transcription.
- Reprocessing an existing completed job into a different mode. A new request
  creates a new job in Phase 3.

The trusted-local deployment boundary remains unchanged.

## 3. Product experience

### 3.1 Navigation

Use five primary destinations:

- **Dashboard** — landing page and operational summary.
- **New processing job** — select a workflow and upload its input.
- **Meeting minutes** — completed minutes only, with meeting details.
- **Actions** — independent action-management area for generated and manually
  created actions.
- **All processing** — all jobs, including transcript-only, active, and failed
  jobs; this evolves the current history view.

Deep links must survive refresh. Introduce client-side routing rather than
keeping all states in the current single screen.

### 3.2 Dashboard metrics

The first dashboard version should show:

- Total jobs created (the requested total process count).
- Counts by processing mode.
- Completed, active, and failed counts.
- Success rate: completed divided by terminal jobs; display `—` when no
  terminal jobs exist.
- Total audio processed and average completed processing duration when duration
  metadata is available.
- Total transcripts and total minutes generated.
- Open, in-progress, completed, and overdue action-item counts.
- Recent jobs and recently generated minutes.

Metrics are database aggregates returned by a dedicated summary endpoint, not
computed by downloading every history page. The dashboard states the selected
time basis and uses all-time metrics in the first release; date filters are a
later enhancement.

### 3.3 New processing job

Present three clear choices before upload:

| Mode | Input | Pipeline | Result |
| --- | --- | --- | --- |
| Transcript only | Supported audio | validate → transcode → transcribe → save | readable transcript |
| Full meeting | Supported audio | validate → transcode → transcribe → generate minutes → save | transcript and minutes |
| Minutes from transcript | `.txt` or `.md` transcript | validate text → normalize → generate minutes → save | uploaded transcript and minutes |

All three remain asynchronous: the API validates, stores, creates a job,
enqueues it, and returns `202 Accepted`. Transcript upload must validate safe
filename, extension, MIME type, non-empty UTF-8 text, byte size, and the
existing configured maximum transcript character count. It must reject binary,
invalidly encoded, or whitespace-only files.

### 3.4 Meeting-minutes library and detail

The library shows only jobs with generated minutes. It supports server-side
pagination, newest-first ordering, and an empty state. Each card/row shows the
minutes title, meeting/source name, created/completed date, and source type.

The detail screen shows meeting details, all structured minutes sections,
download actions, and links to actions that originated from that meeting.
Transcript content is available when relevant but is not the primary view.
The meeting is a finished record: action status changes never change a meeting
status, progress, completion date, or generated-minutes content.

### 3.5 Action-item tracking

Actions are a standalone domain, not a subordinate meeting workflow. When
minutes are first saved, copy each generated action item into a first-class
`Action` record. The generated minutes JSON and completed meeting remain
immutable snapshots; action changes do not silently rewrite either one.

Users can also create actions manually. A manual action may optionally link to
an existing meeting for context, but it does not need a meeting. Generated
actions retain their originating meeting as provenance. Removing an optional
link must not delete the action, and deleting/retaining meeting data must follow
an explicit policy that preserves independent actions.

Each tracked item has:

- Stable ID and optional meeting ID.
- Description, assignee, and due date.
- Status: `Open`, `InProgress`, `Blocked`, `Completed`, or `Cancelled`.
- Optional notes.
- Sort order, created/updated timestamps, and optional completion timestamp.
- Source marker: `Generated` or `Manual`.

Any action may be permanently deleted after explicit UI confirmation. Deleting
an action does not modify its meeting or the immutable generated-minutes
snapshot.

Users can create actions and edit the description, assignee, due date, status,
notes, and optional meeting link. Updates use optimistic concurrency (for
example, a version token) so two stale edits cannot overwrite each other
silently. Overdue means an incomplete item with a due date earlier than the
application's current local date.

The Actions area supports server-side paging and filtering by status, assignee,
due/overdue state, source, and linked meeting. Action counts belong to the
Actions area and dashboard; they are not a lifecycle or progress measure for a
meeting.

Ship downloads for selected/all action items as CSV and JSON using a documented,
provider-neutral schema. Define `IActionItemExporter` in Application so a Jira
adapter can be added later. Do not store Jira credentials or claim that export
has created a Jira issue.

## 4. Transcription readability design

The current Whisper implementation concatenates segment text and discards the
segment boundaries. Replace that behavior with a structured transcription
result behind `ITranscriptionService`:

- Preserve each Whisper segment's start time, end time, and text.
- Normalize whitespace without changing words.
- Start a paragraph after a configurable silence gap, after sentence-ending
  punctuation when the paragraph is sufficiently long, or at a safe maximum
  paragraph length.
- Keep short adjacent segments together to avoid one-line fragments.
- Render a readable plain-text transcript with paragraph breaks and optional
  timestamps at paragraph starts.
- Persist structured segments (JSON or normalized child records) as the source
  used to render text, so formatting can be regenerated without retranscribing.
- Retain deterministic behavior and unit-test the boundary rules.

Recommended initial defaults are a 1.5-second silence gap, a 300-character
preferred paragraph size, and a 700-character hard paragraph bound. These must
be configuration values validated at startup and tuned with representative
audio during acceptance testing.

This improves layout but not speaker attribution. A future diarization feature
could add speaker turns to the same structured segment model. An optional
LLM-based transcript-polishing feature should also be separate and opt-in,
because it adds cost and can alter meaning.

## 5. Architecture and data changes

### 5.1 Domain and persistence

- Add `MeetingProcessingMode`: `TranscriptOnly`, `FullMeeting`, and
  `MinutesFromTranscript`.
- Add mode and input/source metadata to `MeetingJob`. Audio-only paths become
  nullable where the mode does not require audio; enforce valid combinations
  in Domain/Application rules and database constraints where practical.
- Add structured transcript segments and formatting metadata without exposing
  physical storage paths.
- Add first-class `Action` records with an optional meeting provenance link.
  They have their own lifecycle and repository boundary. Use restrictive or
  link-nulling delete behavior rather than cascading meeting deletion into
  actions; the approved retention policy must be explicitly tested.
- Add indexes supporting dashboard aggregates, completed-minutes paging, action
  status/due-date queries, and newest-first ordering.

Existing jobs must migrate to `FullMeeting`. Existing generated minutes need a
documented one-time, idempotent action-item backfill, or the product owner must
explicitly accept that tracking begins only for newly generated minutes. The
recommended choice is the idempotent backfill.

### 5.2 Application and Worker

- Keep one job abstraction and dispatch the Worker pipeline by processing mode.
- Skip minutes generation for `TranscriptOnly`.
- Skip FFmpeg and Whisper for `MinutesFromTranscript`, using the validated
  stored transcript as the durable checkpoint.
- Make retry/checkpoint logic mode-aware so it never expects irrelevant files.
- Seed independent actions atomically/idempotently after minutes persistence.
- Add standalone action create, list, update, link/unlink, and export use cases.
- Add dashboard, minutes-query, and action-item command/query services in
  Application; controllers remain thin.
- Keep all provider and export implementations behind Application interfaces.

Progress must be mode-specific and monotonic. Do not show a skipped stage as if
it ran. Add an `ImportingTranscript` stage only if it provides meaningful user
feedback; otherwise validation/storage occurs before enqueue and the Worker can
start at `GeneratingMinutes`.

### 5.3 Proposed API surface

Exact DTO names may change during P3-01, but the behavior should converge on:

| Method | Endpoint | Purpose |
| --- | --- | --- |
| `GET` | `/api/dashboard/summary` | Aggregate metrics and recent items |
| `POST` | `/api/meetings/audio` | Create transcript-only or full audio job; mode is explicit |
| `POST` | `/api/meetings/transcript` | Create minutes-from-transcript job |
| `GET` | `/api/meetings/minutes` | Paginated completed-minutes library |
| `GET` | `/api/meetings/{jobId}/result` | Meeting details and structured result |
| `GET` | `/api/actions` | Paginated/filterable independent action list |
| `POST` | `/api/actions` | Create a manual action with an optional meeting link |
| `GET` | `/api/actions/{actionId}` | Get one action and optional meeting provenance |
| `PATCH` | `/api/actions/{actionId}` | Update fields or meeting link with concurrency token |
| `GET` | `/api/actions/export?format=csv|json` | Export selected or filtered actions |
| `GET` | `/api/meetings/{jobId}/actions` | Find actions linked to a meeting; read-only meeting context |

Preserve the existing `/api/meetings/upload` contract as a compatibility alias
for `FullMeeting` during Phase 3, mark it deprecated in Swagger, and remove it
only in a future breaking release.

## 6. Ordered work packages

Status legend: `TODO` | `IN PROGRESS` | `BLOCKED` | `PARTIAL` | `DONE`

### P3-01 — Approve requirements, UX, and contracts

- [ ] Reconcile PRD, FSD, SAD, API contract, terminology, and navigation.
- [ ] Approve processing modes, dashboard formulas, transcript-upload limits,
  action statuses, and compatibility behavior.
- [ ] Produce low-fidelity states for dashboard, workflow selection, minutes
  library/detail, action editing, loading, empty, and failure states.
- [ ] Record migration/backfill and retention behavior for new records.

Acceptance: documents contain no conflicting Phase 2/3 claims; every new metric
and status has one definition; the product owner approves the open decisions in
section 8 before schema implementation begins.

### P3-02 — Introduce mode-aware jobs and safe migrations

- [ ] Add the processing-mode model, nullable/source rules, EF migration, and
  indexes.
- [ ] Migrate existing jobs to `FullMeeting` without changing their results.
- [ ] Update history/status contracts and mode-specific retry/checkpoint rules.
- [ ] Add Domain, Application, repository, migration, and API regression tests.

Acceptance: all old jobs remain readable/retryable; invalid mode/input
combinations are rejected; the current full pipeline still passes end to end.

### P3-03 — Deliver all three creation workflows

- [ ] Add explicit audio mode selection and transcript-file upload.
- [ ] Dispatch mode-specific Worker pipelines with accurate stages/progress.
- [ ] Return transcript-only completion without attempting an OpenAI call.
- [ ] Generate minutes from uploaded text without FFmpeg or Whisper calls.
- [ ] Add validation, cancellation, retry, checkpoint, and UI tests per mode.

Acceptance: provider test doubles prove each mode calls only its required
dependencies; every accepted request returns `202`; refresh/closure does not
interrupt work; invalid transcript files fail before enqueue.

### P3-04 — Produce readable structured transcripts

- [ ] Preserve Whisper segment timestamps and add deterministic paragraphing.
- [ ] Persist structured segments and render readable transcript text/download.
- [ ] Make paragraph rules configurable and startup-validated.
- [ ] Verify punctuation, silence-gap, length-boundary, empty-segment, and
  existing-checkpoint behavior with tests and representative audio.

Acceptance: transcripts contain stable paragraph breaks, no recognized words
are lost or invented by formatting, hard paragraph limits hold, and retry does
not require retranscription when valid segments exist.

### P3-05 — Build dashboard and application navigation

- [ ] Add aggregate repository queries and dashboard Application/API contracts.
- [ ] Add client-side routes and make Dashboard the initial route.
- [ ] Render metrics, recent jobs/minutes, empty/error/loading states, and links
  to relevant filtered views.
- [ ] Add query, contract, responsive, accessibility, and frontend tests.

Acceptance: metrics match seeded database truth including zero-data and mixed
status/mode cases; dashboard loading is bounded and does not fetch all history;
keyboard and automated accessibility checks pass.

### P3-06 — Deliver the meeting-minutes library and detail screen

- [ ] Add paginated completed-minutes query and meeting-detail metadata.
- [ ] Build the minutes-only library, deep-linked detail page, structured
  sections, transcript access, and downloads.
- [ ] Keep active/failed/transcript-only work in All Processing.
- [ ] Add pagination, refresh, not-found, and accessibility tests.

Acceptance: only records with minutes appear; direct links survive refresh;
meeting details and all eight minutes sections match persisted data.

### P3-07 — Deliver independent action management and export

- [ ] Add the independent Action aggregate, optional meeting provenance,
  constraints, indexes, and idempotent creation from generated minutes.
- [ ] Implement the approved legacy backfill strategy.
- [ ] Add create/list/get/update/link/unlink services and API with validation
  and optimistic concurrency.
- [ ] Add dashboard action metrics without deriving a meeting workflow status.
- [ ] Build the standalone Actions screen, accessible create/edit/status UI,
  meeting-context links, filters, and CSV/JSON export.
- [ ] Add unit, persistence, contract, frontend, concurrency, and export tests.

Acceptance: generated items are created once even after retry; tracking edits
do not mutate the meeting or generated-minutes snapshot; manual actions work
with or without a meeting; linking/unlinking does not change meeting state;
overdue/status formulas are consistent; stale updates return a conflict;
exports round-trip special characters and contain no local paths.

### P3-08 — Phase 3 release verification and documentation

- [ ] Run backend build/tests and frontend lint/build/tests.
- [ ] Execute an end-to-end matrix for every mode, readable transcription,
  retry, dashboard totals, minutes navigation, action updates, and exports.
- [ ] Verify migration and backfill on a copy of representative Phase 2 data.
- [ ] Update setup, user guidance, API documentation, PRD, FSD, SAD, AGENTS.md,
  and archive the completed Phase 3 backlog.

Acceptance: all automated gates pass; recorded evidence covers every Phase 3
workflow and migration; no item remains incomplete; durable docs match code.

## 7. Release gate and success measures

Phase 3 is complete only when:

- All three workflows complete asynchronously with correct dependency usage.
- Dashboard figures agree with persisted test and acceptance data.
- Representative Whisper output renders readable paragraphs without changing
  recognized words.
- Completed minutes are independently discoverable and deep-linkable.
- Generated action items can be tracked and exported without changing the
  original minutes snapshot.
- Existing Phase 2 data and the legacy full-audio API behavior remain usable.
- Backend and frontend verification suites, accessibility checks, and the local
  end-to-end matrix pass.

Suggested product measures after release:

- Completion and failure rate by processing mode.
- Median time to transcript and median time to minutes (derived from existing
  stage/job timing where available).
- Number and source mix of actively managed actions.
- Action-item completion and overdue rates.
- Transcript formatting defect reports on the acceptance audio set.

These are local aggregate measures; Phase 3 does not add external telemetry.

## 8. Approved decisions

1. **Legacy action items:** idempotently backfill actions from existing minutes.
2. **Transcript timestamps:** show paragraph-start timestamps in the viewer and
   provide a clean text download.
3. **Transcript formats:** accept `.txt` and `.md` only. PDF/DOCX parsing is
   deferred.
4. **Meeting details:** use generated title, source filename, source type, and
   processing timestamps. New editable meeting metadata is deferred.
5. **Action export:** deliver CSV and JSON; direct Jira integration is deferred.
6. **Dashboard time range:** use all-time metrics; date filters are deferred.
7. **Action independence:** generated and manual actions have an independent
   lifecycle. Meeting links are optional provenance and never update meeting
   workflow state or generated minutes.
8. **Meeting retention:** preserve linked actions, null the live meeting link,
   and retain meeting-title/source-filename provenance snapshots.
9. **Action lifecycle:** permit hard deletion after confirmation and movement
   between all statuses. Completed timestamps exist only while Completed.
10. **Action validation:** description 2,000 characters, assignee 200, notes
    10,000; due date is an optional UTC calendar date.
11. **Dashboard formula:** success is Completed divided by Completed plus
    Failed. Cancelled and active jobs are excluded; cancelled remains visible
    separately. Persist and sum source-audio duration for audio jobs only.
12. **Paging:** 20 processing/meeting records, 25 actions, API maximum 100, and
    five recent jobs plus five recent minutes.
13. **Concurrency:** PATCH requires an opaque action version and returns HTTP
    409 for a stale value.

## 9. Recommended sequence

Execute packages in order. P3-02 establishes the data contract; P3-03 proves
the workflows; P3-04 improves their core artifact; P3-05 and P3-06 build read
experiences on stable queries; P3-07 adds mutable tracking after the immutable
minutes behavior is stable; P3-08 closes the release. Verify each package
before starting the next, consistent with the repository workflow.

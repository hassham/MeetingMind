# Product Requirements Document

**Product:** MeetingMind AI

**Version:** 3.0

**Status:** Approved for Phase 3

**Product owner:** Hasham Ahmad

**Approved:** 2026-07-22

## 1. Vision

MeetingMind AI converts meeting recordings or existing transcripts into useful
records: readable transcripts, structured meeting minutes, and independently
trackable actions. The long-term vision is a searchable meeting-intelligence
platform. Phase 3 remains a trusted local application.

## 2. Current product state

Phase 1 delivered asynchronous audio-to-transcript-and-minutes processing.
Phase 2 completed local production hardening: portable configuration, automated
verification, bounded retries, long-transcript aggregation, duration reporting,
safe retention, readiness checks, accessibility, and operational guidance.

Phase 3 is the active delivery cycle. No Phase 3 production feature is complete
until its backlog package passes its acceptance gate.

## 3. Phase 3 goals

### User goals

- Start from a dashboard that summarizes processing activity and outcomes.
- Choose audio transcription only or full audio-to-minutes processing.
- Upload an existing `.txt` or `.md` transcript to generate minutes.
- Read transcripts with meaningful paragraphs and paragraph timestamps.
- Browse completed meeting minutes separately from processing history.
- Turn generated action items into independent actions.
- Create, manage, link, unlink, delete, filter, and export independent actions.
- Trace an action to a meeting when provenance exists without reopening or
  changing the completed meeting workflow.

### Technical goals

- Preserve asynchronous API/Worker separation for every processing mode.
- Evolve one job model with explicit, validated processing modes.
- Preserve provider-neutral transcript segments and deterministic formatting.
- Keep completed meetings/minutes immutable while actions have an independent
  lifecycle and repository boundary.
- Provide bounded aggregate, paging, filtering, and optimistic-concurrency
  contracts.
- Upgrade Phase 2 data safely and backfill generated actions idempotently.

## 4. Target users

The trusted-local release serves project managers, scrum masters, engineering
teams, business analysts, product owners, consultants, and small businesses
using the application on a controlled machine.

Enterprise, regulated, shared, and public use requires a later security and
tenancy cycle.

## 5. Phase 3 scope

### Processing choices

- `TranscriptOnly`: supported audio to a readable transcript.
- `FullMeeting`: supported audio to a readable transcript and structured
  minutes.
- `MinutesFromTranscript`: validated UTF-8 `.txt` or `.md` transcript to
  structured minutes.
- The existing full-audio upload endpoint remains a deprecated compatibility
  alias during Phase 3.

### Dashboard and navigation

- Dashboard is the initial screen.
- Primary destinations are Dashboard, New Processing Job, Meeting Minutes,
  Actions, and All Processing.
- Deep links survive browser refresh.
- Dashboard uses bounded all-time database aggregates and recent-item queries.

### Readable transcripts

- Preserve Whisper segment start/end times and recognized text.
- Render deterministic paragraphs using configurable silence, punctuation, and
  length rules without inventing or removing recognized words.
- Show paragraph-start timestamps in the viewer.
- Provide a clean transcript download without timestamps.
- Do not label formatting boundaries as speakers.

### Meeting minutes

- Provide a paginated library containing only jobs with persisted minutes.
- Provide a deep-linked detail view containing meeting/source details, all
  eight structured minutes sections, transcript access, downloads, and links to
  independent actions with that meeting as provenance.
- Completed meeting state and generated minutes remain immutable when actions
  change.

### Independent actions

- Seed actions idempotently from generated minutes and backfill existing
  generated minutes.
- Permit manual actions with or without a meeting link.
- Support `Open`, `InProgress`, `Blocked`, `Completed`, and `Cancelled` status.
- Support description, optional assignee, optional due date, optional notes,
  source, timestamps, optional meeting link, provenance snapshots, and an
  opaque concurrency version.
- Permit movement between statuses and hard deletion of any action after an
  explicit confirmation.
- Support server-side paging/filtering and CSV/JSON export.
- Action changes never modify a meeting, processing job, or generated-minutes
  snapshot.

## 6. Out of scope

- Authentication, authorization, user ownership, and multi-tenancy.
- Public/shared-network or cloud deployment.
- Direct Jira or other third-party connectors.
- Speaker diarization or recognition.
- LLM-based transcript polishing or correction.
- PDF/DOCX transcript import.
- Editing generated minutes.
- Semantic search, notifications, live recording, live transcription, billing,
  mobile applications, or translation.
- Reprocessing a completed job into another mode.
- Dashboard date filters or external telemetry.

## 7. User stories

### Review the dashboard

As a user, I want an accurate summary of all-time processing, recent work, and
action workload so that I can decide where to go next.

### Choose a processing result

As a user, I want to choose transcript-only or full-meeting processing before I
upload audio so that the system performs only the work I need.

### Generate minutes from a transcript

As a user, I want to upload an existing text or Markdown transcript so that I
can receive minutes without audio transcoding or transcription.

### Read a transcript

As a user, I want paragraphs and timestamps so that a transcript is navigable
without the formatter changing the recognized words.

### Browse meeting records

As a user, I want a minutes-only library and stable detail links so that I can
review completed meeting records independently of processing history.

### Manage independent actions

As a user, I want generated actions to become standalone work records and to
create additional actions so that work continues after the meeting is finished.

### Retain optional meeting context

As a user, I want an action to link optionally to a meeting so that I can trace
its origin without tying its lifecycle to that meeting.

### Export actions

As a user, I want selected or filtered actions in CSV or JSON so that I can
transfer them to another system without MeetingMind claiming an integration.

## 8. Product rules and measures

### Dashboard definitions

- Total jobs is the count of all persisted jobs.
- Active count includes `Queued` and `Processing`.
- Terminal counts show `Completed`, `Failed`, and `Cancelled` separately.
- Success rate is `Completed / (Completed + Failed) * 100`.
- `Cancelled`, `Queued`, and `Processing` are excluded from the success-rate
  denominator. When the denominator is zero, display `—`.
- Total audio processed is the sum of persisted source-audio duration for audio
  jobs; transcript-upload jobs are excluded.
- Average completed processing duration uses completed jobs with valid duration
  data.
- Transcript and minutes totals count persisted artifacts.
- Action counts use the documented independent action statuses and UTC overdue
  rule.
- Dashboard returns five recent jobs and five recent minutes, newest first.

### Action definitions

- Due date is an optional UTC calendar date without a time.
- An action is overdue when its due date is before the current UTC date and its
  status is not `Completed` or `Cancelled`.
- Deleting an action never edits its meeting or the generated-minutes JSON.
- If retention deletes a linked meeting, the action remains, its live meeting
  ID becomes null, and its meeting-title/source-filename provenance snapshot
  remains.

### Pagination

- All Processing and Meeting Minutes use 20 items per page by default.
- Actions uses 25 items per page by default.
- API list requests permit at most 100 items.

## 9. Phase 3 success measures

Phase 3 succeeds when:

- All three modes complete asynchronously and invoke only required providers.
- Existing Phase 2 jobs and legacy upload clients remain usable.
- Dashboard values agree with persisted acceptance data.
- Representative transcripts have stable paragraphs/timestamps without word
  changes or speaker claims.
- Minutes are independently discoverable, immutable, and deep-linkable.
- Generated and manual actions work independently with optional meeting links,
  concurrency protection, filters, deletion confirmation, and safe exports.
- Phase 2 upgrade/backfill and clean-database migrations both pass.
- Backend/frontend tests, accessibility checks, and documented local
  end-to-end scenarios pass.
- Durable documents match released behavior.

Detailed work-package gates and evidence are maintained in
[`../BACKLOG.md`](../BACKLOG.md).

## 10. Risks and mitigations

| Risk | Mitigation |
| --- | --- |
| Processing-mode branching duplicates workflow logic | Use one mode-aware Worker orchestration path and Application contracts. |
| Transcript import bypasses media validation | Apply dedicated text extension, MIME, byte, UTF-8, binary, whitespace, and character validation. |
| Formatting changes recognized meaning | Preserve structured segments and use deterministic formatting only. |
| Paragraphs are mistaken for speakers | State explicitly that diarization is not present. |
| Generated actions duplicate on retry/backfill | Use stable idempotency rules and database constraints. |
| Action edit overwrites another edit | Require opaque version and return HTTP 409 for stale updates. |
| Action deletion appears to alter minutes | Keep minutes immutable and require explicit action-delete confirmation. |
| Meeting cleanup destroys independent work | Null the relationship and retain provenance snapshots before meeting removal. |
| UTC overdue date surprises local users | Label due dates consistently and use the same UTC rule in API and UI. |
| Dashboard queries grow with data | Use bounded aggregate/recent queries and supporting indexes. |
| Local endpoints are exposed remotely | Retain and document the trusted-local boundary. |

## 11. Roadmap terminology

- **Phase 1:** completed local MVP.
- **Phase 2:** completed local production-readiness and hardening cycle.
- **Phase 3:** active flexible-input, dashboard, transcript-readability,
  minutes-library, and independent-actions cycle.
- **Future cycles:** authentication, multi-user functionality, cloud deployment,
  integrations, diarization, and meeting intelligence.

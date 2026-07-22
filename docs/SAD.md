# Software Architecture Document

**Product:** MeetingMind AI

**Version:** 3.0

**Status:** Active Phase 3 architecture

**Architecture:** Clean Architecture modular monolith with separate API and
Worker entry points

**Deployment:** Trusted local environment

**Approved:** 2026-07-22

## 1. Architecture goals

- Keep every long-running processing mode outside HTTP requests.
- Preserve one durable job model while invoking only mode-required providers.
- Store provider-neutral transcript segments and render readable text
  deterministically.
- Treat completed meetings/minutes as immutable records.
- Treat actions as an independent aggregate with optional meeting provenance.
- Support bounded dashboard/list/export queries and safe concurrency.
- Upgrade Phase 2 data without loss and retain infrastructure replacement
  boundaries.

## 2. System context

```text
User
  -> React frontend and client-side routes
  -> MeetingMind.Api
       -> PostgreSQL (MeetingMind + Hangfire)
       -> local storage through IFileStorageService
       -> Hangfire enqueue through IBackgroundJobService

MeetingMind.Worker
  -> Hangfire server
  -> mode-aware MeetingProcessingJob
       -> FFmpeg through IAudioProcessingService (audio modes)
       -> Whisper.net through ITranscriptionService (audio modes)
       -> deterministic transcript formatter
       -> IMeetingMinutesService (minutes modes)
       -> idempotent independent-action seeding (minutes modes)
       -> repositories and IFileStorageService
```

The API is a Hangfire client, not a server. The Worker is the only processing
executor. Dashboard, minutes, and actions are Application use cases; controllers
do not query EF Core directly.

## 3. Dependency rules

```text
Domain                  -> no project dependencies
Application             -> Domain, Shared
Infrastructure          -> Application, Domain
Api                     -> Application, Infrastructure, Shared
Worker                  -> Application, Infrastructure, Shared
```

- Domain contains entities/enums/invariants and no EF Core, Hangfire, FFmpeg,
  Whisper, OpenAI, filesystem, or HTTP dependencies.
- Application owns use cases, repository/provider/export contracts, DTO-like
  results, deterministic formatting/orchestration, and business formulas.
- Infrastructure implements EF Core, storage, FFmpeg/Whisper/OpenAI, Hangfire
  client, and export serialization boundaries.
- API and Worker are thin entry points.
- No MediatR/CQRS package is introduced by Phase 3.

## 4. Component responsibilities

### 4.1 Frontend

- Route Dashboard, New Processing Job, Meeting Minutes, Meeting Detail,
  Actions, and All Processing.
- Select a processing mode and submit the matching input.
- Poll active jobs without overlap.
- Show timestamped readable transcripts and clean downloads.
- Browse immutable meeting minutes.
- Create/filter/edit/link/unlink/delete/export independent actions.
- Require explicit confirmation before action deletion.
- Send the latest opaque action version and handle HTTP 409 conflicts.
- Keep action state visually and semantically separate from meeting state.

### 4.2 API

- Validate HTTP shape and delegate to Application services.
- Return 202 after bounded creation/enqueue work.
- Expose mode-aware processing/status/history/results/download/retry contracts.
- Expose bounded dashboard/minutes/action queries and action commands/exports.
- Preserve the deprecated Phase 2 full-upload alias.
- Expose existing local health, Swagger, and Hangfire dashboard endpoints.

### 4.3 Worker

- Host Hangfire and load persisted jobs.
- Validate the mode/input invariant and the best durable checkpoint.
- Dispatch only required mode operations.
- Persist structured transcription and formatted text.
- Generate/persist immutable minutes when required.
- Seed independent generated actions idempotently after minutes persistence.
- Maintain existing sanitized failure, retry, checkpoint, logging, and timing
  behavior.

### 4.4 PostgreSQL

- Persist MeetingMind and Hangfire data.
- Enforce relational integrity, idempotency, concurrency, paging/filter indexes,
  and non-cascading optional action provenance.
- Execute all changes through EF Core migrations.

### 4.5 Local storage

All artifacts remain behind `IFileStorageService`, stored by safe relative path:

```text
Storage/
  Audio/Original/
  Audio/Processed/
  Transcript/
  Minutes/
```

Transcript imports use `Transcript/`; structured segment persistence may use a
related safe artifact or database representation chosen during P3-02/P3-04.
Physical paths never enter API responses.

## 5. Domain model

### 5.1 MeetingJob

Add required `MeetingProcessingMode`:

- `TranscriptOnly`
- `FullMeeting`
- `MinutesFromTranscript`

Existing jobs migrate to `FullMeeting`. Mode controls valid source/path fields,
pipeline stages, retry checkpoints, and result availability. Add nullable
non-negative `SourceAudioDurationSeconds`; it is null for transcript imports
and until duration discovery for audio.

The existing status/stage/timestamp/retry model remains. Completed meeting jobs
do not change because linked actions change.

### 5.2 MeetingTranscript

Evolve transcription from one concatenated string into:

```text
TranscriptionResult
  Segments[]
    Start
    End
    Text
  FormattingVersion
  FormattedText
```

Application contracts use provider-neutral time values. Infrastructure maps
Whisper segments. Persistence may serialize bounded segments as JSON on the
one-to-one transcript record or use normalized child rows; P3-02/P3-04 must
select after measuring migration/query needs. The API does not expose provider
types or storage paths.

The formatted text is a reproducible artifact. Structured segments are the
durable source, allowing rerendering without Whisper.

### 5.3 MeetingMinutes

Remains one immutable generated snapshot per job with all eight sections.
Generation retry may upsert while the meeting job is processing; after
successful completion, independent action changes never modify it.

### 5.4 Action aggregate

Use `Action` as a standalone Domain entity and repository boundary, distinct
from the existing generation DTO `MeetingActionItem`.

Conceptual fields:

```text
Action
  Id
  Description (required, max 2000)
  Assignee (nullable, max 200)
  Notes (nullable, max 10000)
  DueDate (nullable calendar date)
  Status (Open | InProgress | Blocked | Completed | Cancelled)
  Source (Generated | Manual)
  MeetingJobId (nullable provenance relationship)
  ProvenanceMeetingTitle (nullable snapshot)
  ProvenanceSourceFileName (nullable snapshot)
  GeneratedSourceKey (nullable, unique when Generated)
  CreatedAt
  UpdatedAt
  CompletedAt (nullable)
  ConcurrencyToken
```

Rules:

- Manual actions may exist without a meeting.
- Generated actions initially link to their source meeting.
- Any status transition is allowed.
- Entering Completed sets `CompletedAt`; reopening clears it.
- Hard deletion is allowed after frontend confirmation and affects no meeting
  or minutes record.
- Linking validates the meeting and refreshes provenance snapshots.
- Unlinking preserves snapshots.
- Meeting retention nulls the FK and preserves snapshots before deletion.

### 5.5 Relationships

```text
MeetingJob 1 -> 0..1 MeetingTranscript   (owned meeting artifact)
MeetingJob 1 -> 0..1 MeetingMinutes      (owned immutable artifact)
MeetingJob 1 -> 0..* Action              (optional provenance, not ownership)
```

Transcript/minutes may retain existing cascading behavior with meeting
retention. Action must use non-cascade behavior. The retention transaction
updates linked actions before deleting the meeting.

## 6. Application architecture

### 6.1 Creation services

- Audio creation validates mode and existing audio rules, stores input, creates
  job, and enqueues.
- Transcript creation validates byte limit, filename/extension/MIME, binary,
  UTF-8, whitespace, and character limit before storage/job/enqueue.
- The compatibility upload service delegates to FullMeeting creation.

### 6.2 Mode-aware processing

```text
Load job and validate mode
  TranscriptOnly
    -> audio checkpoint -> FFmpeg -> Whisper segments -> format/save -> Complete
  FullMeeting
    -> audio checkpoint -> FFmpeg -> Whisper segments -> format/save
    -> generate/save minutes -> seed actions -> Complete
  MinutesFromTranscript
    -> transcript checkpoint -> generate/save minutes -> seed actions -> Complete
```

One orchestration handler branches at explicit mode boundaries. Shared status,
retry, error, logging, persistence, and completion behavior is not duplicated.
Automatic retries use the existing configured Hangfire policy.

### 6.3 Transcript formatting

Application owns a deterministic `ITranscriptFormatter` or equivalent pure
service. It consumes provider-neutral segments plus validated options and emits
paragraphs/timestamps/formatted text. Defaults:

- Silence gap: 1.5 seconds.
- Preferred paragraph length: 300 characters.
- Hard paragraph length: 700 characters.

Formatting preserves normalized word order/content. It is not an AI provider
and performs no diarization.

### 6.4 Dashboard and queries

Application owns the formulas. Repository projections execute aggregate and
bounded recent queries in PostgreSQL. Success denominator is Completed + Failed
only. Cancelled/active counts remain separate. Total audio uses known persisted
audio duration only.

List defaults/caps:

- Processing and minutes: 20.
- Actions: 25.
- Maximum: 100.
- Dashboard recent jobs/minutes: five each.

### 6.5 Action commands and concurrency

Application owns create/update/link/unlink/delete validation, UTC overdue and
completion-time rules, and meeting immutability. `IActionRepository` exposes
bounded query/command operations.

Infrastructure concurrency token is encoded into an opaque API `version`.
PATCH supplies the expected version. A conditional update mismatch maps to a
safe Application conflict and HTTP 409; it never applies a partial update.

The API delete endpoint cannot technically prove that a UI dialog occurred;
the frontend owns confirmation. The endpoint is nevertheless explicit and
separate from update.

### 6.6 Action seeding and backfill

Generated action identity uses a deterministic source key derived from meeting
ID plus stable generated-item position/identity. A unique constraint and
idempotent insert/upsert prevent duplication across Worker retry and backfill.
Backfill is restart-safe, bounded, and leaves minutes JSON untouched.

### 6.7 Export

`IActionItemExporter` is an Application interface. Implementations serialize a
bounded selected/filtered projection to CSV or versioned JSON. CSV neutralizes
spreadsheet formula injection and quotes special data. No Jira SDK, credential,
or provider status enters the core contract.

## 7. API architecture

New surface:

| Method | Route | Application responsibility |
| --- | --- | --- |
| POST | `/api/meetings/audio` | create audio job |
| POST | `/api/meetings/transcript` | create transcript job |
| GET | `/api/dashboard/summary` | aggregate summary |
| GET | `/api/meetings/minutes` | paged immutable meeting records |
| GET/POST | `/api/actions` | list/create actions |
| GET/PATCH/DELETE | `/api/actions/{actionId}` | action detail/update/delete |
| GET | `/api/meetings/{jobId}/actions` | provenance navigation |
| GET | `/api/actions/export` | bounded export |

Existing status/history/result/download/retry routes remain and become
mode-aware. `/api/meetings/upload` remains a deprecated FullMeeting alias.

Controllers map HTTP only. Validation/business conflicts originate from stable
Application results/exceptions and map to tested safe codes.

## 8. Frontend architecture

Introduce client-side routing with stable URLs. Route-level views compose
shared status, paging, filters, result sections, and action forms. Existing
single non-overlapping polling remains limited to active selected jobs.

Action edit state retains the last version received. HTTP 409 presents an
actionable reload/reapply flow and does not overwrite local edits silently.
Delete uses a labelled confirmation dialog with safe focus return.

Meetings show provenance navigation only; action status badges/progress are not
used as meeting status.

The complete low-fidelity contract is in [`phase3/UX.md`](phase3/UX.md).

## 9. Query and index strategy

Add indexes supporting:

- MeetingJob mode/status and newest-first dashboard/history queries.
- Known audio duration aggregation where practical.
- Minutes existence/completion newest-first paging.
- Action status, due date, source, nullable meeting ID, created order, and
  generated source key.

Every query remains server-side and bounded. Exact composite indexes follow
measured generated SQL and integration tests; avoid speculative indexes outside
documented query shapes.

## 10. Retention and migration

### Phase 2 upgrade

- Add mode with deterministic existing-row default/backfill `FullMeeting`.
- Preserve all existing job/artifact/status/retry/timing data.
- Add audio duration nullable so existing rows need no invented value.
- Create Action table and backfill generated actions idempotently.
- Support both clean-database and upgrade migration tests.

### Retention transaction

Existing retention eligibility/root-confinement/locking remains. Before job
deletion:

1. Lock and revalidate the meeting candidate.
2. Populate missing action provenance title/source snapshots.
3. Set linked action meeting IDs to null.
4. Prevalidate/delete safe artifacts.
5. Delete meeting, transcript, and minutes records.
6. Commit atomically where database operations apply; artifact failure retains
   the database record under existing safety behavior.

No action is cascade-deleted.

## 11. Security, privacy, and observability

- Phase 3 remains unauthenticated and restricted to a trusted local machine.
- Validate extension, MIME, byte size, encoding, filename, and content rules.
- Prevent storage-root escape and never expose physical paths.
- Do not log transcripts, action notes/descriptions, secrets, provider payloads,
  raw exceptions, or exported content.
- Log identifiers, mode, stage, outcome, attempt, elapsed time, aggregate query
  outcome, and safe error code.
- CSV export mitigates formula injection.
- Existing readiness checks remain. New configuration is startup-validated.

## 12. Configuration additions

Conceptual settings (final names follow existing option conventions):

```text
TranscriptUpload:MaxSizeMb = 10
TranscriptFormatting:SilenceGapSeconds = 1.5
TranscriptFormatting:PreferredParagraphCharacters = 300
TranscriptFormatting:HardParagraphCharacters = 700
```

The existing `MeetingMinutesGeneration:MaxTranscriptCharacters` default remains
1,200,000. API and Worker validate only settings they consume, with shared
values consistent between processes.

## 13. Architecture decisions

Existing ADR-001 through ADR-009 remain active unless superseded below.

### ADR-010 — Explicit processing modes

**Decision:** Persist one of three processing modes on every job and dispatch
one Worker orchestration path by mode.

**Reason:** Users need different outputs without duplicating queue/status/retry
infrastructure.

### ADR-011 — Provider-neutral structured transcription

**Decision:** Preserve timestamped segments behind an Application contract and
format them deterministically.

**Reason:** The current provider already returns segments; preserving them
improves readability and permits rerendering without retranscription.

### ADR-012 — Independent actions

**Decision:** Actions are independent aggregates with optional non-owning
meeting provenance. Meetings/minutes remain immutable.

**Reason:** Work continues after meetings finish and users can create actions
without meetings.

### ADR-013 — Opaque action concurrency version

**Decision:** DTOs carry an opaque version required by PATCH; stale updates
return HTTP 409.

**Reason:** Prevent lost updates without leaking persistence-specific tokens.

### ADR-014 — UTC action due dates

**Decision:** Store/evaluate due dates as UTC calendar dates without times.

**Reason:** One deterministic rule keeps API, dashboard, and frontend aligned in
the trusted-local single-user cycle.

### ADR-015 — Provider-neutral action export

**Decision:** Ship bounded CSV/JSON through `IActionItemExporter`; defer Jira.

**Reason:** Enables transfer while avoiding credentials/provider coupling.

## 14. Verification architecture

- Domain/Application unit tests for invariants/formulas/formatting.
- API contract tests for validation, mode, routes, conflicts, deletion, paging,
  and compatibility.
- PostgreSQL integration tests for migration, relational behavior, queries,
  concurrency, seeding/backfill, indexes, and retention.
- Worker tests with provider/storage doubles for every mode and retry checkpoint.
- Frontend routing/workflow/accessibility tests.
- Local acceptance with representative Phase 2 upgrade data and real local
  FFmpeg/Whisper/OpenAI only in the documented end-to-end run.

## 15. Future cloud mapping

The existing directional cloud mapping remains unchanged and outside Phase 3.
Any public/shared deployment must first address authentication, authorization,
ownership, networking, encryption, secrets, audit, cost, and data residency.

## 16. Governing principle

Keep HTTP requests short, processing durable and mode-minimal, transcripts
traceable, meetings immutable, actions independent, queries bounded, and
infrastructure replaceable.

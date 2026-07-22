# Phase 3 low-fidelity UX contract

**Status:** Approved implementation baseline  
**Date:** 2026-07-22

This document defines information architecture and required states, not visual
styling. Material UI, responsive, keyboard, focus, and live-region conventions
from Phase 2 remain.

## 1. Navigation and routes

```text
Application shell
  Dashboard             /
  New Processing Job    /process/new
  Meeting Minutes       /meetings
  Meeting Detail        /meetings/:jobId
  Actions               /actions
  All Processing        /processing
```

Primary navigation exposes Dashboard, New Processing Job, Meeting Minutes,
Actions, and All Processing. Meeting Detail is reached contextually. Refreshing
any route must restore that route; unknown routes show a useful not-found view.

On narrow screens, navigation collapses into an accessible menu. The current
destination is communicated visually and programmatically.

## 2. Dashboard

```text
+----------------------------------------------------------+
| MeetingMind                         [New processing job]  |
+----------------------------------------------------------+
| Dashboard                                                |
| All-time activity                                        |
| [Total jobs] [Completed] [Active] [Failed] [Cancelled]   |
| [Success rate] [Audio processed] [Avg processing time]   |
| [Transcripts] [Minutes]                                  |
|                                                          |
| Actions: [Open] [In progress] [Blocked] [Overdue] [...]  |
|                                                          |
| Recent processing              Recently generated minutes|
| ...                            ...                       |
+----------------------------------------------------------+
```

Required states:

- Loading skeletons with one polite status announcement.
- Zero-data welcome with New Processing Job action; success rate is `—`.
- Populated all-time metrics, five recent jobs, and five recent minutes.
- Partial/unavailable metric shown as `—`, not zero.
- Safe failure with Retry and navigation still usable.
- Metric cards link only where the destination/filter is meaningful.

Success rate excludes cancelled and active jobs. Cancelled remains its own card.
Action metrics never appear as meeting progress.

## 3. New Processing Job

### Step 1 — Choose result

```text
What do you want to create?

[ Transcript only ]
Audio -> readable transcript

[ Full meeting ]
Audio -> readable transcript + meeting minutes

[ Minutes from transcript ]
.txt/.md -> meeting minutes
```

### Step 2 — Upload

- Audio modes accept MP3/WAV/M4A/AAC and show configured audio limit.
- Transcript mode accepts `.txt`/`.md`, shows 10 MB and character limits, and
  states that the content must be UTF-8.
- Selected mode remains visible with a Change choice action.
- Submit is disabled until one valid file is selected.
- Client validation improves feedback but never replaces API validation.

### Accepted state

Show job ID, mode, queued status, and View processing. Begin the single
non-overlapping polling behavior only on views that display the active job.

Required error states: unsupported extension/MIME, unsafe/empty filename,
empty/whitespace/binary/invalid UTF-8, byte/character limit, enqueue failure,
and safe unexpected failure. Move focus to actionable errors.

## 4. All Processing

```text
All Processing
[20 per page; total N]

[source] [mode] [status/stage] [progress] [duration] [open]
...
[Previous] Page X of Y [Next]
```

- Newest first; 20 items per page.
- Display mode explicitly.
- Preserve selected active job polling across pages as in Phase 2.
- Transcript-only completed jobs open transcript result, FullMeeting and
  MinutesFromTranscript may open Meeting Detail.
- Failed/cancelled jobs show safe error and eligible retry.
- Empty, loading, pagination-boundary, retry-waiting, exhausted, not-found, and
  refresh states are required.

## 5. Meeting Minutes

```text
Meeting Minutes

[Title]
source.ext | Full meeting | completed date
[Open meeting]
...
[Previous] Page X of Y [Next]
```

- Only records with minutes.
- Newest first; 20 items per page.
- Do not show action completion as meeting progress.
- Loading, empty, failure, and pagination boundaries are required.

## 6. Meeting Detail

```text
Title
source | source type | created/completed timestamps
[Download minutes] [View/download transcript]

Summary
Attendees
Discussion points
Decisions
Generated action-items snapshot
Risks
Next steps

Actions linked to this meeting [View in Actions]
```

The generated action-items section is part of immutable minutes. Linked actions
are a separate context panel/list. Editing/deleting an independent action never
changes the generated section or meeting status.

Required states: loading, not found, minutes unavailable, transcript unavailable,
download failure, no linked actions, and linked-action navigation.

## 7. Transcript viewer

```text
Transcript
[Show timestamps: on] [Download clean text]

[00:00:04] First readable paragraph...

[00:01:22] Next readable paragraph...
```

Timestamps label paragraph starts, not speakers. Timestamp visibility may be a
viewer presentation control; the clean download contains paragraphs without
timestamps. Long content preserves readable width, selection, search, and
keyboard scrolling.

## 8. Actions

```text
Actions                                      [Create action]

[Status] [Assignee] [Due/overdue] [Source] [Meeting] [Clear]
[Export filtered] [Export selected]

[ ] Description | Assignee | Due | Status | Source | Meeting | [Open]
...
[Previous] Page X of Y [Next]
```

- Independent list; 25 items per page, newest first.
- Filters are server-backed and reflected in accessible control state.
- Generated/manual source and optional meeting context are visible.
- Absence of meeting context is normal, not an error.
- Selection survives only while it remains valid for the current result set;
  clearing rules must be explicit in implementation tests.

Required states: loading, no actions, no filter results, list failure, export
failure, and pagination boundaries.

### Create/edit action

Fields:

- Description (required; counter/limit 2,000).
- Assignee (optional; limit 200).
- Due date (optional; labelled UTC date).
- Status.
- Notes (optional; counter/limit 10,000).
- Optional meeting lookup/link.

Show provenance title/source snapshots when present. Saving requires the latest
opaque version for edits.

### Conflict

HTTP 409 opens an alert explaining that the action changed elsewhere. Offer
Reload latest and preserve the unsaved draft so the user can compare/reapply.
Do not silently overwrite.

### Delete

Use a modal confirmation:

```text
Delete action permanently?
This removes the tracked action. It does not change its meeting or generated
meeting minutes.

[Cancel] [Delete action]
```

Initial focus is on Cancel, Escape closes, destructive action is clearly
labelled, and focus returns predictably. On success, announce deletion and
remove the row. On failure, retain the action and show a safe retryable error.

## 9. Responsive and accessibility baseline

- Desktop tables may become labelled cards on narrow screens without losing
  fields or actions.
- Touch targets and paging controls remain usable at narrow widths.
- Every form control has a persistent label and error association.
- Status is not communicated by color alone.
- Route changes focus the page heading; selection focuses detail heading;
  actionable failures focus their alert.
- Routine polling does not steal focus.
- One polite live region announces processing changes; action command results
  use concise announcements without duplication.
- Confirmation dialogs trap focus correctly and restore it.
- Automated axe checks cover primary empty, populated, active, failed,
  conflict, and dialog states; manual contrast remains required.

## 10. UX acceptance matrix

| Area | Minimum tested states |
| --- | --- |
| Dashboard | loading, zero, populated, null rate, failure |
| New job | three modes, validation failures, accepted, enqueue failure |
| Processing | active polling, terminal, retry wait/exhausted, pagination |
| Minutes | empty, populated, paging, deep-link refresh, not found |
| Transcript | timestamps, clean download, long content, unavailable |
| Actions | empty, filters, paging, create, edit, link/unlink, conflict |
| Delete | cancel, confirm success, failure, focus restoration |
| Export | filtered, selected, CSV, JSON, failure |

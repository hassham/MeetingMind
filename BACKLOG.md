# BACKLOG.md - MeetingMind AI

This file contains active work only. The completed Phase 1 MVP delivery cycle,
including implementation history and decision records, is preserved under
[`docs/archive/phase1`](docs/archive/phase1/README.md).

Status legend: `TODO` | `IN PROGRESS` | `BLOCKED` | `PARTIAL` | `DONE`

Last verified against code: 2026-07-15.

## Current focus

- [ ] FR-004 - Complete job duration tracking.
      Status: PARTIAL.
      Notes: `GET /api/meetings/{jobId}/status` exposes job ID, status, stage,
      progress, and error message. Explicit duration is not exposed yet.

## Active backlog

- [ ] Long-transcript chunking and hierarchical summarization.
      Status: TODO.
      Notes: The minutes service currently guards against transcripts longer
      than 120000 characters. Longer meetings need chunking and aggregation.

- [ ] Complete the local secrets and configuration strategy.
      Status: PARTIAL.
      Notes: Storage, upload limits, FFmpeg, local transcription, and OpenAI
      settings exist. Machine-specific paths should be made portable, and
      Hangfire retry behavior remains intentionally manual/configuration-light.

- [ ] Expand persistence abstractions when required by a concrete use case.
      Status: PARTIAL.
      Notes: `IMeetingJobRepository` covers the current upload, processing,
      status, history, retry, and result workflows. Add broader repositories
      only when another use case requires them.

- [ ] Reconcile durable documentation with the implemented local MVP.
      Status: TODO.
      Notes: `AGENTS.md` and older specification language still contain some
      references to OpenAI Whisper and the pre-Worker architecture. The current
      implementation uses local Whisper.net and a separate Worker entry point.

## Recently completed

- [x] Phase 1 MVP delivery cycle.
      Status: DONE.
      Completed: 2026-07-13.
      Notes: Upload, asynchronous Hangfire processing, FFmpeg conversion, local
      Whisper transcription, GPT minutes generation, history, retry, downloads,
      and the React frontend were implemented and verified. See the
      [Phase 1 archive](docs/archive/phase1/README.md) for the complete record.

# Phase 1 MVP delivery archive

This folder preserves the completed MeetingMind Phase 1 MVP delivery cycle,
covering the internal implementation phases 1 through 10. The MVP was completed
and verified in July 2026.

Active work is tracked in the repository-root [`BACKLOG.md`](../../../BACKLOG.md).
The current project guide remains in the repository-root
[`README.md`](../../../README.md).

## Completed scope

Phase 1 delivered:

- The Clean Architecture solution and PostgreSQL persistence model.
- Validated audio upload and immediate job creation.
- Hangfire queueing and a separate Worker process.
- FFmpeg conversion to Whisper-ready WAV.
- Local Whisper.net transcription.
- OpenAI GPT structured meeting-minutes generation.
- Transcript and minutes persistence and downloads.
- Status polling, processing history, and failed-job retry.
- The React and Material UI frontend MVP.

## Archived backlog

- [`BACKLOG.md`](BACKLOG.md) - complete backlog snapshot at the end of the
  Phase 1 MVP cycle.

## Decision records

- [`QUESTIONS_phase6_ffmpeg_audio_processing.md`](QUESTIONS_phase6_ffmpeg_audio_processing.md)
  - FFmpeg output, validation, and audio-processing decisions.
- [`QUESTIONS_phase7_local_whisper_transcription.md`](QUESTIONS_phase7_local_whisper_transcription.md)
  - local Whisper model, language, storage, and transcription decisions.
- [`QUESTIONS_phase8_meeting_minutes_generation.md`](QUESTIONS_phase8_meeting_minutes_generation.md)
  - OpenAI model, structured output, persistence, and transcript-limit decisions.
- [`QUESTIONS_phase9_retry_and_history.md`](QUESTIONS_phase9_retry_and_history.md)
  - retry eligibility, job identity, history, and pagination decisions.
- [`QUESTIONS_phase10_frontend_mvp.md`](QUESTIONS_phase10_frontend_mvp.md)
  - frontend layout, polling, result, download, and retry decisions.
- [`QUESTIONS_result_detail_layout.md`](QUESTIONS_result_detail_layout.md)
  - selected-job result and processing-metadata layout decision.
- [`QUESTIONS_frontend_lint_cleanup.md`](QUESTIONS_frontend_lint_cleanup.md)
  - focused React effect and callback-stability cleanup decision.
- [`QUESTIONS_readme_refresh.md`](QUESTIONS_readme_refresh.md)
  - current-state README scope decision.
- [`QUESTIONS_documentation_archiving.md`](QUESTIONS_documentation_archiving.md)
  - Phase 1 archive structure and scope decision.

All decision records in this folder are final and marked `Status: ANSWERED`.

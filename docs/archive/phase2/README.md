# Phase 2 delivery archive

This folder preserves planning and implementation decisions for the MeetingMind
Phase 2 local production-readiness and hardening cycle.

Phase 2 is complete. The repository-root [`BACKLOG.md`](../../../BACKLOG.md)
now points here and confirms that Phase 3 has not started.

## Completion records

- [`BACKLOG_completed.md`](BACKLOG_completed.md) - the complete ordered Phase 2
  backlog, boundaries, acceptance criteria, and completion statuses.
- [`PHASE2_VERIFICATION.md`](PHASE2_VERIFICATION.md) - sanitized automated,
  end-to-end, readiness, artifact, and real-browser release evidence.

## Confirmed scope

Phase 2 completes local hardening, reliable retries, long-meeting processing,
portable configuration, duration tracking, operational improvements, frontend
completeness, and comprehensive verification.

AWS/Azure deployment, authentication, multi-user functionality, and unrelated
new product features are outside Phase 2.

## Decision records

- [`QUESTIONS_phase2_planning.md`](QUESTIONS_phase2_planning.md) - delivery-cycle
  theme, breadth, retries, long meetings, deployment boundary, and completion
  gate.
- [`QUESTIONS_p2_02_automated_verification.md`](QUESTIONS_p2_02_automated_verification.md)
  - backend test separation, disposable PostgreSQL, API test hosting, Worker
  doubles, frontend tooling, Docker behavior, and coverage policy.
- [`QUESTIONS_p2_03_portable_configuration.md`](QUESTIONS_p2_03_portable_configuration.md)
  - local-key remediation, shared storage configuration, local database
  defaults, FFmpeg discovery, Whisper provisioning, OpenAI key configuration,
  and removal of the stub-processing delay.
- [`QUESTIONS_p2_04_processing_duration.md`](QUESTIONS_p2_04_processing_duration.md)
  - processing versus total duration, incomplete timestamp fallbacks, manual
  retry timing, contract units, clock injection, and live frontend display.
- [`QUESTIONS_p2_05_reliable_automatic_retries.md`](QUESTIONS_p2_05_reliable_automatic_retries.md)
  - failure classification, unknown-exception behavior, retry budget and state,
  persisted retry metadata, checkpoint resume, and renewed manual-retry budget.
- [`QUESTIONS_p2_06_long_meeting_support.md`](QUESTIONS_p2_06_long_meeting_support.md)
  - Application orchestration, character bounds, textual chunking, hierarchical
  aggregation, deterministic deduplication, retries, and progress reporting.
- [`QUESTIONS_p2_07_operational_hardening.md`](QUESTIONS_p2_07_operational_hardening.md)
  - structured timing, privacy-safe diagnostics, readiness ownership,
  retention scheduling, cleanup safety, and safe error disclosure.
- [`QUESTIONS_p2_08_frontend_experience.md`](QUESTIONS_p2_08_frontend_experience.md)
  - 20-item pagination, off-page selection and polling, retry presentation,
  safe messaging, accessibility automation, focus, and responsive behavior.
- [`QUESTIONS_p2_09_release_verification.md`](QUESTIONS_p2_09_release_verification.md)
  - final gate environment, privacy-safe fixtures, long-meeting proof, retry
  evidence, manual browser checks, completion archive, and Git finalization.

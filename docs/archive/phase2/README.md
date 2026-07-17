# Phase 2 delivery archive

This folder preserves planning and implementation decisions for the MeetingMind
Phase 2 local production-readiness and hardening cycle.

Active work remains tracked in the repository-root
[`BACKLOG.md`](../../../BACKLOG.md).

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

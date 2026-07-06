**Functional Specification Document (FSD)**

**Project Title**

AI Meeting Minutes Generator

**Version:** 1.0

**Document Status:** Draft

**Prepared By:** Hasham Ahmad

**1. Purpose**

The purpose of this application is to automate the creation of
professional meeting minutes from an uploaded meeting recording.

The application allows users to upload an audio recording of a meeting
through a web interface. The system processes the audio asynchronously,
generates a transcript using an AI transcription service, produces
structured meeting minutes using a Large Language Model (LLM), and makes
the final output available for download.

The system is designed to be modular, scalable, and cloud-ready while
initially running as a local application.

**2. Objectives**

-   Eliminate manual note-taking.

-   Generate professional meeting minutes automatically.

-   Demonstrate modern software architecture.

-   Build an application that can later migrate to a cloud-native
    architecture with minimal code changes.

**3. Scope**

**Included**

-   Audio upload

-   Background processing

-   Audio transcoding

-   Speech-to-text transcription

-   AI-generated meeting minutes

-   Job progress tracking

-   Transcript storage

-   Meeting minutes storage

-   Retry failed jobs

-   Administrative dashboard (no authentication/authorization in v1 --
    accessible to any user of the deployment)

-   Download generated meeting minutes

**Excluded (Version 1)**

-   Video processing

-   Multi-language translation

-   User authentication

-   Multi-tenant support

-   Real-time meeting transcription

-   Live meeting recording

-   Email notifications

-   Calendar integrations

**4. Technology Stack**

**Frontend**

-   React

-   Material UI

-   Axios

**Backend**

-   ASP.NET Core Web API

-   Entity Framework Core

-   Clean Architecture

-   Dependency Injection

**Database**

-   PostgreSQL (Docker)

**Background Processing**

-   Hangfire for queueing, background job execution, retries, and workflow orchestration

**Audio Processing**

-   FFmpeg

**AI Services**

-   OpenAI Whisper API

-   OpenAI GPT

**Storage**

Local File Storage

Storage/

Audio/

Transcript/

Minutes/

**5. System Architecture**

User

↓

React Frontend

↓

ASP.NET Core Web API

↓

PostgreSQL

↓

Hangfire

↓

FFmpeg

↓

Whisper API

↓

OpenAI GPT

↓

Meeting Minutes

↓

Database + File Storage

**6. Functional Requirements**

**FR-001 Upload Meeting**

The user shall be able to upload an audio recording.

Supported formats:

-   MP3

-   WAV

-   M4A

-   AAC

Maximum file size shall be configurable.

**FR-002 Create Processing Job**

Upon upload:

-   Save audio file

-   Create database record

-   Generate Job ID

-   Queue background job

-   Return Job ID immediately

**FR-003 Background Processing**

Background processing shall execute independently of the user session.

Processing stages:

1.  Validate audio

2.  Transcode audio

3.  Generate transcript

4.  Clean transcript

5.  Generate meeting minutes

6.  Save output

7.  Mark job completed

**FR-004 Job Status**

The system shall support:

-   Queued

-   Processing

-   Completed

-   Failed

Each job shall maintain:

-   Progress percentage

-   Current processing stage

-   Error message (if failed)

-   Processing duration

**FR-005 Audio Transcoding**

The system shall convert uploaded audio into a standardized format
before transcription.

**FR-006 Transcription**

The system shall send audio to the transcription provider and retrieve
the generated transcript.

**FR-007 Meeting Minutes Generation**

The system shall generate structured meeting minutes containing:

-   Meeting title

-   Executive summary

-   Attendees (if identifiable)

-   Discussion topics

-   Decisions made

-   Action items

-   Risks / blockers

-   Next steps

**FR-008 View Results**

Users shall be able to:

-   View transcript

-   View meeting minutes

-   Download transcript

-   Download meeting minutes

**FR-009 Retry Failed Jobs**

Any users shall be able to retry failed jobs without re-uploading the
audio.

**FR-010 Processing History**

The system shall maintain historical records for all processing jobs.

**7. Non-Functional Requirements**

**Performance**

-   Upload response \< 3 seconds

-   API returns immediately after job creation

-   Long-running operations execute asynchronously

**Reliability**

-   Automatic retries for transient failures

-   Failed jobs recorded with error details

-   No data loss after successful upload

**Scalability**

Architecture shall support future migration to:

-   AWS

-   Azure

without changes to business logic.

**Maintainability**

The solution shall follow:

-   SOLID Principles

-   Clean Architecture

-   Repository Pattern

-   Dependency Injection

-   Service Abstraction

**Security**

-   Validate uploaded files

-   Restrict supported formats

-   Secure API keys

-   Validate file size

-   Prevent path traversal attacks

**8. Database Design**

**MeetingJob**

  -----------------------------------------------------------------------
  **Field**                    **Description**
  ---------------------------- ------------------------------------------
  Id                           Unique Identifier

  FileName                     Uploaded file

  Status                       Current status

  Progress                     Percentage complete

  CurrentStage                 Current processing step

  TranscriptPath               Transcript location

  MinutesPath                  Minutes location

  ErrorMessage                 Failure reason

  CreatedDate                  Timestamp

  CompletedDate                Timestamp
  -----------------------------------------------------------------------

**MeetingTranscript**

  -----------------------------------------------------------------------
  **Field**                  **Description**
  -------------------------- --------------------------------------------
  Id                         Unique Identifier

  JobId                      Related Job

  Transcript                 Transcript Text
  -----------------------------------------------------------------------

**MeetingMinutes**

  -----------------------------------------------------------------------
  **Field**              **Description**
  ---------------------- ------------------------------------------------
  Id                     Unique Identifier

  JobId                  Related Job

  Summary                Executive Summary

  Decisions              Decisions

  ActionItems            Action Items

  Risks                  Risks

  NextSteps              Next Steps

  MinutesJson            Complete Structured Output
  -----------------------------------------------------------------------

**9. User Workflow**

Upload Audio

↓

Create Job

↓

Return Job ID

↓

Queue Background Job

↓

Validate Audio

↓

Transcode Audio

↓

Generate Transcript

↓

Generate Meeting Minutes

↓

Save Results

↓

Update Status

↓

User Views Results

**10. Error Handling**

The system shall handle:

-   Invalid file format

-   Corrupted audio

-   FFmpeg failures

-   Whisper API failures

-   LLM failures

-   Database failures

-   Network failures

Each failure shall:

-   Record detailed logs

-   Update job status

-   Allow retry

**11. Future Enhancements**

-   User authentication

-   Multi-user support

-   Teams and organizations

-   Speaker diarization

-   Multiple LLM providers

-   AWS cloud deployment

-   Azure deployment

-   S3 / Azure Blob Storage

-   Step Functions / SQS migration

-   Email notifications

-   Meeting templates

-   Microsoft Teams integration

-   Zoom integration

-   Google Meet integration

**12. Success Criteria**

The project will be considered successful when it can:

-   Accept meeting audio uploads.

-   Process recordings asynchronously.

-   Generate accurate transcripts.

-   Produce structured meeting minutes.

-   Track job progress.

-   Recover from failures.

-   Retry failed jobs.

-   Be migrated to a cloud architecture with minimal changes to the
    business logic.

**13. Architecture Evolution**

**Phase 1 (Current)**

-   React

-   ASP.NET Core Web API

-   PostgreSQL

-   Hangfire

-   FFmpeg

-   OpenAI Whisper API

-   OpenAI GPT

-   Local File Storage

**Phase 2 (Cloud)**

-   CloudFront

-   S3

-   API Gateway

-   Lambda / ECS

-   Amazon RDS PostgreSQL

-   Step Functions

-   Amazon S3

-   Amazon Transcribe (optional)

-   Amazon Bedrock (optional)

The business logic and domain layer shall remain unchanged between Phase
1 and Phase 2, with only infrastructure implementations being replaced.

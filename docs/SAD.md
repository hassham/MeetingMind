**Software Architecture Document (SAD)**

**Project Title**

AI Meeting Minutes Generator

**Version:** 1.0\
**Status:** Draft\
**Architecture Style:** Modular monolith with asynchronous background
processing\
**Initial Deployment:** Development environment\
**Future Deployment:** Cloud-ready architecture

**1. Architecture Goals**

The system is designed to:

-   Process long-running AI workloads asynchronously.

-   Keep the web API responsive.

-   Separate business logic from infrastructure.

-   Support future migration to AWS or Azure.

-   Provide clear job tracking, retries, and observability.

-   Avoid local LLM hosting in the first version.

**2. Finalized Local Architecture**

User

↓

React Frontend

↓

ASP.NET Core Web API

↓

PostgreSQL

↓

Hangfire Background Job

↓

Processing Pipeline

├── File validation

├── FFmpeg audio conversion

├── OpenAI Whisper transcription

├── Transcript cleanup

├── OpenAI GPT meeting-minutes generation

└── Save transcript + minutes

**3. Component Overview**

**3.1 React Frontend**

Responsibilities:

-   Upload audio file.

-   Display processing status.

-   Poll API for progress.

-   Show transcript.

-   Show generated meeting minutes.

-   Allow download of results.

The frontend must not perform audio processing or AI processing.

**3.2 ASP.NET Core Web API**

Responsibilities:

-   Receive upload requests.

-   Validate basic request metadata.

-   Save uploaded file through storage abstraction.

-   Create MeetingJob record.

-   Enqueue Hangfire job.

-   Return jobId immediately.

-   Expose status/result endpoints.

The API must not block while transcription or summarisation is running.

**3.3 PostgreSQL**

Used for:

-   Job metadata.

-   Job status.

-   Progress.

-   Error messages.

-   Transcript records.

-   Meeting minutes records.

-   Processing history.

**3.4 Hangfire**

Used for:

-   Background job execution.

-   Retry handling.

-   Failed job tracking.

-   Job dashboard.

-   Asynchronous processing.

Hangfire executes the main processing method:

ProcessMeetingAsync(Guid jobId)

**3.5 Local File Storage**

Initial storage structure:

Storage/

Audio/

Original/

Processed/

Transcript/

Minutes/

Later this can be replaced with S3 or Azure Blob Storage.

**3.6 FFmpeg**

Used for:

-   Audio format conversion.

-   Audio normalization.

-   Preparing audio for transcription provider.

FFmpeg should be wrapped behind an application service, not called
directly from controllers.

**3.7 OpenAI Whisper API**

Used for speech-to-text transcription.

Input:

Processed audio file

Output:

Transcript text

**3.8 OpenAI GPT**

Used for meeting-minutes generation.

Input:

Cleaned transcript

Output:

{

\"title\": \"\",

\"summary\": \"\",

\"attendees\": \[\],

\"discussionPoints\": \[\],

\"decisions\": \[\],

\"actionItems\": \[\],

\"risks\": \[\],

\"nextSteps\": \[\]

}

**4. Logical Architecture**

Frontend Layer

React UI

API Layer

Controllers

Request/response DTOs

Application Layer

Use cases

Interfaces

Orchestration services

Validation

Job workflow logic

Domain Layer

Entities

Enums

Business rules

Infrastructure Layer

PostgreSQL

Hangfire

Local file storage

FFmpeg

OpenAI clients

Recommended solution structure:

MeetingMinutes.sln

src/

MeetingMinutes.Api/

MeetingMinutes.Application/

MeetingMinutes.Domain/

MeetingMinutes.Infrastructure/

MeetingMinutes.Web/

**5. Key Interfaces**

Use abstractions from the beginning.

public interface IFileStorageService

{

Task\<string\> SaveAsync(Stream file, string fileName, CancellationToken
cancellationToken);

Task\<Stream\> ReadAsync(string filePath, CancellationToken
cancellationToken);

Task DeleteAsync(string filePath, CancellationToken cancellationToken);

}

public interface IAudioProcessingService

{

Task\<string\> ConvertToStandardFormatAsync(string inputPath,
CancellationToken cancellationToken);

}

public interface ITranscriptionService

{

Task\<string\> TranscribeAsync(string audioPath, CancellationToken
cancellationToken);

}

public interface IMeetingMinutesService

{

Task\<MeetingMinutesResult\> GenerateAsync(string transcript,
CancellationToken cancellationToken);

}

public interface IBackgroundJobService

{

string EnqueueMeetingProcessing(Guid jobId);

}

**6. Main Processing Flow**

**6.1 Upload Flow**

User uploads audio

↓

React sends multipart/form-data request

↓

API validates request

↓

API saves file locally

↓

API creates MeetingJob record

↓

API enqueues Hangfire background job

↓

API returns jobId

↓

Frontend starts polling

**6.2 Background Processing Flow**

Hangfire starts ProcessMeetingAsync(jobId)

↓

Load MeetingJob from database

↓

Update status: Processing

↓

Validate audio file

↓

Update progress: 10%

↓

Run FFmpeg conversion

↓

Update progress: 25%

↓

Send audio to Whisper API

↓

Update progress: 60%

↓

Clean transcript

↓

Update progress: 70%

↓

Send transcript to GPT

↓

Update progress: 90%

↓

Save transcript and minutes

↓

Update status: Completed

↓

Update progress: 100%

**7. Job Status Model**

Recommended statuses:

public enum MeetingJobStatus

{

Queued,

Processing,

Completed,

Failed,

Cancelled

}

Recommended stages:

public enum MeetingJobStage

{

Uploaded,

Validating,

Transcoding,

Transcribing,

GeneratingMinutes,

SavingResults,

Completed,

Failed

}

**8. Data Model**

**MeetingJob**

Id

OriginalFileName

OriginalFilePath

ProcessedFilePath

Status

Stage

Progress

ErrorMessage

HangfireJobId

CreatedAt

StartedAt

CompletedAt

UpdatedAt

**MeetingTranscript**

Id

MeetingJobId

TranscriptText

TranscriptFilePath

CreatedAt

**MeetingMinutes**

Id

MeetingJobId

Title

Summary

DecisionsJson

ActionItemsJson

RisksJson

NextStepsJson

FullMinutesJson

MinutesFilePath

CreatedAt

**9. API Endpoints**

**Upload meeting**

POST /api/meetings/upload

Returns:

{

\"jobId\": \"guid\",

\"status\": \"Queued\"

}

**Get job status**

GET /api/meetings/{jobId}/status

Returns:

{

\"jobId\": \"guid\",

\"status\": \"Processing\",

\"stage\": \"Transcribing\",

\"progress\": 60,

\"errorMessage\": null

}

**Get meeting result**

GET /api/meetings/{jobId}/result

Returns transcript and minutes if completed.

**Retry failed job**

POST /api/meetings/{jobId}/retry

**Download minutes**

GET /api/meetings/{jobId}/minutes/download

**Download transcript**

GET /api/meetings/{jobId}/transcript/download

**10. Error Handling Strategy**

Each pipeline step must be wrapped with error handling.

Failure rules:

-   Mark job as Failed.

-   Save technical error message.

-   Save current stage.

-   Log exception.

-   Allow retry from admin dashboard.

Retryable failures:

-   OpenAI timeout

-   Temporary network failure

-   Rate limit

-   Transient database issue

Non-retryable failures:

-   Invalid file type

-   Corrupted audio

-   File too large

-   Missing uploaded file

**11. Observability**

Initial local observability:

-   ASP.NET Core logs

-   Hangfire dashboard

-   PostgreSQL job history

-   Error messages in MeetingJob

-   Structured logs per pipeline stage

Future cloud observability:

-   CloudWatch

-   X-Ray

-   Application Insights

-   Centralized distributed tracing

**12. Security Architecture**

Initial version:

-   Validate file extension.

-   Validate MIME type.

-   Enforce file size limit.

-   Store API keys in environment variables or user secrets.

-   Do not expose local file paths to the frontend.

-   Generate safe internal file names.

-   Prevent path traversal.

-   Do not log API keys or full transcripts unnecessarily.

-   No authentication/authorization

Future version:

-   User authentication.

-   Role-based access control.

-   Per-user job ownership.

-   Encrypted object storage.

-   Audit logs.

-   Secret Manager / Key Vault.

**13. Configuration**

Configuration should be environment-based.

Example:

{

\"Storage\": {

\"RootPath\": \"Storage\",

\"MaxUploadSizeMb\": 100

},

\"OpenAI\": {

\"ApiKey\": \"\",

\"TranscriptionModel\": \"whisper-1\",

\"MinutesModel\": \"gpt-4.1\"

},

\"Processing\": {

\"MaxRetryAttempts\": 3

}

}

Sensitive values must not be committed to Git.

**14. Cloud Migration Path**

  -----------------------------------------------------------------------
  **Local Component**         **Future AWS Component**
  --------------------------- -------------------------------------------
  React local build           S3 + CloudFront

  ASP.NET Core Web API        ECS / Lambda / App Runner

  PostgreSQL Docker           Amazon RDS PostgreSQL

  Local file storage          Amazon S3

  Hangfire                    Step Functions + SQS

  FFmpeg local                Lambda / ECS Fargate

  Whisper API                 OpenAI Whisper / Amazon Transcribe

  OpenAI GPT                  OpenAI / Amazon Bedrock

  Local logs                  CloudWatch / X-Ray
  -----------------------------------------------------------------------

The Application and Domain layers should remain mostly unchanged.

Only Infrastructure implementations should be replaced.

**15. Architecture Decisions**

**ADR-001: Use asynchronous processing**

Decision:

Use Hangfire to process meetings in the background. Decision confirmed
2026-07-06.

Reason:

Audio transcription and LLM summarisation are long-running tasks and
should not block the HTTP request.

**ADR-002: Use PostgreSQL**

Decision:

Use PostgreSQL locally through Docker.

Reason:

It is cloud-portable, cost-effective, production-capable, and easy to
migrate to RDS or Azure PostgreSQL.

**ADR-003: Use local storage initially**

Decision:

Store uploaded and generated files locally in Version 1.

Reason:

It keeps MVP complexity low while allowing later replacement with S3 or
Azure Blob through IFileStorageService.

**ADR-004: Use external AI APIs**

Decision:

Use OpenAI Whisper API and OpenAI GPT instead of local models.

Reason:

Local AI processing caused resource bottlenecks in the previous attempt.
External APIs allow the system architecture to be validated first.

**ADR-005: Use Hangfire for queueing and workflow execution**

Decision:

Use Hangfire instead of MSMQ or a SQLite-backed queue for the local MVP.
Hangfire is the selected implementation for queueing, background job
execution, retries, and workflow orchestration.

Reason:

Hangfire is easier to debug, supports retries and dashboard visibility,
and maps better to future cloud workflow processing. It also gives the team a
clear learning surface for how background jobs move the long-running
`FFmpeg -> Whisper -> OpenAI GPT -> save results` workflow out of the HTTP
request path.

**16. Deployment View**

**Local deployment**

Developer machine

├── React frontend

├── ASP.NET Core API

├── PostgreSQL Docker container

├── Hangfire dashboard

├── FFmpeg executable

└── Local Storage folder

**Future AWS deployment**

CloudFront

↓

S3 frontend hosting

↓

API Gateway / ECS API

↓

RDS PostgreSQL

↓

Step Functions

↓

S3 storage

↓

Transcribe / OpenAI / Bedrock

**17. Risks**

  -----------------------------------------------------------------------
  **Risk**                        **Mitigation**
  ------------------------------- ---------------------------------------
  Large files cause slow          Enforce file size limits
  processing                      

  OpenAI API failures             Retry transient failures

  Cost increases                  Add usage limits and logging

  Bad transcript quality          Add audio normalization and prompt
                                  tuning

  Long transcript exceeds LLM     Add chunking and hierarchical
  context                         summarisation

  Local storage grows too large   Add cleanup policy

  User refreshes browser          Job status persisted in database
  -----------------------------------------------------------------------

**18. Summary**

This architecture provides a practical MVP while preserving a clear path
to cloud migration.

The core principle is:

Keep business logic stable.

Make infrastructure replaceable.

Run long tasks asynchronously.

Track every job clearly.

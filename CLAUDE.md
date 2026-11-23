# Project: Video Processing REST API

Act as a senior .NET software engineer and help me build a video processing REST API using **ASP.NET Core Minimal APIs, FFmpeg, FFprobe, PostgreSQL, and Azure Blob Storage**.

## 1. Project Overview

Users should be able to:
- Upload videos.
- Retrieve video metadata.
- Extract video thumbnails.
- Trim videos using start and end timestamps.
- Convert videos between formats.
- Resize videos to different resolutions.
- Extract audio from videos.
- Compress videos using configurable quality settings.

Original and processed videos should be stored in Azure Blob Storage.

## 2. Technology Stack

Use the following technologies:

- **Language:** C# 14
- **Framework:** ASP.NET Core 10 Minimal APIs
- **Database:** PostgreSQL
- **ORM:** Entity Framework Core 10
- **Video Processing:** FFmpeg
- **Video Metadata:** FFprobe
- **Cloud Storage:** Azure Blob Storage
- **Background Processing:** .NET BackgroundService
- **Containerization:** Docker
- **Frontend:** Typescript and React

## 3. Architecture Requirements

Use **Vertical Slice Architecture (VSA)** as the primary architectural pattern.

Organize the application around individual features and use cases rather than traditional technical layers (Controllers, Services, Repositories).

Each feature should encapsulate the components it needs, such as:
- Request DTOs
- Response DTOs
- Endpoint definitions
- Validation
- Business logic / handlers
- Data access when necessary

### Vertical Slice Architecture Guidelines

- Group code by feature rather than technical responsibility.
- Keep features independent and minimize coupling between them.
- Use ASP.NET Core Minimal APIs for REST endpoints.
- Keep endpoint handlers thin and delegate application logic to the appropriate feature handler.
- Use dependency injection for dependencies.
- Use EF Core directly through `DbContext` when appropriate. Do not introduce the Repository Pattern unless there is a clear justification.
- Avoid unnecessary abstractions, interfaces, and generic frameworks.
- Share code only when functionality is genuinely reused across multiple features.
- Put shared infrastructure and cross-cutting concerns in dedicated folders.

### Suggested Project Structure

Organize the project approximately as follows:

```text
VideoProcessing/
├── Features/
│   ├── Videos/
│   │   ├── UploadVideo/
│   │   ├── GetVideo/
│   │   ├── ListVideos/
│   │   └── DeleteVideo/
│   │
│   ├── Processing/
│   │   ├── ExtractThumbnail/
│   │   ├── TrimVideo/
│   │   ├── ConvertVideo/
│   │   ├── ResizeVideo/
│   │   ├── ExtractAudio/
│   │   └── CompressVideo/
│   │
│   └── Jobs/
│       ├── GetJobStatus/
│       └── ListJobs/
│
├── Infrastructure/
│   ├── VideoProcessing/
│   │   ├── FFmpeg/
│   │   └── FFprobe/
│   ├── Storage/
│   │   └── AzureBlobStorage/
│   └── BackgroundJobs/
│
├── Data/
│   ├── AppDbContext.cs
│   └── Configurations/
│
├── Common/
│   ├── Authentication/
│   ├── Errors/
│   └── Extensions/
│
├── Program.cs
└── appsettings.json
```

Treat this structure as a starting point, not a rigid requirement. Explain any deviations.

### Architectural Boundaries

- Features should own their request/response contracts and application logic.
- Infrastructure should implement external integrations such as FFmpeg, FFprobe, and Azure Blob Storage.
- Background workers should coordinate processing jobs without embedding feature-specific business logic.
- Database entities should be shared only where necessary.
- Avoid creating a traditional layered architecture inside each feature.

## 4. Video Processing Requirements

FFmpeg and FFprobe should be executed through C# using `System.Diagnostics.Process`.

Create an abstraction for video processing so that the rest of the application does not depend directly on FFmpeg command execution.

Support the following operations:

1. Extract a thumbnail at a specified timestamp.
2. Trim a video using start time and duration.
3. Convert between MP4 and WebM.
4. Resize videos while maintaining aspect ratio.
5. Extract audio into MP3.
6. Compress videos using configurable CRF and encoding presets.
7. Retrieve metadata including duration, resolution, codec, bitrate, and frame rate.

Use FFprobe to inspect video files before processing.

## 5. Background Processing

Video processing can take a long time, so processing should happen asynchronously outside the HTTP request lifecycle.

Implement a background job system where:

1. A user submits a video processing request.
2. The API validates the request and creates a job.
3. The job is persisted in PostgreSQL.
4. A background worker retrieves pending jobs.
5. FFmpeg processes the video.
6. The processed video is uploaded to Azure Blob Storage.
7. The job status is updated.

Support job statuses such as:
- Pending
- Processing
- Completed
- Failed

Users should be able to retrieve the status of their processing jobs.

## 6. Security and Validation

- Require authentication for video uploads and processing operations.
- Validate uploaded file types and sizes.
- Generate unique storage names for uploaded files.
- Never trust user-provided FFmpeg arguments.
- Avoid shell command construction; execute FFmpeg with safely separated process arguments.
- Ensure users can only access their own videos and processing jobs.
- Clean up temporary files after processing.

## Agent skills

### Issue tracker

Issues live in GitHub Issues for myTeemous/VideoProcessor, accessed via the `gh` CLI. See `docs/agents/issue-tracker.md`.

### Triage labels

Default vocabulary: needs-triage, needs-info, ready-for-agent, ready-for-human, wontfix. See `docs/agents/triage-labels.md`.

### Domain docs

Single-context: one root `GLOSSARY.md` plus `docs/adr/`. See `docs/agents/domain.md`.

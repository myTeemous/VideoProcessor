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
- **Authentication:** ASP.NET Core Identity (bearer tokens)
- **Validation:** FluentValidation
- **API Docs:** Scalar (OpenAPI)
- **Testing:** xUnit + FluentAssertions

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

### Project Structure

The API and the Worker are separate hosts (see `docs/adr/0002-postgres-job-queue-separate-worker.md`), so code both need lives in `Core`:

```text
src/
├── Api/                          # HTTP host
│   ├── Features/
│   │   ├── Videos/
│   │   │   ├── CreateUploadSlot/
│   │   │   ├── ConfirmUpload/
│   │   │   ├── GetVideo/
│   │   │   ├── ListVideos/
│   │   │   ├── GetVideoDownload/
│   │   │   └── DeleteVideo/
│   │   ├── Processing/
│   │   │   ├── ExtractThumbnail/
│   │   │   ├── TrimVideo/
│   │   │   ├── ConvertVideo/
│   │   │   ├── ResizeVideo/
│   │   │   ├── ExtractAudio/
│   │   │   └── CompressVideo/
│   │   └── Jobs/
│   │       ├── GetJob/
│   │       ├── ListJobs/
│   │       └── GetOutputDownload/
│   ├── Common/                   # auth, validation endpoint filter, error mapping
│   └── Program.cs
│
├── Worker/                       # background host
│   ├── JobProcessing/            # polling, SKIP LOCKED claims, leases, timeouts
│   ├── Cleanup/                  # sweep of Videos stuck in AwaitingUpload
│   └── Program.cs
│
├── Core/                         # shared by Api and Worker
│   ├── Domain/                   # entities, enums, Result<T>
│   ├── Data/                     # AppDbContext, Configurations, Migrations
│   ├── Operations/               # Operation parameters -> FFmpeg arguments
│   └── Infrastructure/
│       ├── FFmpeg/
│       ├── FFprobe/
│       └── Storage/              # Azure Blob, SAS issuance
│
└── web/                          # React + TypeScript (Vite)

tests/
├── UnitTests/
└── IntegrationTests/             # Testcontainers (Postgres, Azurite), real FFmpeg on tiny sample clips
```

Features stay in `Api`: they are the HTTP use cases. Put code in `Core` only when both hosts need it.

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
2. Trim a video using start and end timestamps.
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

## 7. Code Conventions

### Naming
- DTOs: `[Entity]Dto`, `Create[Entity]Request`, `Get[Entity]Request`, `Create[Entity]Response`, `Get[Entity]Response`
- Use the domain terms defined in `GLOSSARY.md`.

### Patterns We Use
- Primary constructors for DI
- Records for DTOs, request and response contracts
- Result<T> pattern for error handling (no exceptions for flow control)
- File-scoped namespaces
- Always pass CancellationToken to async methods
- All request validation in FluentValidation validators, auto-registered via assembly scanning
- Always create a new branch based off of main when making changes

### Patterns We DON'T Use (Never Suggest)
- Repository pattern (use EF Core directly)
- AutoMapper (write explicit mappings)
- Exceptions for business logic errors
- Stored procedures

## Commands

Run from the repository root. Integration tests and the local stack need Docker running.

| Task | Command |
|---|---|
| Restore local tools (`dotnet-ef`) | `dotnet tool restore` |
| Build | `dotnet build` |
| Test (all) | `dotnet test` |
| Unit tests only | `dotnet test tests/UnitTests` |
| Single test class | `dotnet test tests/IntegrationTests --filter "FullyQualifiedName~AccountTests"` |
| Start Postgres + Azurite only | `docker compose up -d postgres azurite` |
| Run Api (Scalar at http://localhost:5080/scalar) | `dotnet run --project src/Api` |
| Run Worker | `dotnet run --project src/Worker` |
| Run whole stack (Api at http://localhost:8080) | `docker compose up --build` |
| Add migration | `dotnet ef migrations add <Name> --project src/Core --startup-project src/Api --output-dir Data/Migrations` |
| Update database | `dotnet ef database update --project src/Core --startup-project src/Api` |
| Format | `dotnet format` |

The Api applies pending migrations on startup in Development, so `database update` is only needed outside that.

## Agent skills

### Issue tracker

Issues live in GitHub Issues for myTeemous/VideoProcessor, accessed via the `gh` CLI. See `docs/agents/issue-tracker.md`.

### Triage labels

Default vocabulary: needs-triage, needs-info, ready-for-agent, ready-for-human, wontfix. See `docs/agents/triage-labels.md`.

### Domain docs

Single-context: one root `GLOSSARY.md` plus `docs/adr/`. See `docs/agents/domain.md`.

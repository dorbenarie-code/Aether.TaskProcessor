# Aether.TaskProcessor

Aether.TaskProcessor is a .NET 8 background job processing system with a clean REST API and a React + TypeScript frontend.

The system models a realistic backend flow where jobs are submitted through an API, stored, queued, processed by background workers, retried on failure, and tracked throughout their lifecycle.

The project is built with SOLID principles, layered architecture, testability, and small incremental slices in mind.

---

## Project Goals

The goal of this project is to practice and demonstrate:

- Clean architecture and proper separation of concerns
- SOLID principles in a real backend system
- Background job processing with workers
- Queue-based design using Channels
- REST API design with clear DTO contracts
- Validation and consistent error handling using ProblemDetails
- Retry strategies with exponential backoff
- Parallel processing with multiple workers
- Graceful shutdown and cancellation handling
- SQL Server persistence support
- React + TypeScript frontend integration
- Test-driven, incremental development across backend and frontend

---

## Architecture

The solution is divided into backend layers and a frontend client.

### Domain

Contains the core business logic.

Main job-processing components:

- `Job`
- `JobStatus`

Main optimization components:

- `Resource`
- `Shift`
- `AvailabilityWindow`
- `SchedulingProblem`
- `Assignment`
- `ScheduleCandidate`
- `ConstraintViolation`
- `ConstraintViolationSeverity`
- `ConstraintViolationType`
- `ScheduleConstraintEvaluator`
- `ScheduleScore`
- `ScheduleScoreCalculator`
- `ScheduleEvaluationResult`
- `ScheduleEvaluator`

Responsibilities:

- Managing the job lifecycle
- Handling retries and exponential backoff
- Enforcing valid job state transitions
- Validating UTC time usage
- Protecting job-processing invariants
- Modeling the first Resource & Shift Orchestrator domain concepts
- Representing scheduling problems and candidate schedules
- Evaluating basic scheduling constraint violations
- Calculating a simple schedule score from violations

The Domain layer is isolated and does not depend on infrastructure, API, or UI concerns.

The optimization domain is intentionally kept separate from the generic job processor. Jobs execute background work, while the optimization domain models scheduling concepts, constraints, violations, and scoring.

---

### Application

Contains use cases and orchestration logic.

Main components:

- `IJobRepository`
- `IJobQueue`
- `IJobHandler`
- `IJobSubmissionService`
- `IJobQueryService`
- `IJobCancellationService`
- `JobSubmissionService`
- `JobQueryService`
- `JobCancellationService`
- `JobWorker`
- `JobWorkerPool`

Responsibilities:

- Submitting jobs into the system
- Querying jobs through clean use cases
- Cancelling jobs with proper domain rules
- Coordinating job execution
- Resolving handlers by job type
- Managing retries and scheduling
- Running multiple workers in parallel
- Respecting cancellation tokens and shutdown behavior

---

### Infrastructure

Contains technical implementations.

Main components:

- `InMemoryJobRepository`
- `InMemoryJobQueue`
- `SqlServerJobRepository`
- `SqlServerConnectionFactory`
- `SqlServerOptions`

Responsibilities:

- Storing jobs in memory for lightweight local/test scenarios
- Managing the in-memory queue using `Channel`
- Persisting jobs in SQL Server
- Restoring persisted jobs back into domain objects

Infrastructure depends on Application abstractions and does not leak technical details into the Domain layer.

---

### API

Exposes the system through HTTP.

Main components:

- `JobsController`
- `SubmitJobRequest`
- `SubmitJobResponse`
- `JobResponse`
- `JobWorkerHostedService`
- `GlobalExceptionHandler`
- `RateLimitingPolicies`

Supported endpoints:

- `POST /api/jobs`
  - Submits a new job

- `GET /api/jobs`
  - Returns jobs with optional filtering and pagination

- `GET /api/jobs/{id}`
  - Returns a job by id

- `GET /api/jobs/failed`
  - Returns only failed jobs

- `POST /api/jobs/{id}/cancel`
  - Cancels a pending job
  - Returns `404` if the job does not exist
  - Returns `409` if the job cannot be cancelled

The API includes:

- DTO-based contracts
- Model validation using Data Annotations
- Automatic `400 Bad Request` responses
- Consistent error handling with `ProblemDetails`
- Background processing via `HostedService`
- Rate limiting for job submission
- Swagger support

Controllers remain thin and delegate business logic to the Application layer.

---

### Frontend

The frontend is implemented with React, TypeScript, and Vite.

Location:

```text
src/Aether.Web
```

Main implemented areas:

- Typed Jobs API client
- Jobs list screen
- Status filtering
- Pagination controls
- Job details panel
- Submit job form

Frontend API client functions:

- `getJobs(...)`
- `getJobById(...)`
- `submitJob(...)`

The frontend currently communicates with the existing Jobs API and does not change backend behavior.

---

## Filtering and Pagination

The `GET /api/jobs` endpoint supports filtering and pagination through query parameters.

Query parameters:

- `status` optional
  - Filters jobs by status

- `page` optional, default: `1`
  - Specifies the page number

- `pageSize` optional, default: `20`, max: `100`
  - Specifies the number of items per page

Examples:

```bash
# Get all completed jobs
curl -i "http://localhost:5008/api/jobs?status=Completed"

# Get first page with 1 item
curl -i "http://localhost:5008/api/jobs?page=1&pageSize=1"

# Get second page
curl -i "http://localhost:5008/api/jobs?page=2&pageSize=1"
```

Invalid pagination values return `400 Bad Request`.

---

## Example API Usage

Submit a job:

```bash
curl -i -X POST http://localhost:5008/api/jobs \
  -H "Content-Type: application/json" \
  -d '{"jobType":"PrintMessage","payload":"{\"message\":\"hello from api\"}","maxRetries":3}'
```

Get all jobs:

```bash
curl -i http://localhost:5008/api/jobs
```

Get a job by id:

```bash
curl -i http://localhost:5008/api/jobs/{jobId}
```

Get failed jobs:

```bash
curl -i http://localhost:5008/api/jobs/failed
```

Cancel a pending job:

```bash
curl -i -X POST http://localhost:5008/api/jobs/{jobId}/cancel
```

---

## Frontend Usage

Install frontend dependencies:

```bash
cd src/Aether.Web
npm install
```

Run frontend tests:

```bash
npm test
```

Build the frontend:

```bash
npm run build
```

Run the frontend locally:

```bash
npm run dev
```

The frontend is expected to call the API at the configured backend address during local development.

---

## Console Demo

The console project provides a simple way to run the system without HTTP.

Included demo handlers:

- `PrintMessageJobHandler`
- `FlakyMessageJobHandler`
- `AlwaysFailingJobHandler`

Demonstrates:

- Successful execution
- Retry with exponential backoff
- Recovery after transient failure
- Permanent failure handling
- Final job state inspection

---

## Features Implemented

Backend:

- Job lifecycle: `Pending → Processing → Completed / Failed / Cancelled`
- Job submission through a dedicated Application service
- Job querying through a clean Application layer
- Job cancellation with domain rules
- Background processing with workers
- Worker pool for parallel processing
- Retry mechanism with exponential backoff
- Queue-based architecture using `Channel`
- Graceful shutdown with cancellation tokens
- Proper cancellation handling during processing
- Failed job tracking
- SQL Server repository support
- Request validation with Data Annotations
- Validation responses using `ValidationProblemDetails`
- Global exception handling
- Clean HTTP responses: `400`, `404`, `409`
- Job submission rate limiting
- Swagger integration

Optimization Domain:

- Dedicated Resource & Shift Orchestrator boundary documented under `docs/internal`
- Minimal scheduling domain model:
  - resources
  - shifts
  - availability windows
  - scheduling problems
- Candidate schedule model using assignments
- Constraint violation model with hard and soft severities
- Basic schedule constraint evaluation:
  - unavailable resource assignment
  - overlapping shifts for the same resource
  - understaffed shifts
- Basic schedule scoring from constraint violations
- Combined schedule evaluation result containing score and violations

Frontend:

- React + TypeScript + Vite frontend
- Typed Jobs API client
- Jobs list view
- Status filter
- Pagination controls
- Job details panel
- Submit job form
- Frontend tests with Vitest and Testing Library

---

## Example Flow

1. A client submits a job using `POST /api/jobs`
2. The API validates the request
3. `JobSubmissionService` creates and stores the job
4. The job ID is pushed into the queue
5. Background workers start processing jobs
6. A worker pulls the job from the queue
7. The appropriate handler executes the job
8. On success, the job becomes `Completed`
9. On failure:
   - retries if allowed
   - otherwise marked as `Failed`
10. A job can be cancelled while still `Pending`
11. Jobs can be queried through the API
12. The frontend can list, filter, page, inspect, and submit jobs

---

## Tests

Backend test coverage includes:

- Domain rules and lifecycle behavior
- Retry and exponential backoff logic
- Queue behavior
- Repository behavior
- SQL Server repository integration tests
- Job submission service
- Job query service
- Job cancellation service
- Worker processing
- Worker pool parallelism
- Cancellation and graceful shutdown
- API endpoints and validation

Frontend test coverage includes:

- Jobs API client
- Job details API client
- Submit job API client
- Jobs list rendering
- Status filtering
- Pagination
- Job details rendering
- Submit job form behavior

Current verified status:

```text
Backend/Core test suite:
Passed: 100
Failed: 0

Frontend:
Tests passing
Production build passing
```

---

## Current Status

The backend job processor is stable and tested.

The frontend provides a first working operational dashboard for the existing Jobs API:

- Submit jobs
- List jobs
- Filter by status
- Move between pages
- View job details

The project has now started the Resource & Shift Orchestrator direction in a controlled way.

Completed optimization groundwork:

- Added an internal optimization boundary map
- Added the first pure Domain model for scheduling problems
- Added candidate schedule representation
- Added constraint violation modeling
- Added basic schedule constraint evaluation
- Added simple schedule scoring
- Added a combined schedule evaluation result

The optimization work is still intentionally limited to the Domain layer.

No Genetic Algorithm implementation has been added yet.

The planned separation remains:

- Job processor: executes and tracks generic background work
- Optimization domain: defines resources, shifts, availability, assignments, violations, and scores
- Future optimization engine: will choose or generate candidate schedules
- Future genetic algorithm engine: will implement population, selection, crossover, mutation, and generations behind a clean abstraction
- Frontend: will later expose dedicated optimization screens after backend contracts are stable

The next recommended slice is to add an optimization result model before introducing any optimization engine abstraction.

---

## Development Principles

This project is developed with the following principles:

- SOLID
- KISS
- YAGNI
- Small slices
- Test first where practical
- No backend changes during frontend-only slices
- No feature expansion before the baseline is green
- Clear separation between domain logic, infrastructure, API, and UI

---

# Rental Application Tracking API

A RESTful ASP.NET Core Web API for submitting rental applications, tracking their status through a review workflow, and recording an audit history of every status change.

## 1. Project Overview

The Rental Application Tracking API lets clients:

- Create a rental application for a property.
- Retrieve a single application by ID.
- List applications with optional filtering by property and status, with pagination.
- Transition an application through its status workflow (for example, `Submitted` ? `UnderReview` ? `Approved`/`Denied`).
- Retrieve the history of status changes for an application.

Status changes and their history entries are written in the same database transaction, and concurrent updates to the same application are guarded by a row version.

## 2. Technology Stack

| Area | Technology |
|---|---|
| Runtime / framework | .NET 8, ASP.NET Core Web API (controllers) |
| Language | C# (Nullable and implicit usings enabled) |
| Application mediation | MediatR 14.2.0 |
| Validation | FluentValidation 12.2.0 (`FluentValidation.DependencyInjectionExtensions` in the API project) |
| Persistence | SQL Server via EF Core 8 (`Microsoft.EntityFrameworkCore.SqlServer`). Run locally with SQL Server LocalDB or SQL Server in Docker. Tests use EF Core InMemory. |
| API documentation | Swashbuckle.AspNetCore 6.6.2, Microsoft.AspNetCore.OpenApi 8.0.19 |
| Testing | xUnit 2.5.3, Microsoft.NET.Test.Sdk 17.8.0, EF Core InMemory 8.x, coverlet.collector 6.0.0 |

## 3. Architecture and Design Decisions

The solution is split into four layers plus a test project:

| Project | Responsibility |
|---|---|
| `HB.RentalApplication.Api` | ASP.NET Core host, controllers, HTTP request contracts, request validators, exception-to-ProblemDetails mapping |
| `HB.RentalApplication.Application` | MediatR commands and queries, their handlers, and response DTOs (`Commands/`, `Queries/`) |
| `HB.RentalApplication.Domain` | Entities (`RentalApplication`, `RentalApplicationHistory`) and the `RentalApplicationStatus` enum |
| `HB.RentalApplication.Infrastructure` | `RentalApplicationDbContext`, repositories, EF Core entity configuration, migrations, DI registration |
| `RentalApplication.Tests` | xUnit tests for command and query handlers |

### CQRS with MediatR

Commands (`CreateApplication`, `TransitionApplication`) and queries (`GetApplicationById`, `GetApplications`, `GetApplicationHistory`) are separate request types, each with its own handler. The controller sends them through `IMediator`. Handlers are registered from the Application assembly with `AddMediatR`.

### Entity Framework Core

- `RentalApplicationDbContext` exposes `RentalApplications` and `RentalApplicationHistories`.
- `RentalApplicationRepository` and `RentalApplicationHistoryRepository` wrap the context and are registered as scoped services.
- Queries use `AsNoTracking()`.
- The `RentalApplication` entity has a `RowVersion` byte array used for optimistic concurrency.

### Validation

- **Request-level validation** (API project): `CreateApplicationRequestValidator`, `TransitionApplicationRequestValidator`, and `GetApplicationsRequestValidator` are registered with FluentValidation and run explicitly in the controller. Failures return `400` with `ValidationProblemDetails`.
- **Command-level validation** (Application project): `CreateApplicationCommandValidator` is registered from the Application assembly and run by the controller before the create command is sent.

### Where business rules and status transitions are enforced

Status transition rules live in `TransitionApplicationCommandHandler` (`HB.RentalApplication.Application/Commands/TransitionApplication/`). The handler:

1. Loads the application by ID. If it is missing, it throws `KeyNotFoundException`.
2. Checks whether the requested transition is allowed from the current status (see section 5). Disallowed transitions throw `InvalidOperationException`.
3. Sets the new status and `UpdatedAtUtc`, stages a history entry, and saves both through the repository.

### Application history

Every successful transition creates a `RentalApplicationHistory` record containing the application ID, `FromStatus`, `ToStatus`, `TimestampUtc`, `Actor`, and an optional `Reason`. The history entry is staged through `RentalApplicationHistoryRepository.AddAsync` and committed by `RentalApplicationRepository.UpdateAsync` in the same `SaveChanges` call, so the status change and its history are saved together or not at all.

### Error handling

`ApplicationExceptionHandler` (registered with `AddExceptionHandler`) maps exceptions to ProblemDetails responses:

| Exception | HTTP status |
|---|---|
| `KeyNotFoundException` | 404 Not Found |
| `InvalidOperationException` (invalid transition or concurrency conflict) | 409 Conflict |
| Other exceptions | 500 Internal Server Error (details hidden) |

`OperationCanceledException` is not handled by this mapper.

## 4. API Endpoints

Base route: `/applications`. Responses use `application/json`.

### `POST /applications` - Create an application

Request body:

```json
{
  "propertyId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "renterEmail": "jane@example.com",
  "householdSize": 2,
  "annualIncome": 85000
}
```

- `householdSize` must be at least 1 and is validated, but it is **not currently persisted**. The controller does not pass it to the command.
- `annualIncome` must be zero or greater.
- Success: `201 Created` with a `Location` header (`/applications/{applicationId}`) and a `CreateApplicationResponse` body.
- Validation failure: `400 Bad Request` with `ValidationProblemDetails`.

### `GET /applications/{id}` - Get an application by ID

- `id` is a GUID route parameter.
- Success: `200 OK` with a `GetApplicationByIdResponse` body.
- Not found: `404 Not Found` with `ProblemDetails`.

### `GET /applications` - List applications

Query parameters:

| Name | Type | Default | Rules |
|---|---|---|---|
| `propertyId` | GUID | none | Optional filter |
| `status` | `RentalApplicationStatus` | none | Optional; must be a valid enum value |
| `page` | int | `1` | Must be `>= 1` |
| `pageSize` | int | `10` | Must be between `1` and `100` |

Example: `GET /applications?status=Submitted&page=1&pageSize=20`

Success: `200 OK` with a `GetApplicationsResponse` body containing `applications`, `page`, `pageSize`, and `totalCount`. Results are ordered by `CreatedAtUtc` descending.

Validation failure: `400 Bad Request` with `ValidationProblemDetails`.

### `POST /applications/{id}/transitions` - Change application status

Request body:

```json
{
  "to": "UnderReview",
  "reason": "Documents received",
  "actor": "owner"
}
```

- `to` is a `RentalApplicationStatus` value. It is required and must be a valid enum value.
- `actor` is required. It is used to authorize the renter-only withdrawal rule.
- `reason` is optional and is stored in the history entry.

Success: `200 OK` with a `TransitionApplicationResponse` body:

```json
{
  "applicationId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "status": "UnderReview",
  "transitionedAtUtc": "2024-01-16T09:00:00Z"
}
```

Other responses:

- `400 Bad Request`: validation failure (`ValidationProblemDetails`).
- `404 Not Found`: application does not exist.
- `409 Conflict`: transition not allowed, application is in a final state, or a concurrent update was detected.

> **Security note:** `actor` is taken from the request body and is **not authenticated**. No authentication or authorization is configured. Derive the actor from the caller's identity before production use.

### `GET /applications/{id}/history` - Get application history

- `id` is a GUID route parameter.
- Success: `200 OK` with a `GetApplicationHistoryResponse` body.
- Not found: `404 Not Found` with `ProblemDetails`.

## 5. Application Status Workflow

Statuses: `Submitted`, `UnderReview`, `Approved`, `Denied`, `Withdrawn`.

| From | To | Allowed by |
|---|---|---|
| `Submitted` | `UnderReview` | Any actor |
| `UnderReview` | `Approved` | Any actor |
| `UnderReview` | `Denied` | Any actor |
| `Submitted` or `UnderReview` | `Withdrawn` | Actor `renter` only (case-insensitive) |

- `Approved`, `Denied`, and `Withdrawn` are final states. Any transition from them is rejected.
- Any other transition is rejected with `409 Conflict`.

## 6. Prerequisites and Configuration

### Prerequisites

- .NET 8 SDK
- SQL Server or SQL Server LocalDB (Windows)
- EF Core CLI tools, required only to apply migrations:

  ```powershell
  dotnet tool install --global dotnet-ef
  ```

### Configuration

The connection string is read from `ConnectionStrings:DefaultConnection` in `HB.RentalApplication.Api/appsettings.json`:

```json
"ConnectionStrings": {
  "DefaultConnection": "Server=(localdb)\\mssqllocaldb;Database=RentalApplicationDb;Trusted_Connection=True;MultipleActiveResultSets=true"
}
```

To override it without editing the file, use user secrets or an environment variable such as `ConnectionStrings__DefaultConnection`. Do not commit production credentials.

`appsettings.Development.json` only adjusts logging. Swagger UI is enabled only when the environment is `Development`.

## 7. How to Run

Run these commands from the repository root (`HB.RentalApplication`).

```powershell
# Restore dependencies
dotnet restore HB.RentalApplication.Api/HB.RentalApplication.Api.csproj

# Build the solution
dotnet build

# Apply database migrations (Infrastructure holds the migrations; Api is the startup project)
dotnet ef database update --project HB.RentalApplication.Infrastructure --startup-project HB.RentalApplication.Api

# Run the API
dotnet run --project HB.RentalApplication.Api
```

Swagger UI is available at `/swagger` when running in the Development environment. The URL and port are shown in the console output when the API starts.

## 8. How to Run Tests

The test project is `RentalApplication.Tests` (xUnit, EF Core InMemory). It covers:

- `Commands/CreateApplicationCommandHandlerTests.cs`
- `Commands/TransitionApplicationCommandHandlerTests.cs`, including valid and invalid transitions, final-state rejection, history persistence, and a concurrency test
- `Queries/GetApplicationByIdQueryHandlerTests.cs`
- `Queries/GetApplicationsQueryHandlerTests.cs`
- `Queries/GetApplicationHistoryQueryHandlerTests.cs`
- `Endpoints/ApplicationsEndpointTests.cs`, which runs the API in-process with `WebApplicationFactory` (EF Core InMemory replaces SQL Server) and checks 200, 404, 409, and 400 responses for transitions.

Run all tests:

```powershell
dotnet test RentalApplication.Tests/RentalApplication.Tests.csproj
```

The tests do not require a SQL Server instance because they use the EF Core InMemory provider.

## 9. Main Design Decisions

- **Layered structure:** Keeps HTTP concerns in the API, use-case logic in Application, and persistence details in Infrastructure, so business rules do not depend on ASP.NET Core or SQL Server.
- **CQRS with MediatR:** Separates state-changing commands from read-only queries and keeps each use case in a single handler.
- **EF Core with SQL Server:** Provides a mature ORM with migrations, and the `RowVersion` column gives optimistic concurrency for competing status updates.
- **FluentValidation:** Keeps validation rules declarative and out of controllers and handlers.
- **Explicit status workflow in the handler:** A single, readable place for transition rules, which are covered by unit tests.
- **Atomic history recording:** Writing the status change and its history entry in one save ensures the audit trail always matches the current status.
- **ProblemDetails error mapping:** Gives clients consistent error responses without exception-handling code in each controller action.

## 10. Roadmap and Not Yet Implemented

### Security and access control

- Authentication and authorization. The actor is self-declared.
- Enforcing that the actor is a specific owner or renter for the given application. Only the literal actor `renter` is checked for withdrawal, and any actor can approve or deny.
- Ownership-based filtering in list queries (see the note in section 4).

### Features and data

- Persisting `householdSize` on create.

### Performance

- Caching of frequently read data (for example, application lookups by ID and list queries). Cache invalidation must be handled on every status transition and update.

### Architecture

- Moving status transition rules out of `TransitionApplicationCommandHandler` and into the Domain layer (for example, on `RentalApplication` or a dedicated transition policy). Business rules should live in Domain so the handler only orchestrates, which gives a clear domain boundary and makes the rules testable without the Application or Infrastructure layers.

## 11. AI Use

- **What I used it for:** Generating the API endpoints and controller actions, scaffolding MediatR handlers, drafting FluentValidation rules, writing xUnit tests, and explaining EF Core concurrency with `RowVersion`. The code was written through AI assistance and then reviewed and adjusted.
- **What I changed:** Renamed members to match the layer conventions, adjusted validation limits, and rewrote generated tests to use the InMemory provider correctly.
- **What I rejected:** Suggested authentication middleware that was out of scope, and a generic repository that duplicated the existing repositories. I also rejected the AI's suggestion to move the status transition rules into the Domain layer (a DDD-style approach). It would have split the workflow logic between the Domain and the handler, so I kept the rules in `TransitionApplicationCommandHandler`.



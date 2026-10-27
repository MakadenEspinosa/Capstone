# E-S Events

E-S Events is a capstone project for account management and, ultimately, event coordination. The current working MVP focuses on registration, sign-in, and user profile administration. Event-related domain models exist, but event creation and management are not available in the UI or API yet.

## Current functionality

- Register an account with an email address, name, and password.
- Sign in and out using cookie-based authentication.
- View and update your own profile, or permanently delete your account.
- As an administrator, view, update, and delete user accounts.
- Access user-management endpoints protected by authentication and authorization.
- Persist application and identity data in PostgreSQL.

Newly registered accounts receive the standard `User` role. Administrator-only features are implemented, but the project does not currently include an administrator bootstrap or role-management workflow.

### Web pages

| Page | Access |
| --- | --- |
| `/ui/login` (also `/`) | Public |
| `/ui/register` | Public |
| `/ui/user` | Signed-in users |
| `/ui/users` | Administrators |

### HTTP API

| Method | Route | Access |
| --- | --- | --- |
| `GET` | `/api/auth/csrf` | Public; obtains an antiforgery token |
| `POST` | `/api/auth/login` | Public |
| `POST` | `/api/auth/logout` | Signed-in users |
| `GET` | `/api/auth/session` | Signed-in users |
| `POST` | `/api/users` | Public registration |
| `GET` | `/api/users` | Administrators |
| `GET` | `/api/users/{id}` | Account owner or administrator |
| `PUT` | `/api/users/{id}` | Account owner or administrator |
| `DELETE` | `/api/users/{id}` | Account owner or administrator |

State-changing requests must include a valid antiforgery token. For API clients, first call `GET /api/auth/csrf`, retain the antiforgery cookie, and send the returned `RequestToken` in the `X-CSRF-TOKEN` header on `POST`, `PUT`, and `DELETE` requests.

The OpenAPI document is available at `/openapi/v1.json` in the Development environment.

## Technology

- .NET 10 / ASP.NET Core
- Blazor Interactive Server and MudBlazor
- ASP.NET Core Identity
- Entity Framework Core with PostgreSQL
- Docker Compose and NGINX for containerized deployment
- xUnit for domain tests

## Repository structure

```text
src/
  ESEvents.Domain/         Domain entities, value objects, and rules
  ESEvents.Application/    User services and application contracts
  ESEvents.Infrastructure/ Identity, persistence, and migrations
  ESEvents.Web/            Blazor UI and HTTP API
tests/
  ESEvents.Domain.Tests/   Domain unit tests
```

## Run with Docker Compose

Requirements: Docker Desktop (or Docker Engine) with the Compose plugin, plus the .NET 10 SDK and `dotnet-ef` for applying the database migrations.

1. Create a `.env` file in the repository root. This file is ignored by Git.

   ```dotenv
   POSTGRES_USER=postgres
   POSTGRES_PASSWORD=replace-with-a-local-password
   POSTGRES_DB=esevents
   ```

2. Start PostgreSQL and apply the database migrations before starting the application:

   ```powershell
   docker compose up -d postgresql
   ```

   Follow steps 3–5 in [Run locally](#run-locally) to set the connection string and run `dotnet ef database update`.

3. Start the application stack:

   ```powershell
   docker compose up --build
   ```

4. Open [http://localhost:8080](http://localhost:8080).

Compose runs PostgreSQL, the web application, and an NGINX reverse proxy. PostgreSQL is published on `localhost:5433`; the application is exposed through NGINX on `localhost:8080`. PostgreSQL data and ASP.NET Core data-protection keys are stored in named Docker volumes. Entity Framework Core migrations are not automatically applied at startup.

## Run locally

Requirements: .NET 10 SDK and Docker Compose (for the PostgreSQL service).

1. Create the `.env` file described above.
2. Start PostgreSQL:

   ```powershell
   docker compose up -d postgresql
   ```

3. Set the connection string for the web project in PowerShell. Enter the same password used in `.env` when prompted:

   ```powershell
   $dbPassword = Read-Host "PostgreSQL password"
   $connectionString = "Host=localhost;Port=5433;Database=esevents;Username=postgres"
   $connectionString += ";Pass" + "word=$dbPassword"
   $env:ConnectionStrings__Default = $connectionString
   ```

4. If you do not already have the Entity Framework CLI, install it:

   ```powershell
   dotnet tool install --global dotnet-ef --version 10.0.13
   ```

5. Apply the database migrations:

   ```powershell
   dotnet ef database update --project src/ESEvents.Infrastructure --startup-project src/ESEvents.Web
   ```

6. Run the web application:

   ```powershell
   dotnet run --project src/ESEvents.Web
   ```

Use the local URL printed by `dotnet run`. The connection string must be set in the same shell before running the migration and application commands.

## Tests

Run the solution's tests with:

```powershell
dotnet test ESEvents.sln
```

The current test project covers domain behavior; web and API integration tests are not included yet.

## Project status

This is a work in progress. The current MVP delivers user account and profile management. Although the domain layer contains event and participant models, there are not yet event-management pages, application services, or API endpoints.

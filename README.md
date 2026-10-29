# HR Portal — ASP.NET Core 8 Web API

> REST API powering the **HR Portal**: authentication, employees, departments, leave management, attendance and payroll — built with ASP.NET Core 8, EF Core and SQLite.

![.NET](https://img.shields.io/badge/.NET-8-512BD4?logo=dotnet&logoColor=white)
![C#](https://img.shields.io/badge/C%23-239120?logo=csharp&logoColor=white)
![SQLite](https://img.shields.io/badge/SQLite-003B57?logo=sqlite&logoColor=white)
![JWT](https://img.shields.io/badge/Auth-JWT-000000?logo=jsonwebtokens&logoColor=white)
![Swagger](https://img.shields.io/badge/Docs-Swagger-85EA2D?logo=swagger&logoColor=black)

Frontend: **[hr-portal-angular](https://github.com/zkhattak7600/hr-portal-angular)** (Angular 18 + TypeScript + Tailwind CSS)

---

## 🚀 Live Demo

| | Link |
|---|---|
| **App** | https://hr-portal-angular.vercel.app |
| **API (Swagger)** | https://hr-portal-api.onrender.com/swagger |

On the login page click **Try as Admin** or **Try as Employee**, or sign in manually:

| Role | Email | Password |
|---|---|---|
| Admin | `demo.admin@hrportal.demo` | `Demo@123` |
| Employee | `demo.employee@hrportal.demo` | `Demo@123` |

> ⏳ The API runs on Render's free tier and sleeps when idle — the **first request can take ~50 seconds** to wake it up.
> In the demo, delete actions and password resets are disabled and the data resets whenever the server restarts.

---

## Features
- **JWT authentication** — register, login, forgot / reset password; passwords hashed with **BCrypt**
- **Role-based access** — Admin and Employee roles
- **Employees & departments** — CRUD with pagination
- **Leave management** — apply, approve / reject, per-type leave entitlements and balances
- **Attendance** — check-in / check-out, late and half-day rules (configurable)
- **Payroll** — payslip generation and history
- **Self-service (`/api/me`)** — employee profile, leaves, attendance, payslips
- **Profile photo uploads** stored under `wwwroot/uploads`
- **Swagger / OpenAPI** UI with JWT support
- **EF Core + SQLite** with automatic database creation and seeding

## Tech stack
| Area | Technology |
|---|---|
| Framework | ASP.NET Core 8 Web API |
| Language | C# |
| ORM / DB | Entity Framework Core 8, SQLite |
| Auth | JWT Bearer, BCrypt.Net-Next |
| Docs | Swashbuckle (Swagger) |

## Getting started

### Prerequisites
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)

### Configure the JWT key
The committed `appsettings.json` contains only a placeholder. Provide your own secret (at least 64 random characters) in one of these ways:

```bash
# Option A: local settings file (git-ignored)
cp appsettings.Development.example.json appsettings.Development.json   # then edit Jwt:Key

# Option B: .NET user-secrets
dotnet user-secrets init
dotnet user-secrets set "Jwt:Key" "<your-long-random-secret>"

# Option C: environment variable
Jwt__Key=<your-long-random-secret>
```

### Run
```bash
git clone https://github.com/zkhattak7600/hr-portal-api.git
cd hr-portal-api
dotnet restore
dotnet run --urls http://localhost:5000
```
- API base URL: <http://localhost:5000>
- Swagger UI: <http://localhost:5000/swagger>

The SQLite database (`employeeleave.db`) is created and seeded automatically on first run.

## API overview
| Controller | Purpose |
|---|---|
| `AuthController` | Register, login, forgot / reset password |
| `EmployeesController` | Employee CRUD, paging, photo upload |
| `DepartmentsController` | Department CRUD |
| `LeavesController` | Leave requests and approvals |
| `AttendanceController` | Attendance records |
| `PayrollController` | Payslips |
| `DashboardController` | Admin dashboard statistics |
| `MeController` | Employee self-service endpoints |

## Project structure
```
Controllers/   # API endpoints
Data/          # AppDbContext, DbSeeder
DTOs/          # request / response contracts
Models/        # entity classes
Services/      # TokenService, PhotoStorage
Program.cs     # DI, auth, CORS, Swagger setup
```

## Author
**Zain Khattak** — [@zkhattak7600](https://github.com/zkhattak7600)

## Deployment (Render, Docker)
- `Dockerfile` — multi-stage build (.NET 8 SDK → ASP.NET runtime), listens on `$PORT` (default 8080), stores SQLite in `/app/data`.
- `render.yaml` — Render Blueprint for a free web service. It sets `ASPNETCORE_ENVIRONMENT=Production`, generates a random `Jwt__Key`, enables `DemoMode`, and allows the Vercel frontend via `Cors__AllowedOrigins__0`.
- `DemoMode=true` seeds ~100 employees with a month of attendance, leave requests and payslips plus the demo logins, and blocks all `DELETE` requests and password resets (HTTP 403 "Disabled in demo").
- Locally nothing changes: `DemoMode` is `false` and CORS allows `http://localhost:4200` by default (see `appsettings.json`).

Free-tier note: Render's free disk is ephemeral, so the demo database is recreated on every deploy/restart.

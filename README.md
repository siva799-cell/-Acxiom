# AcxiomCRM

## Current Application: Django

The current application is the Python/Django implementation at the workspace root. It includes Customer Management, Lead Management with transactional lead conversion, Follow-Up Management, and Activity Management. The previous ASP.NET sources remain under `AcxiomCRM/` as legacy reference and are not required to run Django.

### Local setup

```powershell
python -m pip install -r requirements.txt
Copy-Item .env.example .env
python manage.py check
python manage.py makemigrations
python manage.py migrate
python manage.py test
python manage.py runserver 127.0.0.1:3000
```

Local development uses SQLite when `DB_NAME` is unset. To use PostgreSQL, set `DB_NAME`, `DB_USER`, `DB_PASSWORD`, `DB_HOST`, and `DB_PORT` in the ignored `.env` file. Set a real `DJANGO_SECRET_KEY`, `DJANGO_DEBUG=False`, and production `DJANGO_ALLOWED_HOSTS` before deployment. Production deployments should configure a shared cache through `DJANGO_CACHE_BACKEND` and `DJANGO_CACHE_LOCATION` so account lockouts work across application processes.

The `Admin`, `Manager`, and `SalesExecutive` Django groups are created during migrations. Superusers are treated as Admin. To assign an existing user a role, use Django's shell:

```python
from django.contrib.auth import get_user_model
from django.contrib.auth.models import Group

user = get_user_model().objects.get(username="existing-username")
user.groups.add(Group.objects.get(name="SalesExecutive"))
```

No CRM records or KPI values are seeded in Phase 2. The requirements include Django REST Framework for later phases; no API endpoints are registered yet.

### CRM access rules

Admin and Manager roles can access all business records; Manager assignment choices are limited to SalesExecutive accounts. SalesExecutives can access customers they created and leads/follow-ups/activities assigned to or created by them, with related records also checked for ownership. There is no manager-to-team hierarchy in this phase, so Manager access is business-wide. Audit events store only actor, event type, model label, object ID, and timestamp; no user-entered content or credentials are copied into the audit table.

## Legacy ASP.NET Implementation Documentation

The following documentation describes the retained .NET implementation only. It is not used by the current Django application.

AcxiomCRM is a role-aware ASP.NET Core MVC CRM academic project using SQL Server, EF Core, ASP.NET Core Identity, and Razor/Bootstrap views.

## Features

- Identity authentication, password policy, account lockout, active-user checks, and Admin/Manager/SalesExecutive roles.
- Customer, lead, opportunity, follow-up, and activity workflows backed by SQL Server.
- Team-scoped access for Managers and ownership-scoped access for Sales Executives.
- Transactional qualified-lead conversion, server-side validation, append-only audit history, reports, CSV output, and protected REST APIs.
- Development-only demo data and dashboard charts sourced from authorized database queries.

## Stack

- .NET 10 / ASP.NET Core MVC
- Entity Framework Core 10 / SQL Server
- ASP.NET Core Identity
- Razor, Bootstrap 5, JavaScript, Chart.js
- Built-in OpenAPI JSON document in Development at `/openapi/v1.json`

## Prerequisites

- .NET SDK 10.0.4xx or later in the 10.0 feature band (the project was built with SDK 10.0.401).
- SQL Server, SQL Server Express, or SQL Server LocalDB.
- SQL Server Management Studio or another SQL client is useful but optional.

The current authoring environment has the .NET 10 runtime but no installed SQL Server/LocalDB engine. Database migration execution, demo-account creation, and end-to-end authentication therefore remain unverified here.

## Configuration

The default development connection string targets `(localdb)\\MSSQLLocalDB`. Install SQL Server LocalDB or change `ConnectionStrings:DefaultConnection` to your SQL Server instance. For SQL authentication, keep the password in User Secrets or an environment variable, not in tracked settings.

Initialize User Secrets and set a local connection string:

```powershell
dotnet user-secrets init --project .\AcxiomCRM\AcxiomCRM.csproj
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=(localdb)\MSSQLLocalDB;Database=AcxiomCRMDb;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True" --project .\AcxiomCRM\AcxiomCRM.csproj
```

To create development demo accounts, set `SeedUsers:Enabled` to `true` in local Development configuration, and set passwords only through User Secrets. Each password must meet the Identity policy (at least 8 characters, upper/lowercase, digit, and non-alphanumeric character):

```powershell
dotnet user-secrets set "SeedUsers:Admin:Password" "<local-demo-password>" --project .\AcxiomCRM\AcxiomCRM.csproj
dotnet user-secrets set "SeedUsers:Manager:Password" "<local-demo-password>" --project .\AcxiomCRM\AcxiomCRM.csproj
dotnet user-secrets set "SeedUsers:SalesExecutive:Password" "<local-demo-password>" --project .\AcxiomCRM\AcxiomCRM.csproj
dotnet user-secrets set "SeedUsers:Enabled" "true" --project .\AcxiomCRM\AcxiomCRM.csproj
```

Demo usernames are `admin@acxiomcrm.local`, `manager@acxiomcrm.local`, and `sales@acxiomcrm.local`. No demo passwords are committed. The configured Manager is assigned the Sales Executive demo account.

## Database and Run

The application applies migrations and idempotent role/user/demo-data initialization at startup. For explicit migration management:

```powershell
dotnet restore .\AcxiomCRM\AcxiomCRM.csproj
dotnet ef database update --project .\AcxiomCRM\AcxiomCRM.csproj --startup-project .\AcxiomCRM\AcxiomCRM.csproj
dotnet build .\AcxiomCRM.slnx
dotnet run --project .\AcxiomCRM\AcxiomCRM.csproj
```

The initial migration is `InitialIdentityAndCrm`. The project can be opened through `AcxiomCRM.slnx`.

## Authentication and permissions

- Login: `/Account/Login`; logout is a POST with antiforgery protection.
- Self-service password change: `/Account/ChangePassword`.
- Admin manages users/roles/audit. Manager reads and assigns within directly managed Sales Executive scope. Sales Executives work within their own assigned scope.
- Public registration is disabled; Admin creates accounts.
- Identity lockout is five failed attempts for 15 minutes. Production auth cookies require HTTPS.

## MVC modules

Dashboard, customers, leads and conversion, opportunities, follow-ups, activities, user administration, role overview, audit log, and reports. Core workflows use server-side validation and paged queries. Reports include customer, lead, follow-up, opportunity, pipeline, conversion, user activity, and admin-only audit; report tables support CSV export.

## REST APIs

Cookie-authenticated endpoints:

- `GET /api/auth/csrf`, `POST /api/auth/login`, `POST /api/auth/logout`
- `GET|POST /api/customers`, `GET|PUT|DELETE /api/customers/{id}`
- `GET|POST /api/leads`, `GET|PUT /api/leads/{id}`, `POST /api/leads/{id}/convert`
- `GET|POST /api/opportunities`, `GET|PUT /api/opportunities/{id}`
- `GET|POST /api/followups`, `PUT /api/followups/{id}`
- `GET /api/reports/pipeline`, `/lead-status`, `/monthly-sales`

POST/PUT/DELETE API requests use `X-CSRF-TOKEN` from `/api/auth/csrf` for cookie authentication. API model validation returns a consistent JSON error envelope. In Development, OpenAPI JSON is available at `/openapi/v1.json`.

## Security

Identity hashes passwords. The application configures complexity and lockout, secure/HttpOnly/SameSite cookies, antiforgery, role and team/ownership checks, DTO API boundaries, EF parameterized queries, safe API errors, and append-only audit entries. Never place production credentials in appsettings or source control.

## Testing

```powershell
dotnet restore .\AcxiomCRM.slnx
dotnet build .\AcxiomCRM.slnx
dotnet test .\AcxiomCRM.slnx
```

A manual acceptance checklist and implementation traceability are in [REQUIREMENTS-CHECKLIST.md](REQUIREMENTS-CHECKLIST.md). Database-dependent checks require a running SQL Server instance and configured local seed secrets.

## Project structure

```text
AcxiomCRM.slnx
AcxiomCRM/
  Api/Controllers/       REST endpoints
  Authorization/         role names and team access scope
  Controllers/            MVC request handling
  Data/                   Identity DbContext, initializer, migrations
  DTOs/                   API request/response contracts
  Models/                 Identity and CRM entities
  Services/               persistence workflows, validation, reports, audit
  ViewModels/             MVC form and dashboard models
  Views/                  Razor pages and shared shell
  wwwroot/                Bootstrap assets, CSS, JavaScript
```

## Screenshots

_Add application screenshots after connecting a local SQL Server instance and seeding fictional development data._

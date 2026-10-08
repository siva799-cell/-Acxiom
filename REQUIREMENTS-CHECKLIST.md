# AcxiomCRM Requirement Traceability

Status meanings: **Implemented** = connected in source; **Build verified** = compiled locally; **Environment blocked** = requires SQL Server/LocalDB; **Partial** = documented scope remains.

| Requirement | Implementation | File/module | Status | Test/check |
|---|---|---|---|---|
| ASP.NET Core MVC / .NET 10 | MVC app and solution | `AcxiomCRM.csproj`, `Program.cs` | Build verified | `dotnet build` PASS |
| SQL Server + EF Core | SQL Server provider and SQL Server migration; EF confirms snapshot/model match | `ApplicationDbContext`, `Migrations` | Environment blocked | `has-pending-model-changes` PASS; database update blocked by absent LocalDB |
| Identity user and roles | `ApplicationUser`, Identity role manager, Admin/Manager/SalesExecutive | `Models/ApplicationUser.cs`, `Data/DbInitializer.cs` | Build verified | Build PASS; database seeding unrun |
| Password policy and lockout | 8+ chars, mixed case, digit, symbol; 5 attempts/15 min | `Program.cs` | Build verified | Configuration inspected; runtime login test blocked |
| Secure login/logout/password change | Identity sign-in, antiforgery, active checks | `AccountController`, `Views/Account` | Build verified | Build PASS; DB login test blocked |
| Development demo users/data | Configured secrets only; deterministic development-only sample entities | `Data/DbInitializer.cs` | Environment blocked | Requires SQL Server + User Secrets |
| Responsive application shell | Role-aware MVC layout and responsive styles | `Views/Shared/_Layout.cshtml`, `wwwroot/css/site.css` | Build verified | Razor build PASS |
| Server-side team/owner authorization | Admin global scope; Manager and Sales Executive constrained by manager tree/owner | `Authorization/CrmAccessScope.cs`, CRM services | Build verified | Build PASS; integration test requires DB |
| Customer CRUD/search/duplicate/deactivate | Paged query, unique filtered email/phone, duplicate check, admin soft deactivate | `CustomerService`, `CustomersController` | Build verified | Build PASS; database behavior unrun |
| Customer record history | Related activity/follow-up/opportunity history is not yet shown on customer details | `Views/Customers/Details.cshtml` | Partial | Not implemented |
| Lead workflow/conversion | Defined transitions and transactional qualified conversion | `LeadService`, `LeadsController` | Build verified | Build PASS; transaction test requires SQL Server |
| Opportunity rules/pipeline | Amount > 0, probability 0–100, active close date, stage graph, server weighted amount | `OpportunityService` | Build verified | Build PASS; runtime test blocked |
| Follow-ups and activities | Date/type/related-record checks, ownership, completion/reschedule audit | `FollowUpService`, `ActivityService` | Build verified | Build PASS; runtime test blocked |
| User and role administration | Admin-only user/role management, activation, lockout unlock, password reset, team assignment | `UsersController`, `RolesController` | Build verified | Build PASS; runtime test blocked |
| Audit log and immutability | Login/business events, paged admin filters, DbContext blocks update/delete | `AuditService`, `AuditController`, `ApplicationDbContext` | Build verified | Build PASS; runtime persistence unrun |
| REST APIs and DTOs | Auth, customers, leads, opportunities, follow-ups, reports | `Api/Controllers`, `DTOs` | Build verified | Build PASS; HTTP integration tests absent |
| API validation/error responses | `[ApiController]` model errors and consistent envelopes | `Program.cs`, API controllers | Build verified | Build PASS; endpoint test unrun |
| Reports and CSV | Scoped customer/lead/follow-up/opportunity/pipeline/conversion/activity/audit queries | `ReportService`, `ReportsController` | Build verified | Build PASS; SQL query test unrun |
| Dashboard and charts | Scoped DB KPIs, period filters, Chart.js endpoints | `DashboardController`, report APIs, dashboard view | Build verified | Razor build PASS; runtime rendering unrun |
| Role-specific dashboard extras | Shared scoped metrics/charts work; admin user/security cards and recent-activity panels are not implemented | Dashboard | Partial | Not implemented |
| Activity editing | Activity creation and scoped list are implemented; edit/update workflow is not yet available | `ActivityService`, `ActivitiesController` | Partial | Not implemented |
| Table sorting | Lists use stable server-side ordering and pagination; user-selectable sorting is not yet implemented | CRM list controllers/services | Partial | Not implemented |
| API docs | ASP.NET Core OpenAPI JSON in Development | `Program.cs`, project package | Build verified | Build PASS; URL requires app/database startup |
| Secrets and Git hygiene | User Secrets instructions; example config; ignored env/db/build files | `README.md`, `appsettings.example.json`, `.gitignore` | Implemented | Source inspected; no passwords in tracked config |
| Safe error handling | Generic MVC pages and sanitized API errors | `HomeController`, `Program.cs` | Build verified | Build PASS; runtime error simulation unrun |
| Full end-to-end acceptance | Real database, login/roles/CRUD/APIs/reports | Whole application | Environment blocked | SQL Server absent; see Known limits |

## Manual acceptance checklist

- [ ] Start SQL Server/LocalDB and configure `DefaultConnection` in User Secrets.
- [ ] Configure all three demo passwords and enable `SeedUsers` in Development.
- [ ] Run `dotnet ef database update`; verify Identity and CRM tables in SQL Server.
- [ ] Start the app; confirm three roles, users, team link, and fictional records are seeded once.
- [ ] Verify login, invalid credentials, five-attempt lockout, inactive user rejection, password change, and logout.
- [ ] Verify anonymous dashboard redirects to login; Admin sees all; Manager sees only own/direct-report data; Sales Executive sees own assigned records.
- [ ] Verify customer duplicate, email/phone validation, and deactivation rules.
- [ ] Verify lead transitions and transactional conversion.
- [ ] Verify opportunity amount/probability/date/stage rules and weighted pipeline.
- [ ] Verify follow-up date/type/status transitions, activity scope, and audit records.
- [ ] Verify API 200/201/400/401/403/404/409, CSRF, validation errors, and DTO serialization.
- [ ] Verify date-filtered reports/charts and CSV output against database records.

## Known limits and remaining work

- No SQL Server, LocalDB runtime, SQL command-line client, or Docker installation was detected. `dotnet ef database update` cannot create a database here.
- EF Core reports no pending model changes against the initial migration snapshot. Database update was attempted but cannot connect because the LocalDB runtime is absent.
- `dotnet run` was attempted and stops during database initialization for the same LocalDB error, before opening an HTTP listener.
- Customer history, activity editing, selectable column sorting, extra role-specific dashboard panels, and an automated integration test project remain incomplete.
- No database-backed automated integration test suite was created or run. Build success does not certify database connectivity, login, roles, migrations, or runtime workflows.

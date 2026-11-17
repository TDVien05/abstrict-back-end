# ABSTRICT — Backend

REST API for a platform to find and book hourly home-help services in Ho Chi Minh City. Customers book freelancers or companies, pay a deposit, check in via QR, accept the work, leave two-way ratings, file disputes, and partners can buy visibility plans.

- **Live demo:** https://abstrict.vientruong.site (frontend) · https://abstrict.api.vientruong.site (API)
- **Frontend repo:** https://github.com/TDVien05/abstrict-front-end

## Tech stack

| Layer | Technology |
| --- | --- |
| Runtime | .NET 8 (SDK `8.0.425`, see `global.json`), ASP.NET Core Web API |
| Data | PostgreSQL 16, EF Core 8 + Npgsql, migrations in `src/Abstrict.Api/Data/Migrations` |
| Auth | Phone number + OTP registration/login, JWT access token (15 min), `HttpOnly` refresh-token cookie (30 days) |
| Payments | `Sandbox` gateway (default for dev) and SePay integration |
| Other | Swagger, booking-expiry background job, local file storage (KYC, check-in photos, dispute evidence) |
| Testing | xUnit (`tests/Abstrict.Api.Tests`) |
| Deployment | Docker / Docker Compose, GitHub Actions CI (`.github/workflows/ci.yml`) |

## Project structure

```text
src/Abstrict.Api/
  Controllers/      # Auth, Customer, Freelancer(s), Partner, Bookings, Payments,
                    # Disputes, Admin, CompanyOnboarding, Catalog, Health ...
  Services/         # Interfaces/ + Implementations/ (business logic, seeders)
  DTOs/             # Requests/ and Responses/
  Models/           # Entities/ and Enums/
  Data/             # AppDbContext, Configurations/, Migrations/, Reference/
  Integrations/     # Payments, Storage, Notifications
  BackgroundJobs/   # BookingExpiryJob
  Options/          # Typed configuration (Booking, Kyc, Payment, AddressKit)
  Program.cs        # DI, middleware, startup seeding
tests/Abstrict.Api.Tests/   # Unit tests for business policies and architecture rules
scripts/                    # PowerShell scripts that create sample data
docs/                       # Per-module API docs
```

Flow: `Controller → Service → AppDbContext`. More documentation: `PROJECT_IDEA.md` (business rules), `BACKEND_ARCHITECTURE.md`, `docs/FEATURES.md` (endpoint list), `docs/DOCKER.md`.

Conventions: base URL `/api/v1`; IDs are UUIDs; timestamps are UTC (ISO 8601); money is an integer amount in VND; errors are `ProblemDetails` with a stable `code`.

## Testing on the live demo

1. Open https://abstrict.vientruong.site.
2. Log in with a phone number and password from the accounts below, or register a new account (customer, freelancer or company).
3. Check that the API is up: `GET https://abstrict.api.vientruong.site/health`.

### Test accounts

Demo data is created by `DemoDataSeeder`; the demo accounts share one password, set on the server through the `Demo__SeedPassword` variable (not committed to this repo).

| Role | Phone number | Notes |
| --- | --- | --- |
| Admin | `0900000001` | Approves KYC/companies, manages plans, resolves disputes |
| Customer | `0920000001` | Test customer, District 1 address |
| Customer | `0920000002` – `0920000006` | Have sample orders for rating tests (if `seed-rating-fixtures.ps1` was run) |
| Freelancer (approved) | `0910000001` – `0910000005` | 14 days of availability, bookable. `0910000001` = Nguyễn Thị Lan (cleaning) |
| Freelancer (Submitted) | `0910000006` | Waiting for admin KYC approval |
| Freelancer (UnderReview) | `0910000007` | Under review |
| Freelancer (Draft) | `0910000008` | Profile not finished |

> **Password:** `<fill in the current demo password>` — ask the repo owner. Do not commit real passwords to this README.

### Suggested test flow

1. Customer `0920000001` → browse freelancers → pick an available slot → book → pay the 15% deposit (sandbox gateway).
2. Freelancer `0910000001` → confirm the booking → scan the customer's check-in QR → upload before/after photos → submit for acceptance.
3. Customer accepts the work → rates within 48 hours, or opens a dispute.
4. Admin `0900000001` → resolves disputes, approves the KYC of freelancer `0910000006`, manages visibility plans.

In the Development environment the API returns `developmentOtpCode` in the response, so registration can be tried without SMS.

## Local setup

### Requirements

- .NET SDK 8.0.x, PostgreSQL 16 (or Docker), Git

### Option 1 — Docker Compose (recommended)

```powershell
Copy-Item .env.docker.example .env.docker   # change the passwords in this file
docker compose --env-file .env.docker up -d --build
```

- API: http://localhost:8080 · Health: `/health` · Swagger: `/swagger`
- pgAdmin: http://localhost:5050
- Migrations are applied automatically (`Database__ApplyMigrations=true`). Stop with `docker compose --env-file .env.docker down`. See `docs/DOCKER.md` for more.

### Option 2 — Run directly with .NET

```powershell
# 1. Create a PostgreSQL database named "abstrict". The default in appsettings.json is postgres/postgres,
#    or override it with an environment variable:
$env:ConnectionStrings__DefaultConnection = "Host=localhost;Port=5432;Database=abstrict;Username=postgres;Password=postgres"
$env:Database__ApplyMigrations = "true"

# 2. Run the API (Development: OTP exposed in responses, Sandbox payments, dev admin seeded)
dotnet run --project src/Abstrict.Api
```

Ports are defined in `src/Abstrict.Api/Properties/launchSettings.json` (3541 or 5191 depending on the profile). Migrations can also be applied manually:

```powershell
dotnet tool restore
dotnet tool run dotnet-ef database update --project src/Abstrict.Api
```

### Key configuration

| Key (environment variables use `__`) | Meaning |
| --- | --- |
| `ConnectionStrings__DefaultConnection` | PostgreSQL connection string |
| `Database__ApplyMigrations` | Apply migrations on startup |
| `Cors__AllowedOrigins__0` | Allowed frontend origin (default `https://abstrict.vientruong.site`; add `http://localhost:5173` for local dev) |
| `Admin__SeedPhoneNumber` / `Admin__SeedPassword` | Creates the first admin (password ≥ 12 chars); dev values are in `appsettings.Development.json` |
| `Demo__SeedFreelancers` / `Demo__SeedPassword` | Enables demo data (8 freelancers + a test customer), password ≥ 12 chars |
| `Payments__Provider` | `Sandbox` or `Sepay` (with SePay keys) |
| `Otp__HmacKey`, `Kyc__CitizenIdHashKey` | Secret keys; must be set to your own values in production |

### Sample data (PowerShell, optional)

`scripts/seed-fake-freelancers.ps1`, `seed-booking-fixtures.ps1`, `seed-rating-fixtures.ps1`, `seed-freelancer-avatars.ps1`.

### Tests

```powershell
dotnet test
```

To run with the frontend (`http://localhost:5173`, which proxies `/api` to the local API), see the frontend repo README.

## API documentation

In `docs/`: `FEATURES.md`, `BOOKING_API.md`, `FREELANCER_KYC_API.md`, `FREELANCER_PROFILE_API.md`, `COMPANY_ONBOARDING_API.md`, `CUSTOMER_PROFILE_API.md`, `PROMOTION_PLANS_API.md`, `RATINGS_API.md`.

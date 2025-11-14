# 🏢 StaffValidator — Enterprise-ready Staff Validation System

[![.NET](https://img.shields.io/badge/.NET-8.0-blue.svg)](https://dotnet.microsoft.com/download)  [![License](https://img.shields.io/badge/license-MIT-green.svg)](LICENSE)

This repository contains StaffValidator — an ASP.NET Core 8.0 solution demonstrating an enterprise-focused staff management and validation system. It includes:

- A web application (`StaffValidator.WebApp`) with MVC UI and a REST API
- A core library (`StaffValidator.Core`) containing domain models, a repository layer, validation attributes, and a hybrid validation service (regex-first with deterministic automata fallback)
- A console checker (`StaffValidator.Checker`) to verify data and perform HTTP smoke checks against the web app
- Tests (`StaffValidator.Tests`) including unit tests and in-process integration tests using `WebApplicationFactory`

This README is a comprehensive guide you can use to prepare reports, demos, or CI automation.

## Table of contents

1. Overview & Goals
2. Project structure
3. How the Hybrid Validation works (design & safeguards)
4. Getting started (build & run)
5. Configuration
6. Checker app (data & interface verification)
7. API endpoints (summary + examples)
8. Authentication & security
9. Logging & observability
10. Testing & coverage
11. CI / Production recommendations
12. Troubleshooting & common issues
13. Contribution & license

---

## 1 — Overview & Goals

StaffValidator demonstrates a production-like approach to validating user-supplied staff records while protecting the system from regular expression denial-of-service (ReDoS). Key goals:

- Provide reliable validation for critical fields (Email, PhoneNumber) using a hybrid approach: fast regex checks with a bounded match-timeout and a deterministic automata fallback (NFA/DFA) when regex fails, is invalid, or times out.
- Secure the web application with JWT authentication, role-based authorization, and safe password hashing (BCrypt).
- Offer strong observability (Serilog structured logging) and test coverage (unit + integration).
- Provide a small console checker to verify both the data/validation layer and the interface layer (HTTP smoke tests), for use in CI or diagnostics.

Intended audience: developers, security reviewers, and ops engineers building robust validation services for web applications.

---

## 2 — Project structure

Top-level layout:

```
StaffValidator/
├── StaffValidator.Core/         # Core models, attributes, services (HybridValidatorService)
├── StaffValidator.WebApp/      # ASP.NET Core MVC + API project
├── StaffValidator.Checker/     # Console app: data-validator + HTTP smoke-checker
├── StaffValidator.Tests/       # Unit and integration tests (xUnit, WebApplicationFactory)
├── data/                       # sample JSON repository (staff_records.json)
├── README.md
└── StaffValidator.sln
```

Key files of interest
- `StaffValidator.Core/Services/HybridValidatorService.cs`: main hybrid validator, concurrency limiter, regex timeout handling, and automata fallback.
- `StaffValidator.Core/Attributes/*.cs`: Data annotation attributes (EmailCheckAttribute, PhoneCheckAttribute, RegexCheckAttribute).
- `StaffValidator.Core/AutomataFactory.cs`: pragmatic NFA/DFA builders for email/phone (used as fallback).
- `StaffValidator.WebApp/Controllers/StaffApiController.cs`: API controller for staff CRUD; wired to use `ValidatorService` (the hybrid implementation is registered in DI).
- `StaffValidator.WebApp/Controllers/Api/AuthApiController.cs`: programmatic login endpoint for JWT token issuance (useful for checker/tests).
- `StaffValidator.Checker/Program.cs`: console checker; supports `data` mode (validation of repository) and `--http-check` mode (HTTP smoke tests); supports `--username/--password` to obtain JWT and `--output` for JSON reports.

---

## 3 — Hybrid Validation (design & safeguards)

Goal: Validate fields like Email and Phone reliably while mitigating ReDoS.

Contract (what the HybridValidatorService does):

- Input: an object instance with properties decorated by validation attributes (e.g., `[EmailCheck(pattern)]`).
- Output: a tuple (bool ok, List<string> errors) indicating whether all checks passed and any error messages.

Algorithm (high-level):

1. For each property with a RegexCheckAttribute, build or reuse a compiled Regex using a match-timeout (configured via `HybridValidationOptions.RegexTimeoutMs`).
2. Before executing the regex, attempt to acquire a slot from a `SemaphoreSlim` configured by `MaxConcurrentRegexMatches`. If the semaphore is not available immediately, the service falls back to the automata to avoid queuing heavy regex work.
3. If the regex match completes successfully within the timeout and matches — accept. If the regex fails, throws `ArgumentException` (invalid pattern) or throws `RegexMatchTimeoutException`, attempt automata fallback.
4. For email and phone attributes, `AutomataFactory` provides deterministic NFA/DFA logic that `Simulate` can run quickly and deterministically.

Safeguards against ReDoS:

- Engine-level match timeout (use Regex constructor overload accepting `TimeSpan matchTimeout`) — avoids blocking threads for long periods.
- Concurrency limit using `SemaphoreSlim` with immediate `Wait(0)` acquisition attempt — prevents queuing many expensive regex tasks under heavy load.
- Deterministic fallback (automata) for known patterns (email, phone).

Configuration keys
- `HybridValidation:RegexTimeoutMs` (int, milliseconds, default 200)
- `HybridValidation:MaxConcurrentRegexMatches` (int, default 4)

Extending the fallback
- Add new attributes for patterns and implement NFA/DFA builders in `AutomataFactory` and `AutomataEngine`.

---

## 4 — Getting started (build & run)

Prerequisites
- .NET 8.0 SDK installed

Build the solution

```powershell
dotnet build StaffValidator.sln
```

Run the web application (development)

```powershell
cd StaffValidator.WebApp
dotnet run
# Web UI: http://localhost:5000
# Swagger: http://localhost:5000/api/docs
```

Run the checker (data validation against the JSON repository)

```powershell
dotnet run --project StaffValidator.Checker
```

HTTP smoke-check the running web app (interface verification)

```powershell
# Basic smoke check (no auth)
dotnet run --project StaffValidator.Checker -- --http-check http://localhost:5000

# With credentials (will POST to /api/auth/login and attach bearer token)
dotnet run --project StaffValidator.Checker -- --http-check http://localhost:5000 --username admin --password admin123

# Write a JSON report to file
dotnet run --project StaffValidator.Checker -- --http-check http://localhost:5000 --username admin --password admin123 --output checker-report.json
```

UI layer verification (MVC Views, Forms, HTML rendering)

```powershell
# Verify MVC interface layer
dotnet run --project StaffValidator.Checker -- --ui-check http://localhost:5000 --username admin --password admin123

# With JSON report
dotnet run --project StaffValidator.Checker -- --ui-check http://localhost:5000 --username admin --password admin123 --output ui-report.json
```

Exit codes (useful for CI):
- `0` — success (no mismatches / no HTTP failures)
- `2` — data mismatches found
- `3` — HTTP smoke-check failures or authentication required but not obtained
- `4` — performance test errors detected
- `5` — UI layer verification failures

---

## 5 — Configuration (detailed)

Primary configuration is in `appsettings.json` / `appsettings.Development.json` and environment variables.

Important sections:

- `JwtSettings` — `SecretKey`, `Issuer`, `Audience`, `ExpiryInMinutes` (token lifetime).
- `StaffRepository` — `FilePath` (defaults to `data/staff_records.json`), `BackupEnabled`.
- `Serilog` — logging options (MinimumLevel, WriteTo etc.).
- `HybridValidation` — see previous section for `RegexTimeoutMs` and `MaxConcurrentRegexMatches`.

Environment variable mapping example (Windows / PowerShell):

```powershell
$env:HybridValidation__RegexTimeoutMs = "250"
$env:HybridValidation__MaxConcurrentRegexMatches = "6"
$env:JWT_SECRET = "replace-with-32-plus-char-secret"
```

Security note: ensure `JwtSettings:SecretKey` is stored in a secure vault for production (Key Vault, environment variable, or secrets manager). Do not check secrets into source control.

---

## 6 — Checker app (details)

Purpose: provide a small console verification tool to validate both the data/validation layer and quickly exercise the HTTP interface.

Modes:

- **Data-check (default)**: Reads `data/staff_records.json`, validates each record using `HybridValidatorService`, compares with automata, prints mismatches, writes report (if `--output`), and exits with code 0 (no mismatches) or 2 (mismatches present).
- **HTTP smoke-check**: `--http-check <baseUrl>` runs a set of HTTP requests to verify the web app is up and returns expected results. Supports authentication via `--username` and `--password` (POST `/api/auth/login`) and writing a JSON report with `--output <file>`.
- **UI layer verification**: `--ui-check <baseUrl>` verifies MVC Views, form rendering, HTML content, and CSRF tokens. Tests the presentation/interface layer by requesting pages like `/`, `/Staff`, `/Staff/Create`, and validating HTML structure and expected content. Supports form-based authentication and JSON report output.
- **Performance test (safe stress)**: `--perf <baseUrl> [--endpoint /api/staff] [--concurrency 10] [--duration 30] [--username ... --password ...] [--output report.json] [--confirm-perf]`
  - Sends concurrent GET requests to the specified endpoint for the given duration.
  - Reports totals, RPS, and latency percentiles (avg, p50, p95, p99) and status code counts.
  - Safety: by default caps `--concurrency` at 50 and `--duration` at 60s unless `--confirm-perf` is provided.

Examples:

```powershell
# 30s perf run with concurrency 10 (default), endpoint /api/staff
dotnet run --project StaffValidator.Checker -- --perf http://localhost:5000

# 45s perf run with concurrency 25 on /api/staff (requires --confirm-perf to exceed default caps)
dotnet run --project StaffValidator.Checker -- --perf http://localhost:5000 --endpoint /api/staff --concurrency 25 --duration 45 --confirm-perf

# Authenticated perf run and write JSON report
dotnet run --project StaffValidator.Checker -- --perf http://localhost:5000 --username admin --password admin123 --output perf-report.json --confirm-perf
```

Notes:
- Use only against environments you own and control. The perf mode is intended for safe stress within development/staging and includes conservative guardrails.
- Consider enabling rate limiting at the app or reverse-proxy layer; observe HTTP 429/5xx under bursts.

How the checker authenticates

- The checker POSTs JSON `{ "Username": "...", "Password": "..." }` to `/api/auth/login` and expects a JSON payload containing `token` (or `access_token`, or nested `data.token`). The token is attached as `Authorization: Bearer <token>` for subsequent requests.

Report format (example):

```json
{
  "mode": "http-check",
  "baseUrl": "http://localhost:5000",
  "authUsed": true,
  "failures": [ /* ... */ ]
}
```

Performance report example (`--output perf-report.json`):

```json
{
  "mode": "perf",
  "baseUrl": "http://localhost:5000",
  "endpoint": "/api/staff",
  "concurrency": 10,
  "durationSec": 30,
  "totals": { "total": 1234, "success": 1230, "errors": 4, "rps": 41.1 },
  "latency": { "avgMs": 20.4, "p50Ms": 18, "p95Ms": 40, "p99Ms": 60 },
  "status": { "200": 1230, "500": 4 }
}
```

---

## 7 — API endpoints (summary & examples)

Authentication endpoints (web UI routes exist as MVC views; API routes exist under `/api/auth`):

- POST `/api/auth/login` — body: `{ "Username": "admin", "Password": "admin123" }`, returns `{ "success": true, "token": "<jwt>" }`.

Staff API (examples)

- GET `/api/staff` — returns JSON array of staff records.
- POST `/api/staff` — create staff (requires auth & role).

Example: curl create (with token)

```bash
curl -X POST http://localhost:5000/api/staff \
  -H "Authorization: Bearer <token>" \
  -H "Content-Type: application/json" \
  -d '{ "StaffID":"s-100","StaffName":"New Person","Email":"new@example.com","PhoneNumber":"+100000000" }'
```

Note: The web UI also performs client-side checks and presents a polished UX for manual verification.

---

## 8 — Authentication & security details

- Authentication service is in `StaffValidator.Core.Services.AuthenticationService` and uses an in-memory user store for demo. Produces JWTs with role claims.
- Passwords in demo users are hashed using BCrypt.
- JWT bearer tokens are validated by the web app middleware with `TokenValidationParameters` configured in `Program.cs`.
- For production, replace in-memory users with a secure identity store (IdentityServer, ASP.NET Identity, or an external provider).

Security recommendations

- Keep `JwtSettings:SecretKey` outside source control (KeyVault / environment variable).
- Use HTTPS in production and set secure cookie options.
- Rate-limit endpoints exposed to anonymous users to reduce abuse.

---

## 9 — Logging & observability

- Serilog configured in `StaffValidator.WebApp/Program.cs` with console and file sinks.
- Logs are structured and include environment/machine enrichers.
- Key events are logged:
  - Authentication attempts and results
  - Validation fallbacks and timeouts (HybridValidatorService logs warnings when fallback occurs)
  - Uploads and bulk operations

Monitoring

- Track occurrence of fallback events (timeout, invalid regex, concurrency fallback) — these indicate either malicious inputs (possible ReDoS attempt) or misconfigured validation patterns.
- Consider piping Serilog output to a centralized log sink (ELK, Seq, Application Insights) for alerts and dashboards.

---

## 10 — Testing & coverage

Unit + Integration

- Unit tests live under `StaffValidator.Tests` and use xUnit.
- Integration tests use `Microsoft.AspNetCore.Mvc.Testing`'s `WebApplicationFactory<Program>` to exercise controllers and authentication in-process.

Run tests and collect coverage

```powershell
dotnet test StaffValidator.sln --collect:"XPlat Code Coverage"
```

Coverage reports

- Test runner produces coverage artifacts under `*/TestResults/*/coverage.cobertura.xml`.
- To generate an HTML coverage report using ReportGenerator:

```powershell
dotnet tool install --global dotnet-reportgenerator-globaltool
reportgenerator -reports:"**/TestResults/*/coverage.cobertura.xml" -targetdir:coverage-report -reporttypes:Html
# open coverage-report/index.htm
```

Quick notes:

- There is a concurrency-focused unit test (HybridValidatorConcurrencyTests) designed to exercise the semaphore fallback path with pathological inputs.
- Integration tests validate that login yields a token and that protected endpoints accept tokens.

---

## 11 — CI / Production recommendations

- Build and test pipeline (example stages):
  1. Checkout
  2. dotnet restore / dotnet build
  3. dotnet test --collect:"XPlat Code Coverage"
  4. Generate coverage HTML (ReportGenerator) and publish artifacts
  5. Run `dotnet publish -c Release` and create container image
  6. Deploy to staging and run `StaffValidator.Checker --http-check` to smoke-test endpoints

- Add runtime alerts for:
  - Frequent fallback events (regex timeouts / concurrency fallbacks)
  - Authentication failures spikes
  - High error-rate responses

---

## 12 — Troubleshooting & common issues

- ReDoS-like behavior: If you see many regex timeouts or fallback events, tighten regex patterns, lower `MaxConcurrentRegexMatches`, and consider replacing fragile patterns with automata or simpler checks.
- `POST /api/staff` returns 401 in checker: provide valid credentials with `--username`/`--password` or use `--allow-unauth` (not recommended for CI).
- Token parsing: checker attempts to parse `token` or `access_token` fields; if your auth endpoint uses a different shape, update `StaffValidator.Checker/Program.cs` accordingly.

---

## 13 — Contribution & license

Please follow the repository contribution flow: branch per feature, add tests, update docs, and open a PR.

This project is MIT licensed — see the `LICENSE` file.


## Selenium test
dotnet run --project "StaffValidator.Checker" -- --selenium-ui-check http://localhost:5000 --browser chrome --no-headless --timeout 20 --username admin --password admin123 --delay-ms 500 --output .\selenium-report.json
---



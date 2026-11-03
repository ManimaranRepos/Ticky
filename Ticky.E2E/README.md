# Ticky.E2E — Playwright end-to-end tests

Browser tests for Ticky written with **Playwright for .NET** and **NUnit** (the same test framework as `Ticky.Units`).

## What is covered

| Area | Tests |
| --- | --- |
| Authentication | redirect with `returnUrl`, wrong password, required fields, default credentials disabled after first setup, login + logout |
| Projects & boards | create project and board, upper-case board code validation, required project name, "Recent board" link |
| Cards | create columns and cards, counters, persistence after reload, column card limit, card details modal |
| Drag & drop | move a card between columns (persisted), refuse a drop into a full column |
| Real-time sync | two independent browsers on one board: new card and moved card appear without reload (SignalR `UpdateHub`) |

Every test also fails if Blazor's "An unhandled error has occurred" bar is shown.

## How it works

- **Global setup** (`GlobalSetup.cs`) starts Ticky if nothing answers at `TICKY_BASE_URL`, walks the seeded `admin@ticky.com` through Ticky's forced credential change, and saves the logged-in browser state. Every test then starts already authenticated in its own isolated browser context.
- **Page objects** (`Pages/`) keep selectors in one place: `LoginPage`, `CredentialsPage`, `HomePage`, `BoardPage`.
- **Blazor helpers** (`Infrastructure/BlazorExtensions.cs`):
  - `ClickUntilVisibleAsync` — Ticky prerenders, so clicks before the SignalR circuit connects are ignored. Retry the click until the expected element appears, instead of sleeping.
  - `FillAndCommitAsync` — Blazor `InputText` binds on `change`, not `input`.
  - `SortableDragAsync` — Ticky's Sortable.js runs with `forceFallback: true` (pointer events), so the mouse is moved in steps.
- **Why Release on :8080?** In Debug builds Ticky hard-codes its board hub to `https://localhost:7225`; Release uses `http://localhost:8080`. The suite runs Release with `ASPNETCORE_ENVIRONMENT=Development` so real-time updates work and static assets are served by `dotnet run`.
- Each test creates its own uniquely named project and board, so tests run in parallel safely.
- On failure a **trace** and **screenshot** are saved to `bin/Debug/net10.0/playwright-artifacts/`. Open a trace with `pwsh bin/Debug/net10.0/playwright.ps1 show-trace <file>.zip`.

## Run it

Prerequisites: .NET 10 SDK, PowerShell (`pwsh`), and MySQL 8 (Docker is easiest).

```bash
# 1. empty MySQL database
docker compose -f docker-compose.e2e.yml up -d --wait

# 2. build and install the browser (once)
dotnet build Ticky.E2E
pwsh Ticky.E2E/bin/Debug/net10.0/playwright.ps1 install chromium

# 3. run (the suite starts Ticky for you)
dotnet test Ticky.E2E

# watch it in a real browser
dotnet test Ticky.E2E -- Playwright.LaunchOptions.Headless=false Playwright.LaunchOptions.SlowMo=300 NUnit.NumberOfTestWorkers=1

# only one area
dotnet test Ticky.E2E --filter "Category=DragAndDrop"
```

In VS Code: **Terminal → Run Task…** has ready-made tasks for each step, and the Testing panel (C# Dev Kit) lists every test.

### Settings (environment variables)

| Variable | Default |
| --- | --- |
| `TICKY_BASE_URL` | `http://localhost:8080` (use `http://localhost:4088` for the official Docker image) |
| `TICKY_START_APP` | `true` — set `false` to test an app you started yourself |
| `TICKY_DB_HOST` / `TICKY_DB_NAME` / `TICKY_DB_USER` / `TICKY_DB_PASSWORD` | `localhost` / `ticky_e2e` / `ticky` / `ticky_e2e_pw` |
| `TICKY_ADMIN_EMAIL` / `TICKY_ADMIN_PASSWORD` | `e2e.admin@ticky.test` / `E2e-Passw0rd!` |

The suite expects an **empty database** on its first run (it needs the default admin). Re-runs against the same database work too.

CI: `.github/workflows/e2e.yml` runs everything on GitHub Actions with a MySQL service container and uploads traces when a test fails.

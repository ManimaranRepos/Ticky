<#
.SYNOPSIS
  One-shot setup for Ticky + the Playwright E2E suite on Windows.

  1. Installs what is missing (.NET 10 SDK, Docker Desktop, VS Code) with winget
  2. Starts an empty MySQL 8 in Docker
  3. Builds the solution and installs the Playwright Chromium browser
  4. Runs the existing unit tests and the E2E suite (the suite starts Ticky itself)
  5. Opens the folder in VS Code

.EXAMPLE
  powershell -ExecutionPolicy Bypass -File .\run-e2e.ps1
  powershell -ExecutionPolicy Bypass -File .\run-e2e.ps1 -Headed      # watch the browser
  powershell -ExecutionPolicy Bypass -File .\run-e2e.ps1 -RunApp      # leave Ticky running at http://localhost:8080 afterwards
#>
param(
    [switch]$Headed,
    [switch]$RunApp,
    [switch]$SkipInstall
)

$ErrorActionPreference = 'Stop'
Set-Location -Path $PSScriptRoot

function Step($msg) { Write-Host "`n==> $msg" -ForegroundColor Cyan }
function Have($cmd) { [bool](Get-Command $cmd -ErrorAction SilentlyContinue) }
function Refresh-Path {
    $env:Path = [Environment]::GetEnvironmentVariable('Path', 'Machine') + ';' + [Environment]::GetEnvironmentVariable('Path', 'User')
}
function Winget-Install($id) {
    if (-not (Have winget)) { throw "winget is not available. Install '$id' manually and re-run." }
    winget install --id $id --exact --silent --accept-package-agreements --accept-source-agreements
    Refresh-Path
}

# ---------------------------------------------------------------- prerequisites
if (-not $SkipInstall) {
    Step 'Checking prerequisites'

    $hasNet10 = (Have dotnet) -and ((dotnet --list-sdks) -match '^10\.')
    if (-not $hasNet10) { Write-Host '.NET 10 SDK missing - installing'; Winget-Install 'Microsoft.DotNet.SDK.10' }
    else { Write-Host '.NET 10 SDK: OK' }

    if (-not (Have docker)) {
        Write-Host 'Docker missing - installing Docker Desktop (a reboot/login may be required)'
        Winget-Install 'Docker.DockerDesktop'
    } else { Write-Host 'Docker: OK' }

    if (-not (Have code)) { Write-Host 'VS Code missing - installing'; Winget-Install 'Microsoft.VisualStudioCode' }
    else { Write-Host 'VS Code: OK' }
}

# ---------------------------------------------------------------- database
Step 'Starting MySQL 8 (Docker, throwaway data)'
docker info *> $null
if ($LASTEXITCODE -ne 0) {
    $dd = Join-Path $env:ProgramFiles 'Docker\Docker\Docker Desktop.exe'
    if (Test-Path $dd) { Start-Process $dd; Write-Host 'Waiting for Docker Desktop to start...' }
    for ($i = 0; $i -lt 60; $i++) { Start-Sleep 5; docker info *> $null; if ($LASTEXITCODE -eq 0) { break } }
    if ($LASTEXITCODE -ne 0) { throw 'Docker is not running. Start Docker Desktop and re-run this script.' }
}
docker compose -f docker-compose.e2e.yml down *> $null
docker compose -f docker-compose.e2e.yml up -d --wait
if ($LASTEXITCODE -ne 0) { throw 'MySQL container did not become healthy.' }

# ---------------------------------------------------------------- build
Step 'Building the solution'
dotnet build Ticky.sln
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
dotnet build Ticky.Web/Ticky.Web.csproj -c Release
if ($LASTEXITCODE -ne 0) { throw 'Release build of Ticky.Web failed.' }

Step 'Installing Playwright Chromium'
& powershell -ExecutionPolicy Bypass -File "Ticky.E2E/bin/Debug/net10.0/playwright.ps1" install chromium
if ($LASTEXITCODE -ne 0) { throw 'Playwright browser install failed.' }

# ---------------------------------------------------------------- tests
Step 'Running existing unit tests (Ticky.Units)'
dotnet test Ticky.Units/Ticky.Units.csproj --no-build

Step 'Running Playwright E2E tests (Ticky.E2E)'
$testArgs = @('test', 'Ticky.E2E/Ticky.E2E.csproj', '--no-build', '--logger', 'console;verbosity=normal', '--logger', 'trx;LogFileName=e2e.trx')
if ($Headed) { $testArgs += @('--', 'Playwright.LaunchOptions.Headless=false', 'Playwright.LaunchOptions.SlowMo=300', 'NUnit.NumberOfTestWorkers=1') }
dotnet @testArgs
$e2eExit = $LASTEXITCODE

if ($e2eExit -eq 0) { Write-Host "`nAll E2E tests passed." -ForegroundColor Green }
else {
    Write-Host "`nSome E2E tests failed. Traces and screenshots: Ticky.E2E\bin\Debug\net10.0\playwright-artifacts\" -ForegroundColor Yellow
    Write-Host 'Open a trace with: powershell -File Ticky.E2E\bin\Debug\net10.0\playwright.ps1 show-trace <file>.zip'
}

# ---------------------------------------------------------------- app + editor
if (Have code) { Step 'Opening VS Code'; code . }

if ($RunApp) {
    Step 'Starting Ticky at http://localhost:8080  (Ctrl+C to stop)'
    Write-Host 'Log in as e2e.admin@ticky.test / E2e-Passw0rd!  (set by the test run)'
    $env:ASPNETCORE_ENVIRONMENT = 'Development'
    $env:DB_HOST = 'localhost'; $env:DB_NAME = 'ticky_e2e'; $env:DB_USERNAME = 'ticky'; $env:DB_PASSWORD = 'ticky_e2e_pw'
    $env:SMTP_ENABLED = 'false'; $env:FULLY_OFFLINE = 'true'; $env:BASE_URL = 'http://localhost:8080'
    Start-Process 'http://localhost:8080'
    dotnet run -c Release --no-build --project Ticky.Web/Ticky.Web.csproj --no-launch-profile
}

exit $e2eExit

# Starts the whole app for local development: PostgreSQL (Docker), the API and the Blazor web app.
# Each app opens in its own window; close a window (or press Ctrl+C in it) to stop that app.
# The database keeps running in Docker; stop it with `docker compose stop`.
$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot

if (-not (Get-Command docker -ErrorAction SilentlyContinue)) {
    Write-Host "Docker was not found. Install Docker Desktop and start it, then run this again." -ForegroundColor Red
    exit 1
}

Write-Host "Starting database..." -ForegroundColor Cyan
docker compose up -d --wait
if ($LASTEXITCODE -ne 0) {
    Write-Host "Could not start the database. Is Docker Desktop running?" -ForegroundColor Red
    exit 1
}

Write-Host "Starting API and web app..." -ForegroundColor Cyan
Start-Process powershell -ArgumentList '-NoExit', '-Command', "`$Host.UI.RawUI.WindowTitle = 'ZaneTask API'; dotnet run --project src/ZaneTask.Api --launch-profile http"
Start-Process powershell -ArgumentList '-NoExit', '-Command', "`$Host.UI.RawUI.WindowTitle = 'ZaneTask Web'; dotnet run --project src/ZaneTask.Web --launch-profile http"

Write-Host ""
Write-Host "ZaneTask is starting. The browser opens http://localhost:5046 when the web app is ready." -ForegroundColor Green
Write-Host "API: http://localhost:5299"

#requires -Version 5.1
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $MyInvocation.MyCommand.Path

function Write-Step {
    param([string]$Message)
    Write-Host ""
    Write-Host "==> $Message" -ForegroundColor Cyan
}

function Stop-WithError {
    param([string]$Message)
    Write-Host ""
    Write-Host "ERROR: $Message" -ForegroundColor Red
    exit 1
}

function Require-Command {
    param([string]$Name, [string]$Hint)
    if (-not (Get-Command $Name -ErrorAction SilentlyContinue)) {
        Stop-WithError "$Name was not found. $Hint"
    }
}

function Start-AppWindow {
    param([string]$Title, [string]$WorkingDirectory, [string]$Command)

    $safeTitle = $Title.Replace("'", "''")
    $safeDirectory = $WorkingDirectory.Replace("'", "''")
    $script = @'
$Host.UI.RawUI.WindowTitle = '__TITLE__'
Set-Location -LiteralPath '__DIRECTORY__'
__COMMAND__
'@
    $script = $script.Replace('__TITLE__', $safeTitle).Replace('__DIRECTORY__', $safeDirectory).Replace('__COMMAND__', $Command)
    $encoded = [Convert]::ToBase64String([Text.Encoding]::Unicode.GetBytes($script))
    Start-Process -FilePath 'powershell.exe' -ArgumentList @(
        '-NoExit',
        '-NoProfile',
        '-ExecutionPolicy', 'Bypass',
        '-EncodedCommand', $encoded
    ) | Out-Null
}

Write-Host "WaterWatch launcher" -ForegroundColor Green
Write-Host "Repository: $repoRoot"

Require-Command 'dotnet' 'Install the .NET 8 SDK or newer and reopen this window.'

$envFile = Join-Path $repoRoot '.env'
if (Test-Path $envFile) {
    Write-Step 'Loading local environment from .env'
    Get-Content $envFile | ForEach-Object {
        if ($_ -match '^\s*([^#][^=]*)=(.*)$') {
            Set-Item -Path "Env:$($matches[1].Trim())" -Value $matches[2].Trim()
        }
    }
}
if (-not $env:ASPNETCORE_ENVIRONMENT) {
    $env:ASPNETCORE_ENVIRONMENT = 'Development'
}

Write-Host ""
Write-Host "WaterWatch is ASP.NET Core MVC with bundled Leaflet assets; backend and frontend run in one process." -ForegroundColor Yellow
Write-Host "PostgreSQL with PostGIS must be running and the configured waterwatch database must be available."
Write-Host "The application applies migrations and seeds Port Harcourt boundaries, OSM layers and optional demo data."

Write-Step 'Starting WaterWatch on http://localhost:5099'
Start-AppWindow -Title 'WaterWatch (http://localhost:5099)' -WorkingDirectory $repoRoot -Command @'
dotnet run --project 'WatchWaterConsumption\WatchWaterConsumption.csproj' --no-launch-profile --urls 'http://localhost:5099'
'@

Write-Host ""
Write-Host "Application: http://localhost:5099" -ForegroundColor Yellow
Write-Host "Swagger:     http://localhost:5099/swagger"
Write-Host "The first map load can take a few seconds while GeoJSON data is imported."

Start-Sleep -Seconds 8
Start-Process 'http://localhost:5099'

#Requires -Version 5.1
<#
.SYNOPSIS
	Tears down the DtoOrm sample stack started by start-all.ps1.

.DESCRIPTION
	Stops the Portal and API (by the PIDs saved at start time, and by whatever
	is listening on their ports as a fallback) and then stops the MariaDB
	container via Docker Compose.

.PARAMETER KeepDatabase
	Leave the MariaDB container running; only stop the API and Portal.

.PARAMETER RemoveData
	Also delete the MariaDB data volume ('docker compose down -v') so the next
	start re-seeds the database from scratch.

.EXAMPLE
	.\stop-all.ps1

.EXAMPLE
	.\stop-all.ps1 -RemoveData
#>
[CmdletBinding()]
param(
	[switch] $KeepDatabase,
	[switch] $RemoveData
)

$ErrorActionPreference = 'SilentlyContinue'

$RepoRoot      = $PSScriptRoot
$ContainerName = 'dtoorm-mariadb'
$ComposeFile   = Join-Path $RepoRoot 'samples\docker-compose.yml'
$StateDir      = Join-Path $env:TEMP 'dtoorm-run'
$StateFile     = Join-Path $StateDir 'state.json'

function Write-Banner($text) { Write-Host ''; Write-Host "==> $text" -ForegroundColor Cyan }
function Write-Info($text)   { Write-Host "    $text" -ForegroundColor Gray }
function Write-Ok($text)     { Write-Host "    OK  $text" -ForegroundColor Green }

function Stop-Tree($processId) {
	if (-not $processId) { return }
	try {
		# Stop child processes first (e.g., the 'dotnet' under the host window), then the host.
		Get-CimInstance Win32_Process -Filter "ParentProcessId=$processId" -ErrorAction SilentlyContinue |
			ForEach-Object { Stop-Tree $_.ProcessId }
		Stop-Process -Id $processId -Force -ErrorAction SilentlyContinue
	} catch { }
}

function Stop-ByPort($port) {
	try {
		Get-NetTCPConnection -LocalPort $port -State Listen -ErrorAction SilentlyContinue |
			ForEach-Object { Stop-Tree $_.OwningProcess }
	} catch { }
}

Write-Host ''
Write-Host '  Stopping the DtoOrm sample stack' -ForegroundColor Yellow

# Read saved state (if start-all.ps1 wrote it).
$state = $null
if (Test-Path $StateFile) {
	try { $state = Get-Content $StateFile -Raw | ConvertFrom-Json } catch { $state = $null }
}

# Portal + API
Write-Banner 'Portal and API'

$apiUrl    = 'http://localhost:5080'
$portalUrl = 'http://localhost:5090'
if ($state) {
	if ($state.apiUrl)    { $apiUrl = $state.apiUrl }
	if ($state.portalUrl) { $portalUrl = $state.portalUrl }
	Stop-Tree $state.portalPid
	Stop-Tree $state.apiPid
}

# Fallback: stop whatever is listening on the known ports (covers manual launches).
Stop-ByPort ([Uri]$portalUrl).Port
Stop-ByPort ([Uri]$apiUrl).Port
Write-Ok "Stopped processes on ports $(([Uri]$portalUrl).Port) and $(([Uri]$apiUrl).Port)."

# Database
if ($KeepDatabase) {
	Write-Banner 'Database (kept running)'
} else {
	Write-Banner 'Database - MariaDB container'
	if (Get-Command docker -ErrorAction SilentlyContinue) {
		$downArgs = @('compose', '--project-directory', $RepoRoot, '-f', $ComposeFile, 'down')
		if ($RemoveData) { $downArgs += '-v' }
		& docker @downArgs
		if ($LASTEXITCODE -ne 0) {
			# Fall back to stopping the named container directly (e.g., Compose v1 only).
			& docker stop $ContainerName *> $null
		}
		if ($RemoveData) { Write-Ok 'Container and data volume removed (next start re-seeds).' }
		else             { Write-Ok 'Container stopped.' }
	} else {
		Write-Info 'Docker not found; skipping database teardown.'
	}
}

# Clear saved state.
if (Test-Path $StateFile) { Remove-Item $StateFile -Force -ErrorAction SilentlyContinue }

Write-Host ''
Write-Host '  Done.' -ForegroundColor Green
Write-Host ''

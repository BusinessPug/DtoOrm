#Requires -Version 5.1
<#
.SYNOPSIS
	One-command launcher for the DtoOrm sample stack on Windows:
	MariaDB (Docker) -> DtoOrm.Api -> DtoOrm.Portal -> browser.

.DESCRIPTION
	Starts every tier in the right order and waits for each one to be ready
	before starting the next:

	  1. Brings up MariaDB via samples/docker-compose.yml and waits for the
		 container's health check to report "healthy".
	  2. Builds the API and Portal up front so compile errors surface here
		 instead of in a child window.
	  3. Launches the API in its own window and waits until it answers.
	  4. Launches the Portal in its own window and waits until it answers.
	  5. Opens the Portal in your default browser.

	The API and Portal each run in their own PowerShell window so you can read
	their logs and Ctrl+C them individually. Run .\stop-all.ps1 to tear the
	whole stack down again (apps + database container).

.PARAMETER SkipDatabase
	Don't touch Docker; assume MariaDB is already listening on port 3306.

.PARAMETER SkipBuild
	Skip the upfront 'dotnet build' (each app still builds on 'dotnet run').

.PARAMETER NoBrowser
	Don't open the Portal in a browser at the end.

.EXAMPLE
	.\start-all.ps1

.EXAMPLE
	.\start-all.ps1 -SkipDatabase -NoBrowser
#>
[CmdletBinding()]
param(
	[switch] $SkipDatabase,
	[switch] $SkipBuild,
	[switch] $NoBrowser,
	[string] $ApiUrl = 'http://localhost:5080',
	[string] $PortalUrl = 'http://localhost:5090',
	[int]    $DatabaseTimeoutSeconds = 150,
	[int]    $ServiceTimeoutSeconds = 150
)

$ErrorActionPreference = 'Stop'

$RepoRoot      = $PSScriptRoot
$ContainerName = 'dtoorm-mariadb'
$ComposeFile   = Join-Path $RepoRoot 'samples\docker-compose.yml'
$ApiProject    = 'samples\DtoOrm.Api\DtoOrm.Api.csproj'
$PortalProject = 'samples\DtoOrm.Portal\DtoOrm.Portal.csproj'
$StateDir      = Join-Path $env:TEMP 'dtoorm-run'
$StateFile     = Join-Path $StateDir 'state.json'

function Write-Banner($text) { Write-Host ''; Write-Host "==> $text" -ForegroundColor Cyan }
function Write-Info($text)   { Write-Host "    $text" -ForegroundColor Gray }
function Write-Ok($text)     { Write-Host "    OK  $text" -ForegroundColor Green }

function Resolve-PwshHost {
	if (Get-Command pwsh -ErrorAction SilentlyContinue) { return 'pwsh' }
	return 'powershell'
}

function Get-ComposeCommand {
	# Prefer Docker Compose v2 ('docker compose'); fall back to v1 ('docker-compose').
	try {
		& docker compose version *> $null
		if ($LASTEXITCODE -eq 0) { return @{ Exe = 'docker'; Prefix = @('compose') } }
	} catch { }
	if (Get-Command docker-compose -ErrorAction SilentlyContinue) {
		return @{ Exe = 'docker-compose'; Prefix = @() }
	}
	throw "Docker Compose was not found. Install Docker Desktop (it includes 'docker compose')."
}

function Wait-ContainerHealthy($name, $timeoutSeconds) {
	$deadline = (Get-Date).AddSeconds($timeoutSeconds)
	while ((Get-Date) -lt $deadline) {
		$status = ''
		try { $status = (& docker inspect --format '{{.State.Health.Status}}' $name 2>$null) } catch { $status = '' }
		if ($status) { $status = "$status".Trim() }
		if ($status -eq 'healthy')   { return }
		if ($status -eq 'unhealthy') { throw "Container '$name' reported 'unhealthy'. Inspect it with 'docker logs $name'." }
		Write-Host '.' -NoNewline -ForegroundColor DarkGray
		Start-Sleep -Seconds 2
	}
	Write-Host ''
	throw "Timed out after $timeoutSeconds s waiting for '$name' to become healthy."
}

function Wait-HttpReady($url, $timeoutSeconds) {
	$deadline = (Get-Date).AddSeconds($timeoutSeconds)
	while ((Get-Date) -lt $deadline) {
		try {
			Invoke-WebRequest -Uri $url -UseBasicParsing -TimeoutSec 4 | Out-Null
			return $true
		} catch {
			$resp = $_.Exception.Response
			if ($resp -and $resp.StatusCode) { return $true }  # server answered (even 4xx) => it's up
		}
		Write-Host '.' -NoNewline -ForegroundColor DarkGray
		Start-Sleep -Seconds 2
	}
	Write-Host ''
	return $false
}

function Start-AppWindow($title, $projectRelPath, $launchUrl) {
	$psHost = Resolve-PwshHost
	$childCmd = "`$Host.UI.RawUI.WindowTitle = '$title'; " +
				"Set-Location '$RepoRoot'; " +
				"Write-Host '$title' -ForegroundColor Green; " +
				"Write-Host 'URL: $launchUrl' -ForegroundColor Gray; " +
				"dotnet run --project '$projectRelPath' -c Debug"
	$argLine = "-NoExit -Command `"$childCmd`""
	return Start-Process -FilePath $psHost -ArgumentList $argLine -PassThru
}

# ---- main -------------------------------------------------------------------

Write-Host ''
Write-Host '  DtoOrm sample stack launcher' -ForegroundColor Green
Write-Host '  database  ->  api  ->  portal' -ForegroundColor DarkGray

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
	throw "The .NET SDK ('dotnet') was not found on PATH. Install the .NET 10 SDK."
}

# 1. Database -----------------------------------------------------------------
if ($SkipDatabase) {
	Write-Banner 'Database (skipped)'
	Write-Info 'Assuming MariaDB is already listening on port 3306.'
} else {
	Write-Banner 'Database - MariaDB via Docker Compose'
	if (-not (Get-Command docker -ErrorAction SilentlyContinue)) {
		throw "Docker was not found. Install Docker Desktop, or rerun with -SkipDatabase if MariaDB runs elsewhere."
	}
	try { & docker info *> $null } catch { }
	if ($LASTEXITCODE -ne 0) {
		throw "The Docker engine doesn't appear to be running. Start Docker Desktop and try again."
	}
	if (-not (Test-Path $ComposeFile)) { throw "Compose file not found: $ComposeFile" }

	$compose = Get-ComposeCommand
	$composeExe    = $compose.Exe
	$composePrefix = $compose.Prefix
	Write-Info "Bringing up the 'mariadb' service (port 3306)..."
	& $composeExe @composePrefix --project-directory $RepoRoot -f $ComposeFile up -d
	if ($LASTEXITCODE -ne 0) { throw "docker compose up failed (exit code $LASTEXITCODE)." }

	Write-Info 'Waiting for MariaDB to pass its health check'
	Wait-ContainerHealthy -name $ContainerName -timeoutSeconds $DatabaseTimeoutSeconds
	Write-Host ''
	Write-Ok "MariaDB is healthy on localhost:3306 (database 'dtoorm_demo')."
}

# 2. Build --------------------------------------------------------------------
if ($SkipBuild) {
	Write-Banner 'Build (skipped)'
} else {
	Write-Banner 'Build - API and Portal'
	foreach ($proj in @($ApiProject, $PortalProject)) {
		Write-Info "dotnet build $proj"
		& dotnet build (Join-Path $RepoRoot $proj) -c Debug --nologo -v minimal
		if ($LASTEXITCODE -ne 0) { throw "Build failed for $proj. Fix the errors above and rerun." }
	}
	Write-Ok 'Build succeeded.'
}

# 3. API ----------------------------------------------------------------------
Write-Banner 'API - DtoOrm.Api'
$apiProc = Start-AppWindow -title 'DtoOrm API (port 5080)' -projectRelPath $ApiProject -launchUrl "$ApiUrl/swagger"
Write-Info "Launched in a new window (PID $($apiProc.Id)). Waiting for it to answer"
if (-not (Wait-HttpReady -url "$ApiUrl/swagger/v1/swagger.json" -timeoutSeconds $ServiceTimeoutSeconds)) {
	throw "The API didn't become ready within $ServiceTimeoutSeconds s. Check the 'DtoOrm API' window for errors."
}
Write-Host ''
Write-Ok "API is ready at $ApiUrl (Swagger UI at $ApiUrl/swagger)."

# 4. Portal -------------------------------------------------------------------
Write-Banner 'Portal - DtoOrm.Portal'
$portalProc = Start-AppWindow -title 'DtoOrm Portal (port 5090)' -projectRelPath $PortalProject -launchUrl $PortalUrl
Write-Info "Launched in a new window (PID $($portalProc.Id)). Waiting for it to answer"
if (-not (Wait-HttpReady -url $PortalUrl -timeoutSeconds $ServiceTimeoutSeconds)) {
	throw "The Portal didn't become ready within $ServiceTimeoutSeconds s. Check the 'DtoOrm Portal' window for errors."
}
Write-Host ''
Write-Ok "Portal is ready at $PortalUrl."

# Save state so stop-all.ps1 can find the processes again ----------------------
$containerForState = ''
if (-not $SkipDatabase) { $containerForState = $ContainerName }
New-Item -ItemType Directory -Force -Path $StateDir | Out-Null
@{
	apiPid    = $apiProc.Id
	portalPid = $portalProc.Id
	apiUrl    = $ApiUrl
	portalUrl = $PortalUrl
	container = $containerForState
	startedAt = (Get-Date).ToString('o')
} | ConvertTo-Json | Set-Content -Path $StateFile -Encoding UTF8

# 5. Browser ------------------------------------------------------------------
if (-not $NoBrowser) {
	Write-Banner 'Opening the Portal in your browser'
	Start-Process $PortalUrl | Out-Null
}

Write-Host ''
Write-Host '  All set - the stack is up:' -ForegroundColor Green
Write-Host "    Portal    $PortalUrl" -ForegroundColor White
Write-Host "    API       $ApiUrl  (Swagger: $ApiUrl/swagger)" -ForegroundColor White
if (-not $SkipDatabase) {
	Write-Host "    Database  localhost:3306  (container '$ContainerName')" -ForegroundColor White
}
Write-Host ''
Write-Host '  Stop everything with:  .\stop-all.ps1' -ForegroundColor DarkGray
Write-Host ''

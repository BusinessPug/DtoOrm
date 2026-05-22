#!/usr/bin/env pwsh
<#
.SYNOPSIS
	Packs all DtoOrm NuGet packages into the artifacts/ directory.

	Produces two packages:
	  - DtoOrm          (library + analyzer — DtoOrm.MariaDb project)
	  - DtoOrm.Cli      (dotnet tool — must be a separate package by NuGet design)

.PARAMETER Configuration
	Build configuration. Defaults to Release.

.PARAMETER Version
	Package version override. Defaults to the version in Directory.Build.props.

.EXAMPLE
	.\eng\pack.ps1
	.\eng\pack.ps1 -Version 1.0.0
#>
param(
	[string] $Configuration = "Release",
	[string] $Version = ""
)

$ErrorActionPreference = "Stop"
$root = "$PSScriptRoot\.."
$output = "$root\artifacts"

# DtoOrm.Core and DtoOrm.Generator are bundled inside DtoOrm — they are not packed separately.
$projects = @(
	"src\DtoOrm.MariaDb\DtoOrm.MariaDb.csproj",
	"src\DtoOrm.Cli\DtoOrm.Cli.csproj"
)

$versionArgs = if ($Version) { @("--property:Version=$Version") } else { @() }

Write-Host "Packing to: $output" -ForegroundColor Cyan

foreach ($project in $projects) {
	$projectPath = Join-Path $root $project
	Write-Host "`nPacking $project ..." -ForegroundColor Yellow
	dotnet pack $projectPath `
		--configuration $Configuration `
		--output $output `
		--no-build `
		@versionArgs
	if ($LASTEXITCODE -ne 0) { throw "Pack failed for $project" }
}

Write-Host "`nAll packages written to $output" -ForegroundColor Green
Get-ChildItem $output -Filter "*.nupkg" | Select-Object Name, @{ N="Size"; E={ "{0:N0} KB" -f ($_.Length / 1KB) } }

#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT"

dotnet restore DtoOrm.slnx
dotnet test tests/DtoOrm.Core.Tests/DtoOrm.Core.Tests.csproj -c Release
dotnet pack src/DtoOrm.Core/DtoOrm.Core.csproj -c Release -o artifacts/packages
dotnet pack src/DtoOrm.MariaDb/DtoOrm.MariaDb.csproj -c Release -o artifacts/packages
dotnet pack src/DtoOrm.Generator/DtoOrm.Generator.csproj -c Release -o artifacts/packages
dotnet pack src/DtoOrm.Cli/DtoOrm.Cli.csproj -c Release -o artifacts/packages

echo "Packages written to artifacts/packages"

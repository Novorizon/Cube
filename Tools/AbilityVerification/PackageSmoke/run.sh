#!/usr/bin/env bash
set -euo pipefail
ability_repository="$(cd "$(dirname "$0")/../../.." && pwd)"
ability_import="$(mktemp -d "${TMPDIR:-/tmp}/cube-ability-import.XXXXXX")"
trap 'rm -rf "$ability_import"' EXIT
mkdir -p "$ability_import/Consumer"
cp -R "$ability_repository/Assets/Scripts/Ability" "$ability_import/Ability"
cp "$ability_repository/Tools/AbilityVerification/Core/Unity.cs" "$ability_import/Consumer/Unity.cs"
cp "$ability_repository/Tools/AbilityVerification/PackageSmoke/"*.csproj "$ability_import/Consumer/"
cp "$ability_repository/Tools/AbilityVerification/PackageSmoke/Directory.Build.props" "$ability_import/Consumer/"
cp "$ability_repository/Tools/AbilityVerification/PackageSmoke/Program.cs" "$ability_import/Consumer/"
"${DOTNET:-dotnet}" run --project "$ability_import/Consumer/PackageSmoke.csproj" --configuration Release

#!/usr/bin/env bash
# Usage: scripts/run-mutation.sh <Model|Weather|Core> [--local]
# Runs Stryker.NET (MTP runner) on one project. Default: in the .NET SDK container (needs Docker, works on
# Windows too); --local runs the tool directly (CI, Linux/macOS). Reports land in src/Njord.<P>.Tests/StrykerOutput.
set -euo pipefail

project="${1:?project name required, e.g. Alerts}"
mode="${2:-docker}"
root="$(cd "$(dirname "$0")/.." && pwd)"
tests="Njord.${project}.Tests"
extra=()
if [ "$project" = Core ]; then
  extra=(-m "Configuration/ParameterSelection.cs" -m "Configuration/Polling/BudgetCalculator.cs" -m "Configuration/NjordOptionsValidator.cs" -m "Configuration/LegacyConfigKeys.cs")
fi
args=(--config-file ../stryker-config.json --project "Njord.${project}.csproj" "${extra[@]}")

if [ "$mode" = --local ]; then
  cd "$root/src/$tests"
  dotnet tool restore --tool-manifest "$root/.config/dotnet-tools.json" >/dev/null
  dotnet dotnet-stryker "${args[@]}"
else
  MSYS_NO_PATHCONV=1 docker run --rm -v "$(cd "$root" && (pwd -W 2>/dev/null || pwd)):/repo" -w "/repo/src/$tests" \
    mcr.microsoft.com/dotnet/sdk:10.0 bash -c \
    "dotnet tool restore --tool-manifest /repo/.config/dotnet-tools.json >/dev/null && dotnet dotnet-stryker ${args[*]}"
fi

#!/usr/bin/env bash
# Usage: scripts/run-mutation.sh [Project] [--local]
# No project: runs every target in src/mutation-targets.txt. With a project (e.g. Njord.Features): just that one.
# Default: inside the .NET SDK container (needs Docker, works on Windows too); --local runs the tool directly
# (CI, Linux/macOS). Reports land in src/<Project>.Tests/StrykerOutput.
set -euo pipefail

root="$(cd "$(dirname "$0")/.." && pwd)"
targets_file="$root/src/mutation-targets.txt"
local_mode=false
project=""

for arg in "$@"; do
  case "$arg" in
    --local) local_mode=true ;;
    *) project="$arg" ;;
  esac
done

targets=()
if [ -n "$project" ]; then
  targets=("$project")
else
  while IFS= read -r line; do
    line="$(echo "$line" | tr -d '[:space:]')"
    [ -z "$line" ] && continue
    [[ "$line" == \#* ]] && continue
    targets+=("$line")
  done < "$targets_file"
fi

run_stryker() {
  local target="$1"
  local tests="${target}.Tests"
  local args="--config-file ../stryker-config.json --project ${target}.csproj"
  echo "=== Mutating: $target (tests: $tests) ==="
  if $local_mode; then
    (
      cd "$root/src/$tests"
      dotnet tool restore --tool-manifest "$root/.config/dotnet-tools.json" >/dev/null
      dotnet dotnet-stryker $args
    )
  else
    MSYS_NO_PATHCONV=1 docker run --rm -v "$(cd "$root" && (pwd -W 2>/dev/null || pwd)):/repo" -w "/repo/src/$tests" \
      mcr.microsoft.com/dotnet/sdk:10.0 bash -c \
      "dotnet tool restore --tool-manifest /repo/.config/dotnet-tools.json >/dev/null && dotnet dotnet-stryker $args"
  fi
}

failed=()
for target in "${targets[@]}"; do
  if ! run_stryker "$target"; then
    failed+=("$target")
  fi
done

echo
echo "=== Mutation testing summary ==="
echo "Targets: ${#targets[@]}"
echo "Failed:  ${#failed[@]}"
if [ ${#failed[@]} -gt 0 ]; then
  echo "Failed projects: ${failed[*]}"
  exit 1
fi

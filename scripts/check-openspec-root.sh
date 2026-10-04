#!/usr/bin/env bash
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

hits="$(find "$root" \
    \( -name node_modules -o -name .git -o -name bin -o -name obj -o -name dist \) -prune -o \
    \( \( -type d -name openspec \) -o \( -type f \( -name 'openspec.yaml' -o -name 'openspec.yml' \) \) \) \
    -print | grep -vxF "$root/openspec" | grep -vxF "$root/ha/openspec" || true)"

# config files inside any openspec/ directory other than the root one are covered by the directory hit
if [ -n "$hits" ]; then
    echo "OpenSpec must live only in $root/openspec. Found:" >&2
    echo "$hits" >&2
    exit 1
fi

echo "OK: OpenSpec roots at $root/openspec and $root/ha/openspec"

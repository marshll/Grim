#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
TOOLS_PROJECT="${ROOT_DIR}/src/Grim.Tools/Grim.Tools.csproj"

echo "DEPRECATED: tools/validate-content.sh"
echo "Use: dotnet run --project src/Grim.Tools/Grim.Tools.csproj -- content validate"

if [[ ! -f "${TOOLS_PROJECT}" ]]; then
  echo "Grim.Tools project not found: ${TOOLS_PROJECT}"
  exit 1
fi

dotnet run --project "${TOOLS_PROJECT}" -- content validate --repo-root "${ROOT_DIR}"
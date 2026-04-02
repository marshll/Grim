#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
TOOLS_PROJECT="${ROOT_DIR}/src/Grim.Tools/Grim.Tools.csproj"

TARGET_ID=""

usage() {
  echo "Usage: $0 [--id <model_id>]"
  echo "Imports FBX assets defined in content/models/import-manifest.json, converts them to glTF,"
  echo "and upserts content/models/registry.json entries."
}

while [[ $# -gt 0 ]]; do
  case "$1" in
    --id)
      if [[ $# -lt 2 ]]; then
        echo "Missing value for --id"
        exit 1
      fi
      TARGET_ID="$2"
      shift 2
      ;;
    -h|--help)
      usage
      exit 0
      ;;
    *)
      echo "Unknown argument: $1"
      usage
      exit 1
      ;;
  esac
done

if [[ ! -f "${TOOLS_PROJECT}" ]]; then
  echo "Grim.Tools project not found: ${TOOLS_PROJECT}"
  exit 1
fi

echo "DEPRECATED: tools/import-models.sh"
echo "Use: dotnet run --project src/Grim.Tools/Grim.Tools.csproj -- models import [--id <model_id>]"

if [[ -n "${TARGET_ID}" ]]; then
  dotnet run --project "${TOOLS_PROJECT}" -- models import --id "${TARGET_ID}" --repo-root "${ROOT_DIR}"
else
  dotnet run --project "${TOOLS_PROJECT}" -- models import --repo-root "${ROOT_DIR}"
fi

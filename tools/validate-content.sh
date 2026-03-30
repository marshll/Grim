#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
CONTENT_DIR="${ROOT_DIR}/content"

echo "Validating content files in ${CONTENT_DIR}"

if ! command -v jq >/dev/null 2>&1; then
  echo "jq is required for validation. Install jq and retry."
  exit 1
fi

find "${CONTENT_DIR}" -type f -name "*.json" -print0 | while IFS= read -r -d '' file; do
  jq -e '.id and (.id | type == "string") and (.id | length > 0)' "${file}" >/dev/null
  echo "OK: ${file}"
done

echo "Validation complete"
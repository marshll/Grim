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
  case "${file}" in
    */content/models/registry.json)
      jq -e '.models and (.models | type == "array") and (.models | length > 0) and all(.models[]; .id and (.id | type == "string") and (.id | length > 0) and .gltfPath and (.gltfPath | type == "string") and (.gltfPath | length > 0))' "${file}" >/dev/null
      ;;
    */content/models/import-manifest.json)
      jq -e '.imports and (.imports | type == "array") and all(.imports[]; .id and (.id | type == "string") and (.id | length > 0) and .sourceFbx and (.sourceFbx | type == "string") and (.sourceFbx | length > 0) and .targetDir and (.targetDir | type == "string") and (.targetDir | length > 0) and ((.scale // 1) | type == "number"))' "${file}" >/dev/null
      ;;
    *)
      jq -e '.id and (.id | type == "string") and (.id | length > 0)' "${file}" >/dev/null
      ;;
  esac

  echo "OK: ${file}"
done

echo "Checking FBX -> glTF conversion freshness"
find "${CONTENT_DIR}" -type f -name "*.fbx" -print0 | while IFS= read -r -d '' fbx_file; do
  gltf_file="${fbx_file%.fbx}.gltf"
  if [[ ! -f "${gltf_file}" ]]; then
    echo "Missing converted glTF for ${fbx_file} (expected ${gltf_file})"
    exit 1
  fi

  if [[ "${fbx_file}" -nt "${gltf_file}" ]]; then
    echo "Stale conversion: ${fbx_file} is newer than ${gltf_file}"
    exit 1
  fi

  echo "OK: ${fbx_file} -> ${gltf_file}"
done

echo "Validation complete"
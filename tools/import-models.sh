#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
MANIFEST_PATH="${ROOT_DIR}/content/models/import-manifest.json"
REGISTRY_PATH="${ROOT_DIR}/content/models/registry.json"
CONVERTER_SCRIPT="${ROOT_DIR}/tools/convert-fbx-to-gltf.sh"

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

if ! command -v jq >/dev/null 2>&1; then
  echo "jq is required. Install jq and retry."
  exit 1
fi

if [[ ! -f "${MANIFEST_PATH}" ]]; then
  echo "Manifest not found: ${MANIFEST_PATH}"
  exit 1
fi

if [[ ! -f "${REGISTRY_PATH}" ]]; then
  echo "Registry not found: ${REGISTRY_PATH}"
  exit 1
fi

if [[ ! -x "${CONVERTER_SCRIPT}" ]]; then
  echo "Converter script is not executable: ${CONVERTER_SCRIPT}"
  echo "Run: chmod +x tools/convert-fbx-to-gltf.sh"
  exit 1
fi

validate_manifest='\
  .imports and (.imports | type == "array") and\
  all(.imports[];\
    .id and (.id | type == "string") and (.id | length > 0) and\
    .sourceFbx and (.sourceFbx | type == "string") and (.sourceFbx | length > 0) and\
    .targetDir and (.targetDir | type == "string") and (.targetDir | length > 0) and\
    ((.scale // 1) | type == "number")\
  )\
'

jq -e "${validate_manifest}" "${MANIFEST_PATH}" >/dev/null

if [[ -n "${TARGET_ID}" ]]; then
  mapfile -t import_items < <(jq -c --arg id "${TARGET_ID}" '.imports[] | select(.id == $id)' "${MANIFEST_PATH}")
  if [[ ${#import_items[@]} -eq 0 ]]; then
    echo "No import manifest entry with id '${TARGET_ID}'"
    exit 1
  fi
else
  mapfile -t import_items < <(jq -c '.imports[]' "${MANIFEST_PATH}")
fi

if [[ ${#import_items[@]} -eq 0 ]]; then
  echo "No imports found. Add entries to content/models/import-manifest.json"
  exit 0
fi

for item in "${import_items[@]}"; do
  id="$(jq -r '.id' <<< "${item}")"
  source_fbx_rel="$(jq -r '.sourceFbx' <<< "${item}")"
  target_dir_rel="$(jq -r '.targetDir' <<< "${item}")"
  output_file="$(jq -r '.outputFile // ("" + .id + ".gltf")' <<< "${item}")"
  scale="$(jq -r '(.scale // 1)' <<< "${item}")"

  source_fbx="${ROOT_DIR}/${source_fbx_rel}"
  output_gltf="${ROOT_DIR}/content/models/${target_dir_rel}/${output_file}"

  if [[ ! -f "${source_fbx}" ]]; then
    echo "Source FBX missing for '${id}': ${source_fbx}"
    exit 1
  fi

  echo "Importing ${id}"
  "${CONVERTER_SCRIPT}" "${source_fbx}" "${output_gltf}"

  output_gltf_rel="content/models/${target_dir_rel}/${output_file}"
  registry_gltf_path="${output_gltf_rel#content/models/}"

  entry_json="$(jq -n --arg id "${id}" --arg gltfPath "${registry_gltf_path}" --argjson scale "${scale}" '{id: $id, gltfPath: $gltfPath, scale: $scale}')"

  tmp_registry="${REGISTRY_PATH}.tmp"
  jq --argjson entry "${entry_json}" '
    .models = ((.models // []) | map(select(.id != $entry.id)) + [$entry] | sort_by(.id))
  ' "${REGISTRY_PATH}" > "${tmp_registry}"
  mv "${tmp_registry}" "${REGISTRY_PATH}"

  echo "Upserted registry entry: ${id} -> ${registry_gltf_path}"
done

echo "Import completed (${#import_items[@]} item(s))."

#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
TOOLS_PROJECT="${ROOT_DIR}/src/Grim.Tools/Grim.Tools.csproj"

if [[ $# -lt 2 ]]; then
  echo "Usage: $0 <input.fbx> <output.gltf>"
  echo "Example: $0 content/models/pillar/pillar.fbx content/models/pillar/pillar.gltf"
  exit 1
fi

INPUT_FBX="$1"
OUTPUT_GLTF="$2"

if [[ ! -f "${TOOLS_PROJECT}" ]]; then
  echo "Grim.Tools project not found: ${TOOLS_PROJECT}"
  exit 1
fi

echo "DEPRECATED: tools/convert-fbx-to-gltf.sh"
echo "Use: dotnet run --project src/Grim.Tools/Grim.Tools.csproj -- models convert <input.fbx> <output.gltf>"

dotnet run --project "${TOOLS_PROJECT}" -- models convert "${INPUT_FBX}" "${OUTPUT_GLTF}"

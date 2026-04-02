#!/usr/bin/env bash
set -euo pipefail

if [[ $# -lt 2 ]]; then
  echo "Usage: $0 <input.fbx> <output.gltf>"
  echo "Example: $0 content/models/pillar/pillar.fbx content/models/pillar/pillar.gltf"
  exit 1
fi

INPUT_FBX="$1"
OUTPUT_GLTF="$2"

if [[ ! -f "${INPUT_FBX}" ]]; then
  echo "Input file not found: ${INPUT_FBX}"
  exit 1
fi

mkdir -p "$(dirname "${OUTPUT_GLTF}")"

if command -v blender >/dev/null 2>&1; then
  blender --background --python-expr "import bpy;bpy.ops.wm.read_factory_settings(use_empty=True);bpy.ops.import_scene.fbx(filepath=r'${INPUT_FBX}');bpy.ops.export_scene.gltf(filepath=r'${OUTPUT_GLTF}', export_format='GLTF_SEPARATE')"
  echo "Converted with Blender: ${INPUT_FBX} -> ${OUTPUT_GLTF}"
  exit 0
fi

if command -v fbx2gltf >/dev/null 2>&1; then
  fbx2gltf -i "${INPUT_FBX}" -o "${OUTPUT_GLTF}"
  echo "Converted with fbx2gltf: ${INPUT_FBX} -> ${OUTPUT_GLTF}"
  exit 0
fi

echo "No converter found. Install Blender or fbx2gltf and retry."
exit 1

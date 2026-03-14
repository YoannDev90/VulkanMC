#!/usr/bin/env bash
set -euo pipefail

# Compile GLSL shaders to SPIR-V for Vulkan targets.
# Places .spv files next to the source shaders.

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
SHADER_DIR="$ROOT_DIR/VulkanMC/Shaders"

if [ ! -d "$SHADER_DIR" ]; then
  echo "Shader directory not found: $SHADER_DIR" >&2
  exit 1
fi

if command -v glslangValidator >/dev/null 2>&1; then
  COMPILER=glslangValidator
elif command -v glslc >/dev/null 2>&1; then
  COMPILER=glslc
else
  echo "No shader compiler found. Install glslangValidator or glslc." >&2
  exit 1
fi

echo "Using $COMPILER to compile shaders in $SHADER_DIR"

for shader in "$SHADER_DIR"/*.vert "$SHADER_DIR"/*.frag; do
  [ -e "$shader" ] || continue
  out="$shader.spv"
  echo "Compiling $(basename "$shader") -> $(basename "$out")"
  if [ "$COMPILER" = "glslangValidator" ]; then
    # glslangValidator -V already defines VULKAN; do not re-define it to avoid macro redefinition.
    glslangValidator -V -o "$out" "$shader"
  else
    glslc -DVULKAN -o "$out" "$shader"
  fi
done

echo "Shader compilation finished."

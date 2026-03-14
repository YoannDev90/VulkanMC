#!/usr/bin/env bash
set -euo pipefail

repo_root="$(git rev-parse --show-toplevel)"
project_dir="$repo_root/VulkanMC"
out_file="$project_dir/tree.txt"

if [[ ! -d "$project_dir" ]]; then
  echo "Error: project directory not found: $project_dir" >&2
  exit 1
fi

tmp_file="$(mktemp)"
trap 'rm -f "$tmp_file"' EXIT

# Include tracked + untracked files while excluding anything ignored by git.
(
  cd "$repo_root"
  git ls-files -co --exclude-standard \
    | grep '^VulkanMC/' \
    | while IFS= read -r path; do
        # Skip stale tracked paths that were renamed/moved and no longer exist on disk.
        [[ -e "$repo_root/$path" ]] || continue
        printf '%s\n' "${path#VulkanMC/}"
      done \
    | grep -v '^$' \
    | sort
) > "$tmp_file"

if [[ ! -s "$tmp_file" ]]; then
  echo "." > "$out_file"
  echo "Generated $out_file (empty tree)."
  exit 0
fi

if command -v tree >/dev/null 2>&1 && tree --help 2>&1 | grep -q -- '--fromfile'; then
  {
    echo "."
    tree --fromfile --noreport "$tmp_file"
  } > "$out_file"
else
  {
    echo "."
    sed 's#^#├── #' "$tmp_file"
  } > "$out_file"
fi

echo "Generated $out_file"

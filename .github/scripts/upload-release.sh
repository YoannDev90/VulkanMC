#!/bin/bash
set -e

VERSION=${VERSION:-"v1.0.0"}
UPLOAD_URL=$1

if [ -z "$GITHUB_TOKEN" ]; then
  echo "❌ GITHUB_TOKEN required"
  exit 1
fi

if [ -z "$UPLOAD_URL" ]; then
  echo "Creating release first..."
  
  # Créer la release
  CREATE_RESPONSE=$(curl -s -X POST \
    -H "Authorization: token $GITHUB_TOKEN" \
    -H "Accept: application/vnd.github.v3+json" \
    https://api.github.com/repos/${GITHUB_REPOSITORY}/releases \
    -d "{\"tag_name\":\"$VERSION\",\"name\":\"VulkanMC $VERSION\",\"draft\":false,\"prerelease\":false}")
  
  echo "$CREATE_RESPONSE"
  
  # Extraire upload_url
  UPLOAD_URL=$(echo "$CREATE_RESPONSE" | jq -r '.upload_url' | sed 's/{.*}//')
  
  if [ "$UPLOAD_URL" == "null" ]; then
    echo "❌ Failed to create release"
    exit 1
  fi
fi

echo "📤 Uploading to: $UPLOAD_URL"

# Fonction d'upload générique
upload_asset() {
  local file="$1"
  local name="$2"
  local content_type="${3:-application/octet-stream}"
  
  if [ ! -f "$file" ]; then
    echo "⚠️  Skipping $name (missing: $file)"
    return
  fi
  
  echo "📦 Uploading $name..."
  curl -s -w "\nHTTP: %{http_code}\n" -X POST \
    -H "Authorization: token $GITHUB_TOKEN" \
    -H "Content-Type: $content_type" \
    --data-binary @"$file" \
    "$UPLOAD_URL?name=$name" > /dev/null || echo "❌ Failed to upload $name"
  
  echo "✅ $name uploaded!"
}

# Tous les uploads avec bons Content-Type
upload_asset "build/vulkanmc-win-x64.exe" "vulkanmc-win-x64.exe" "application/octet-stream"
upload_asset "build/vulkanmc-linux-x64.tar.gz" "vulkanmc-linux-x64.tar.gz" "application/gzip"
upload_asset "build/vulkanmc-osx-x64" "vulkanmc-osx-x64" "application/octet-stream"
upload_asset "build/vulkanmc-osx-arm64" "vulkanmc-osx-arm64" "application/octet-stream"
upload_asset "pkg-deb/vulkanmc_1.0.0_amd64.deb" "vulkanmc_1.0.0_amd64.deb" "application/vnd.debian.binary-package"
upload_asset "pkg-rpm/vulkanmc-1.0.0-1.x86_64.rpm" "vulkanmc-1.0.0-1.x86_64.rpm" "application/x-rpm"
upload_asset "vulkanmc-1.0.0-1-x86_64.pkg.tar.zst" "vulkanmc-1.0.0-1-x86_64.pkg.tar.zst" "application/octet-stream"

echo "🎉 Release $VERSION terminée avec succès !"

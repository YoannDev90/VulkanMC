#!/bin/bash
set -e

mkdir -p build

echo "🏗️ Building Linux x64..."
dotnet publish VulkanMC/VulkanMC.csproj -c Release -r linux-x64 --self-contained -o build/linux

echo "🏗️ Building Windows x64..."
dotnet publish VulkanMC/VulkanMC.csproj \
  -c Release \
  -r win-x64 \
  --self-contained true \
  -p:PublishSingleFile=true \
  -p:IncludeNativeLibrariesForSelfExtract=true \
  -o build/win-single

echo "🏗️ macOS x64 Single File..."
dotnet publish VulkanMC/VulkanMC.csproj \
  -c Release \
  -r osx-x64 \
  --self-contained true \
  -p:PublishSingleFile=true \
  -p:IncludeNativeLibrariesForSelfExtract=true \
  -o build/osx-single

echo "🏗️ macOS ARM64 Single File..."
dotnet publish VulkanMC/VulkanMC.csproj \
  -c Release \
  -r osx-arm64 \
  --self-contained true \
  -p:PublishSingleFile=true \
  -p:IncludeNativeLibrariesForSelfExtract=true \
  -o build/osx-arm64-single

# Binaires finaux
cp build/win-single/VulkanMC.exe build/vulkanmc-win-x64.exe
cp build/osx-single/VulkanMC build/vulkanmc-osx-x64  
cp build/osx-arm64-single/VulkanMC build/vulkanmc-osx-arm64
tar -czvf build/vulkanmc-linux-x64.tar.gz -C build/linux .

# Vérification tailles
echo "📊 Tailles finales :"
ls -lh build/*.exe build/*.tar.gz build/vulkanmc-osx-* 2>/dev/null || true

echo "✅ Tous les builds terminés !"

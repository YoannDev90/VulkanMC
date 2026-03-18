#!/bin/bash
set -e

mkdir -p build
dotnet publish VulkanMC/VulkanMC.csproj -c Release -r linux-x64 --self-contained -o build/linux
dotnet publish VulkanMC/VulkanMC.csproj -c Release -r win-x64 --self-contained -o build/win
dotnet publish VulkanMC/VulkanMC.csproj -c Release -r osx-x64 --self-contained -o build/osx
dotnet publish VulkanMC/VulkanMC.csproj -c Release -r osx-arm64 --self-contained -o build/osx-arm64

# Binaires finaux
cp build/win/VulkanMC.exe build/vulkanmc-win-x64.exe
cp build/osx/VulkanMC build/vulkanmc-osx-x64
cp build/osx-arm64/VulkanMC build/vulkanmc-osx-arm64
tar -czvf build/vulkanmc-linux-x64.tar.gz -C build/linux .
echo "✅ All builds completed!"

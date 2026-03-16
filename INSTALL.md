# Installation & Build Guide

## Prerequisites
- .NET 8 SDK
- Vulkan-compatible GPU and drivers
- (Linux) Ruby (for fpm), tar, dpkg, rpm
- (macOS) zip

## Building from Source

### Linux (also builds Windows .exe)
1. Clone the repository:
   ```sh
   git clone https://github.com/YoannDev90/VulkanMC.git
   cd VulkanMC
   ```
2. Run the build workflow manually or use:
   ```sh
   dotnet publish VulkanMC/VulkanMC.csproj -c Release -r linux-x64 --self-contained -o build/linux
   dotnet publish VulkanMC/VulkanMC.csproj -c Release -r win-x64 --self-contained -o build/win
   mv build/win/VulkanMC.exe build/VulkanMC-win-x64.exe
   tar -czvf build/VulkanMC-linux-x64.tar.gz -C build/linux .
   sudo gem install --no-document fpm
   mkdir -p deb/usr/local/bin && cp build/linux/VulkanMC deb/usr/local/bin/
   fpm -s dir -t deb -n VulkanMC -v 1.0.0 deb/usr/local/bin/VulkanMC
   mkdir -p rpm/usr/local/bin && cp build/linux/VulkanMC rpm/usr/local/bin/
   fpm -s dir -t rpm -n VulkanMC -v 1.0.0 rpm/usr/local/bin/VulkanMC
   ```

### macOS
1. Clone the repository and run:
   ```sh
   dotnet publish VulkanMC/VulkanMC.csproj -c Release -r osx-x64 --self-contained -o build/mac
   zip -r build/VulkanMC-osx-x64.zip build/mac
   ```

### Windows
- Use the prebuilt .exe from the release artifacts or build with .NET SDK (if available).

## Running VulkanMC
- Run the executable for your platform:
  - Linux: `./VulkanMC`
  - Windows: `VulkanMC-win-x64.exe`
  - macOS: `./VulkanMC`

## Configuration
- Edit `VulkanMC/config.toml` for game settings (auto-jump, window state, etc).

## Troubleshooting
- Ensure Vulkan drivers are installed and up to date.
- For build errors, check .NET SDK and dependencies.
- Use GitHub issues for bug reports and feature requests.


# VulkanMC

VulkanMC is a Minecraft-inspired voxel game engine written in C# using Vulkan and Silk.NET. It aims to provide fast, modern rendering and flexible gameplay features for sandbox worlds.

---

## Table of Contents
- [VulkanMC](#vulkanmc)
  - [Table of Contents](#table-of-contents)
  - [Features](#features)
  - [Architecture](#architecture)
  - [Repository Structure](#repository-structure)
  - [Build \& Installation](#build--installation)
  - [Configuration](#configuration)
  - [Usage](#usage)
  - [Troubleshooting](#troubleshooting)
  - [Contributing](#contributing)
  - [License](#license)
  - [Links](#links)

---

## Features
- **Vulkan Rendering:** High-performance chunk rendering using Vulkan via Silk.NET.
- **Configurable Gameplay:** Auto-jump, jump force, and window state (fullscreen/maximized) via config.toml.
- **Robust Logger:** Colored, timestamped logs with deduplication and error reporting.
- **Minecraft-like Terrain:** Procedural terrain generation, surface spawn logic, and chunk pre-generation.
- **Manual Build Workflows:** GitHub Actions for Windows (.exe), Linux (.tar.gz, .deb, .rpm), and macOS (.zip).
- **Script Utilities:** Bash scripts for formatting, log filtering, and shader compilation.
- **Cross-Platform:** Build and run on Linux, Windows, and macOS.

---

## Architecture
VulkanMC is structured for modularity and performance:
- **Rendering:** Uses Vulkan for fast chunk and mesh rendering, with frustum culling and chunk visibility optimizations.
- **Game Logic:** Handles player movement, collision, gravity, and spawn logic similar to Minecraft.
- **Configuration:** Uses TOML files for runtime settings, parsed with Tomlyn.
- **Logging:** Central Logger utility for colored, deduplicated logs and error reporting.
- **Scripts:** Bash scripts for formatting, shader compilation, and log filtering.

---

## Repository Structure
- `VulkanMC/` — Main game source code
	- `Config.cs`, `Program.cs`, `config.toml` — Configuration and entry point
	- `Rendering/` — Rendering logic, chunk updates, mesh uploads
	- `Terrain/` — Terrain generation, noise, world logic
	- `UI/` — Text overlays and UI rendering
	- `Utils/` — Logger, Vertex utilities
	- `Platform/` — System metrics and platform-specific code
	- `Textures/`, `Shaders/` — Game assets
- `scripts/` — Formatting, log filtering, shader compilation scripts
- `.github/` — Workflows and issue templates

---

## Build & Installation
See [INSTALL.md](INSTALL.md) for detailed build instructions and supported platforms.
- Manual and GitHub Actions workflows for Windows, Linux, and macOS
- Cross-compilation for Windows .exe from Linux
- Linux packages: tar.gz, .deb, .rpm

---

## Configuration
- Edit `VulkanMC/config.toml` to customize gameplay and window settings:
	- `auto_jump`, `jump_force`, `window_state`, etc.
- Logger and error reporting are configurable via code and scripts.

---

## Usage
- Run the executable for your platform:
	- Linux: `./VulkanMC`
	- Windows: `VulkanMC-win-x64.exe`
	- macOS: `./VulkanMC`
- Use config.toml to adjust settings.
- Use scripts for formatting and log filtering.

---

## Troubleshooting
- Ensure Vulkan drivers are installed and up to date.
- For build errors, check .NET SDK and dependencies.
- Use GitHub issues for bug reports and feature requests (templates provided).
- See logs for error details (Logger utility).

---

## Contributing
- Pull requests and issues are welcome!
- Use the provided templates for bug reports and feature requests.
- Follow code style and commit guidelines.

---

## License
MIT License

---

## Links
- [GitHub Repository](https://github.com/YoannDev90/VulkanMC)
- [Silk.NET](https://github.com/dotnet/Silk.NET)
- [Vulkan](https://www.vulkan.org/)

---


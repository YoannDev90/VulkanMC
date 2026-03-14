# VulkanMC

A Minecraft-inspired voxel engine written in C# with Silk.NET and Vulkan.

## Features

- Vulkan renderer with partial-engine split (`Engine/Vulkan/*`).
- Procedural terrain generation (Simplex noise).
- Chunk meshing with runtime streaming.
- In-game debug text overlay (FPS, CPU/GPU/RAM, position, chunk).
- Configurable behavior through `VulkanMC/config.toml`.

## Screenshots

*(Coming soon...)*

## Getting Started

### Prerequisites

- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Vulkan SDK and compatible drivers.
- A GPU that supports Vulkan 1.2+.

### Installation

1. Clone the repository:
   ```bash
   git clone https://github.com/yourusername/VulkanMC.git
   cd VulkanMC
   ```

2. Build and run:
   ```bash
   dotnet run --project VulkanMC/VulkanMC.csproj
   ```

3. Regenerate the repository tree file:
   ```bash
   bash VulkanMC/tree.sh
   ```

## Controls

- **WASD**: Move
- **Space**: Jump
- **Mouse**: Look around
- **Esc**: Toggle pause/cursor lock

## Project Structure

- `VulkanMC/Core/`: app entrypoint and logging.
- `VulkanMC/Config/`: TOML configuration model/loader.
- `VulkanMC/Engine/Vulkan/`: Vulkan engine partial implementation.
- `VulkanMC/Terrain/`: terrain generation and world logic.
- `VulkanMC/UI/`: debug text overlay.
- `VulkanMC/Platform/`: OS-specific metrics providers.
- `VulkanMC/Graphics/`: render-facing geometry structs.
- `VulkanMC/Shaders/`: GLSL sources and SPIR-V binaries.
- `VulkanMC/tree.sh`: generates `VulkanMC/tree.txt` while respecting `.gitignore`.

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

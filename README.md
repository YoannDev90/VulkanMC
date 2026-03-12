# VulkanMC

A high-performance Minecraft-inspired voxel engine written in C# using **Silk.NET** and **Vulkan**.

## Features

- **Vulkan Rendering**: Modern graphics API for maximum performance.
- **Infinite Terrain (WIP)**: Procedural world generation using Simplex Noise.
- **Texture Atlas**: Optimized texture management for multiple block types (Grass, Stone, Snow, Dirt).
- **Smooth Physics**: Player movement with gravity, collisions, and configurable auto-jump.
- **LOD System**: Efficient Level of Detail for distant terrain chunks.

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

## Controls

- **WASD**: Move
- **Space**: Jump
- **Mouse**: Look around
- **Esc**: Exit

## Project Structure

- `World.cs`: Terrain generation and block logic.
- `VulkanEngine.*.cs`: Partial classes handling the Vulkan pipeline, resource management, and rendering loop.
- `Shaders/`: GLSL shader sources.
- `Textures/`: Block assets.

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

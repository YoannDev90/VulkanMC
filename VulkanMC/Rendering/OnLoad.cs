using Silk.NET.Input;
using Silk.NET.Maths;
using VulkanMC.Core;

namespace VulkanMC;

public partial class VulkanEngine
{
    private unsafe void OnLoad()
    {
        Logger.Info("Starting OnLoad...");
        var sw = System.Diagnostics.Stopwatch.StartNew();

        _input = _window!.CreateInput();
        _input.Mice[0].Cursor.CursorMode = CursorMode.Normal;
        _world = new World();
        _debugOverlay = new UI.TextOverlay();

        Logger.Info("Initializing Vulkan...");
        InitVulkan();

        Logger.Info("Pre-generating central 4 chunks...");
        for (int x = -1; x <= 0; x++)
        {
            for (int z = -1; z <= 0; z++)
            {
                var (v, i) = _world.GenerateChunk(x, z, Config.Data.Rendering.ChunkSize, Config.Data.Rendering.ChunkSize, 0);
                UploadMesh(new Vector2D<int>(x, z), v, i);
                Logger.Info($"Loaded spawn chunk ({x}, {z}): {v.Length} vertices.");
            }
        }

        float spawnH = _world.GetHeightAt(0, 0);
        int blockY = (int)MathF.Floor(spawnH);
        float targetY = blockY + 2.8f;
        // Attendre que le chunk central soit chargé
        if (_world.IsChunkLoaded(0, 0))
        {
            _cameraPos = new Vector3D<float>(0.5f, targetY, 0.5f);
            _spawnProtected = false;
            Logger.Debug($"Chunk central chargé, protection désactivée. CameraPos: {_cameraPos}");
        }
        else
        {
            // Place temporairement le joueur en hauteur
            _cameraPos = new Vector3D<float>(0.5f, spawnH + 10.0f, 0.5f);
            _spawnProtected = true;
            Logger.Debug($"Chunk central non chargé, protection activée. CameraPos: {_cameraPos}");
        }
        _verticalVelocity = 0;

        Logger.Info($"Camera spawn at: {_cameraPos} (Ground height: {spawnH})");

        _input.Mice[0].Cursor.CursorMode = CursorMode.Raw;

        sw.Stop();
        Logger.Info($"OnLoad completed in {sw.ElapsedMilliseconds}ms.");

        Task.Run(UpdateChunksLoop);
    }
}

using System.Runtime.InteropServices;
using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.Vulkan;
using System.Diagnostics;
using VulkanMC.Core;

namespace VulkanMC;

public partial class VulkanEngine
{
    private double _fpsTimer = 0;
    private int _fpsCounter = 0;
    private int _lastFps = 0;
    private bool _debugOverlay = true;

    private void OnUpdate(double dt)
    {
        _fpsTimer += dt;
        _fpsCounter++;

        if (_fpsTimer >= 1.0)
        {
            _lastFps = _fpsCounter;
            _fpsCounter = 0;
            _fpsTimer = 0;
            
            if (_window != null)
            {
                _window.Title = $"VulkanMC - FPS: {_lastFps}";
            }
        }

        if (_input!.Keyboards.Count > 0)
        {
            if (_input.Keyboards[0].IsKeyPressed(Key.Escape))
            {
                _isPaused = !_isPaused;
                _input.Mice[0].Cursor.CursorMode = _isPaused ? CursorMode.Normal : CursorMode.Raw;
                Thread.Sleep(200);
            }
            if (_input.Keyboards[0].IsKeyPressed(Key.Delete))
            {
                Environment.Exit(0);
            }
        }
        if (_isPaused) return;

        UpdatePositionWithCollisions((float)dt);

        if (_input.Mice.Count > 0)
        {
            _lastMouseX ??= _input.Mice[0].Position.X;
            _lastMouseY ??= _input.Mice[0].Position.Y;
            float dx = (float)(_input.Mice[0].Position.X - _lastMouseX.Value);
            float dy = (float)(_input.Mice[0].Position.Y - _lastMouseY.Value);
            _lastMouseX = _input.Mice[0].Position.X;
            _lastMouseY = _input.Mice[0].Position.Y;
            
            _yaw += dx * 0.1f;
            _pitch -= dy * 0.1f;
            _pitch = Math.Clamp(_pitch, -89f, 89f);
        }

        Vector3D<float> f;
        f.X = MathF.Cos(MathF.PI / 180 * _yaw) * MathF.Cos(MathF.PI / 180 * _pitch);
        f.Y = MathF.Sin(MathF.PI / 180 * _pitch);
        f.Z = MathF.Sin(MathF.PI / 180 * _yaw) * MathF.Cos(MathF.PI / 180 * _pitch);
        _cameraFront = Vector3D.Normalize(f);
    }

    private void UpdatePositionWithCollisions(float dt)
    {
        if (_input == null || _input.Keyboards.Count == 0) return;
        var k = _input.Keyboards[0];
        float s = 10f * dt;
        var nextPos = _cameraPos;
        var forward = new Vector3D<float>(_cameraFront.X, 0, _cameraFront.Z);
        if (forward.Length != 0) forward = Vector3D.Normalize(forward);
        var right = Vector3D.Normalize(Vector3D.Cross(forward, Vector3D<float>.UnitY));

        if (k.IsKeyPressed(Key.W)) nextPos += forward * s;
        if (k.IsKeyPressed(Key.S)) nextPos -= forward * s;
        if (k.IsKeyPressed(Key.A)) nextPos -= right * s;
        if (k.IsKeyPressed(Key.D)) nextPos += right * s;
        if (k.IsKeyPressed(Key.Space)) nextPos.Y += s;
        if (k.IsKeyPressed(Key.ShiftLeft)) nextPos.Y -= s;

        if (Config.Data.Physics.GravityEnabled)
        {
            _verticalVelocity -= 20f * dt;
            nextPos.Y += _verticalVelocity * dt;
            
            // Check collisions at feet and slightly below
            int blockX = (int)MathF.Floor(nextPos.X);
            int blockY = (int)MathF.Floor(nextPos.Y - 1.8f);
            int blockZ = (int)MathF.Floor(nextPos.Z);

            bool grounded = false;
            // Vérifier les collisions aux pieds (Y-1.8f) et légèrement au-dessus (Y-1.5f) pour plus de stabilité
            if (_world != null && (_world.IsBlockAt(blockX, blockY, blockZ) || 
                                   _world.IsBlockAt((int)MathF.Floor(nextPos.X + 0.3f), blockY, (int)MathF.Floor(nextPos.Z)) ||
                                   _world.IsBlockAt((int)MathF.Floor(nextPos.X - 0.3f), blockY, (int)MathF.Floor(nextPos.Z)) ||
                                   _world.IsBlockAt((int)MathF.Floor(nextPos.X), blockY, (int)MathF.Floor(nextPos.Z + 0.3f)) ||
                                   _world.IsBlockAt((int)MathF.Floor(nextPos.X), blockY, (int)MathF.Floor(nextPos.Z - 0.3f))))
            {
                // On place les pieds exactement au-dessus du bloc (Y = blockY + 1)
                // Donc la caméra (tête) est à Y = blockY + 1 + 1.8f = blockY + 2.8f
                // J'ajoute un très léger offset (0.01f) car sinon IsBlockAt pourrait renvoyer vrai au frame suivant
                nextPos.Y = (float)blockY + 2.81f; 
                _verticalVelocity = 0;
                grounded = true;
            }
            
            // Safety: Floor at Y=2.0 to stop infinite fall if world isn't loaded
            if (nextPos.Y < 2.0f)
            {
                nextPos.Y = 2.0f;
                _verticalVelocity = 0;
                grounded = true;
            }

            // Saut
            if (grounded && k.IsKeyPressed(Key.Space))
            {
                _verticalVelocity = 8.0f;
            }

            // Auto-Jump
            if (Config.Data.Physics.AutoJump && grounded && (k.IsKeyPressed(Key.W) || k.IsKeyPressed(Key.S) || k.IsKeyPressed(Key.A) || k.IsKeyPressed(Key.D)))
            {
                // Un saut naturel : on regarde si on avance vers un bloc d'un bloc de haut
                // On vérifie une position légèrement devant le joueur
                Vector3D<float> moveDir = nextPos - _cameraPos;
                moveDir.Y = 0;
                if (moveDir.Length > 0.001f)
                {
                    moveDir = Vector3D.Normalize(moveDir);
                    Vector3D<float> checkPos = nextPos + moveDir * 0.5f;
                    int cx = (int)MathF.Floor(checkPos.X);
                    int cy = (int)MathF.Floor(checkPos.Y - 0.8f); // On vérifie à hauteur de genou (un cran au dessus du bloc sur lequel on est)
                    int cz = (int)MathF.Floor(checkPos.Z);
                    
                    if (_world != null && _world.IsBlockAt(cx, cy, cz))
                    {
                        // On vérifie qu'il n'y a pas de bloc au dessus (pour ne pas se cogner la tête)
                        if (!_world.IsBlockAt(cx, cy + 1, cz))
                        {
                            _verticalVelocity = 5.5f; // Un saut un peu plus faible pour que ce soit fluide
                        }
                    }
                }
            }

            if (_frameCount % 60 == 0)
            {
                Logger.Debug($"[Physics] Pos: {nextPos.X:F1}, {nextPos.Y:F1}, {nextPos.Z:F1} | Grounded: {grounded} | Vel: {_verticalVelocity:F2}");
            }
        }
        _cameraPos = nextPos;
        
        if (_frameCount % 60 == 0)
        {
            Logger.Debug($"Pos: {_cameraPos.X:F2}, {_cameraPos.Y:F2}, {_cameraPos.Z:F2} | Vel: {_verticalVelocity:F2}");
        }
    }

    private unsafe void OnLoad()
    {
        Logger.Info("Starting OnLoad...");
        var sw = System.Diagnostics.Stopwatch.StartNew();

        _input = _window!.CreateInput();
        _input.Mice[0].Cursor.CursorMode = CursorMode.Normal;
        _world = new World();
        
        // Initialiser Vulkan AVANT pour pouvoir faire UploadMesh
        Logger.Info("Initializing Vulkan...");
        InitVulkan();

        // Générer les 4 chunks centraux pour un spawn sûr
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
        _cameraPos = new Vector3D<float>(0.5f, spawnH + 2.0f, 0.5f);
        _verticalVelocity = 0;
        
        Logger.Info($"Camera spawn at: {_cameraPos} (Ground height: {spawnH})");

        _input.Mice[0].Cursor.CursorMode = CursorMode.Raw;
        
        sw.Stop();
        Logger.Info($"OnLoad completed in {sw.ElapsedMilliseconds}ms.");

        Task.Run(UpdateChunksLoop);
    }

    private unsafe void UploadMesh(Vector2D<int> pos, Vertex[] vertices, uint[] indices)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var mesh = new ChunkMesh { ChunkPos = pos, IndexCount = (uint)indices.Length };
        
        ulong vSize = (ulong)(vertices.Length * sizeof(Vertex));
        CreateBuffer(vSize, BufferUsageFlags.VertexBufferBit, MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit, out mesh.VertexBuffer, out mesh.VertexMemory);
        void* vData; _vk!.MapMemory(_device, mesh.VertexMemory, 0, vSize, 0, &vData);
        fixed (Vertex* pV = vertices) { System.Buffer.MemoryCopy(pV, vData, (long)vSize, (long)vSize); }
        _vk.UnmapMemory(_device, mesh.VertexMemory);

        ulong iSize = (ulong)(indices.Length * sizeof(uint));
        CreateBuffer(iSize, BufferUsageFlags.IndexBufferBit, MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit, out mesh.IndexBuffer, out mesh.IndexMemory);
        void* iData; _vk.MapMemory(_device, mesh.IndexMemory, 0, iSize, 0, &iData);
        fixed (uint* pI = indices) { System.Buffer.MemoryCopy(pI, iData, (long)iSize, (long)iSize); }
        _vk.UnmapMemory(_device, mesh.IndexMemory);

        mesh.IsReady = true;
        _chunkMeshes[pos] = mesh;
        sw.Stop();
        Logger.Debug($"Uploaded chunk {pos} ({vertices.Length} verts) in {sw.ElapsedMilliseconds}ms.");
    }

    private void UpdateChunksLoop()
    {
        const int maxEffectiveRenderDistance = 12;
        const int maxUploadsPerPass = 6;

        while (_window != null)
        {
            if (_world == null) { Thread.Sleep(100); continue; }

            var camPos = _cameraPos;
            int renderDistance = Math.Clamp(Config.Data.Rendering.RenderDistanceThreshold, 2, maxEffectiveRenderDistance);
            int chunkSize = Config.Data.Rendering.ChunkSize;

            int centerChunkX = (int)MathF.Floor(camPos.X / chunkSize);
            int centerChunkZ = (int)MathF.Floor(camPos.Z / chunkSize);
            int uploadsQueued = 0;

            for (int x = -renderDistance; x <= renderDistance; x++)
            {
                for (int z = -renderDistance; z <= renderDistance; z++)
                {
                    // Circular loading area to avoid loading expensive corner chunks.
                    if (x * x + z * z > renderDistance * renderDistance) continue;

                    int chunkX = centerChunkX + x;
                    int chunkZ = centerChunkZ + z;
                    var pos = new Vector2D<int>(chunkX, chunkZ);

                    if (!_chunkMeshes.ContainsKey(pos))
                    {
                        var (vertices, indices) = _world.GenerateChunk(chunkX, chunkZ, chunkSize, chunkSize);
                        if (indices.Length > 0)
                        {
                            if (uploadsQueued >= maxUploadsPerPass) continue;
                            _pendingUploads.Enqueue(() => UploadMesh(pos, vertices, indices));
                            uploadsQueued++;
                        }
                    }
                }
            }
            Thread.Sleep(100);
        }
    }
}

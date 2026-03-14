using System.Runtime.CompilerServices;
using System.IO;
using System.Threading.Tasks;
using Silk.NET.Maths;
using AppConfig = VulkanMC.Config.Config;

namespace VulkanMC.Engine.Vulkan;

public partial class VulkanEngine
{
    private unsafe void UploadMesh(Vector2D<int> pos, Vertex[] vertices, uint[] indices)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var mesh = new ChunkMesh { ChunkPos = pos, IndexCount = (uint)indices.Length };
        // LOD will be inferred by caller if needed; default 0
        mesh.LOD = 0;

        ulong vSize = (ulong)(vertices.Length * sizeof(Vertex));
        CreateBuffer(vSize, Silk.NET.Vulkan.BufferUsageFlags.VertexBufferBit, Silk.NET.Vulkan.MemoryPropertyFlags.HostVisibleBit | Silk.NET.Vulkan.MemoryPropertyFlags.HostCoherentBit, out mesh.VertexBuffer, out mesh.VertexMemory);
        void* vData; _vk!.MapMemory(_device, mesh.VertexMemory, 0, vSize, 0, &vData);
        fixed (Vertex* pV = vertices) { System.Buffer.MemoryCopy(pV, vData, (long)vSize, (long)vSize); }
        _vk.UnmapMemory(_device, mesh.VertexMemory);

        ulong iSize = (ulong)(indices.Length * sizeof(uint));
        CreateBuffer(iSize, Silk.NET.Vulkan.BufferUsageFlags.IndexBufferBit, Silk.NET.Vulkan.MemoryPropertyFlags.HostVisibleBit | Silk.NET.Vulkan.MemoryPropertyFlags.HostCoherentBit, out mesh.IndexBuffer, out mesh.IndexMemory);
        void* iData; _vk.MapMemory(_device, mesh.IndexMemory, 0, iSize, 0, &iData);
        fixed (uint* pI = indices) { System.Buffer.MemoryCopy(pI, iData, (long)iSize, (long)iSize); }
        _vk.UnmapMemory(_device, mesh.IndexMemory);

        mesh.IsReady = true;
        _chunkMeshes[pos] = mesh;
        sw.Stop();
        Logger.Debug($"Uploaded chunk {pos} ({vertices.Length} verts) in {sw.ElapsedMilliseconds}ms.");
        try { SaveChunkMeshAsync(pos, vertices, indices); } catch { }
    }

    private static string ChunkMeshFilePath(Vector2D<int> pos)
    {
        try
        {
            var baseDir = AppContext.BaseDirectory ?? Directory.GetCurrentDirectory();
            var dir = Path.Combine(baseDir, "chunk_cache");
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            return Path.Combine(dir, $"mesh_{pos.X}_{pos.Y}.bin");
        }
        catch
        {
            var dir = Path.Combine(Directory.GetCurrentDirectory(), "chunk_cache");
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            return Path.Combine(dir, $"mesh_{pos.X}_{pos.Y}.bin");
        }
    }

    private void SaveChunkMeshAsync(Vector2D<int> pos, Vertex[] vertices, uint[] indices)
    {
        var path = ChunkMeshFilePath(pos);
        Task.Run(() =>
        {
            try
            {
                int vCount = vertices.Length;
                int iCount = indices.Length;
                int vSize = Unsafe.SizeOf<Vertex>();
                byte[] bytes = new byte[4 + 4 + (vCount * vSize) + (iCount * 4)];
                // header: vCount (int), iCount (int)
                System.Buffer.BlockCopy(new[] { vCount }, 0, bytes, 0, 4);
                System.Buffer.BlockCopy(new[] { iCount }, 0, bytes, 4, 4);
                // vertices
                var vertBytes = new byte[vCount * vSize];
                unsafe { fixed (byte* p = vertBytes) { System.Buffer.MemoryCopy(Unsafe.AsPointer(ref vertices[0]), p, vertBytes.Length, vertBytes.Length); } }
                System.Buffer.BlockCopy(vertBytes, 0, bytes, 8, vertBytes.Length);
                // indices
                var idxBytes = new byte[iCount * 4];
                System.Buffer.BlockCopy(indices, 0, idxBytes, 0, idxBytes.Length);
                System.Buffer.BlockCopy(idxBytes, 0, bytes, 8 + vertBytes.Length, idxBytes.Length);
                File.WriteAllBytes(path, bytes);
            }
            catch { }
        });
    }

    private bool TryLoadChunkMeshFromDisk(int chunkX, int chunkZ, out Vertex[] vertices, out uint[] indices)
    {
        vertices = Array.Empty<Vertex>();
        indices = Array.Empty<uint>();
        var path = ChunkMeshFilePath(new Vector2D<int>(chunkX, chunkZ));
        if (!File.Exists(path)) return false;
        try
        {
            var bytes = File.ReadAllBytes(path);
            if (bytes.Length < 8) return false;
            int vCount = BitConverter.ToInt32(bytes, 0);
            int iCount = BitConverter.ToInt32(bytes, 4);
            int vSize = Unsafe.SizeOf<Vertex>();
            if (bytes.Length != 8 + (vCount * vSize) + (iCount * 4)) return false;
            vertices = new Vertex[vCount];
            indices = new uint[iCount];
            var vertBytes = new byte[vCount * vSize];
            System.Buffer.BlockCopy(bytes, 8, vertBytes, 0, vertBytes.Length);
            unsafe { fixed (byte* p = vertBytes) { System.Buffer.MemoryCopy(p, Unsafe.AsPointer(ref vertices[0]), vertBytes.Length, vertBytes.Length); } }
            System.Buffer.BlockCopy(bytes, 8 + vertBytes.Length, indices, 0, iCount * 4);
            return true;
        }
        catch { return false; }
    }

    private void UpdateChunksLoop()
    {
        while (_window != null)
        {
            if (_world == null) { Thread.Sleep(100); continue; }

            var camPos = _cameraPos;
            var perf = AppConfig.Data.Performance;
            var safety = AppConfig.Data.Safety;

            int maxEffectiveRenderDistance = Math.Max(2, perf.MaxEffectiveRenderDistance);
            int renderDistance = Math.Clamp(AppConfig.Data.Rendering.RenderDistanceThreshold, 2, maxEffectiveRenderDistance);
            int chunkSize = AppConfig.Data.Rendering.ChunkSize;
            int maxUploadsPerPass = Math.Max(1, perf.ChunkUploadBudgetPerPass);
            int updateIntervalMs = Math.Clamp(perf.ChunkUpdateIntervalMs, 25, 1000);
            int maxPendingUploads = Math.Max(16, safety.MaxPendingUploadActions);

            int centerChunkX = (int)MathF.Floor(camPos.X / chunkSize);
            int centerChunkZ = (int)MathF.Floor(camPos.Z / chunkSize);
            int uploadsQueued = 0;

            if (safety.EnableResourceGuard)
            {
                bool pressure = IsSystemUnderPressure(safety);
                if (pressure)
                {
                    renderDistance = Math.Max(2, renderDistance - 2);
                    maxUploadsPerPass = Math.Max(1, maxUploadsPerPass / 2);
                    TrimChunksByDistance(centerChunkX, centerChunkZ, renderDistance, Math.Max(4, safety.UnloadBatchSize));
                }

                EnforceLoadedChunkBudget(centerChunkX, centerChunkZ, Math.Max(64, safety.MaxLoadedChunks), Math.Max(2, safety.UnloadBatchSize));
            }

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
                        // Distance-based LOD selection
                        int dx = chunkX - centerChunkX;
                        int dz = chunkZ - centerChunkZ;
                        float dist = MathF.Sqrt(dx * dx + dz * dz) * chunkSize;
                        int maxLod = 4;
                        int lod = 0;
                        if (dist > chunkSize * 2) lod = Math.Min(maxLod, (int)(dist / (chunkSize * 4)));

                        // Try to load mesh from disk to avoid regeneration
                        if (TryLoadChunkMeshFromDisk(chunkX, chunkZ, out var vertices, out var indices))
                        {
                            if (indices.Length > 0 && uploadsQueued < maxUploadsPerPass && _pendingUploads.Count < maxPendingUploads)
                            {
                                _pendingUploads.Enqueue(() => UploadMesh(pos, vertices, indices));
                                uploadsQueued++;
                            }
                        }
                        else
                        {
                            var (genV, genI) = _world.GenerateChunk(chunkX, chunkZ, chunkSize, chunkSize, lod);
                            if (genI.Length > 0 && uploadsQueued < maxUploadsPerPass && _pendingUploads.Count < maxPendingUploads)
                            {
                                _pendingUploads.Enqueue(() => UploadMesh(pos, genV, genI));
                                uploadsQueued++;
                            }
                        }
                    }
                }
            }
            Thread.Sleep(updateIntervalMs);
        }
    }

    private bool IsSystemUnderPressure(SafetyConfig safety)
    {
        var metrics = _systemMetricsProvider.GetMetrics();
        bool cpuPressure = IsMetricAbove(metrics.CpuUsagePercent, safety.CpuUsageSoftLimitPercent);
        bool gpuPressure = IsMetricAbove(metrics.GpuUsagePercent, safety.CpuUsageSoftLimitPercent);
        bool ramPressure = IsMetricAbove(metrics.RamUsagePercent, safety.RamUsageSoftLimitPercent);

        if (!(cpuPressure || gpuPressure || ramPressure))
        {
            return false;
        }

        long nowTick = Environment.TickCount64;
        if (nowTick >= _nextResourcePressureLogTick)
        {
            _nextResourcePressureLogTick = nowTick + Math.Max(100, safety.PressureCooldownMs);
            Logger.Warning(
                $"Resource guard active: CPU={FmtMetric(metrics.CpuUsagePercent)}% RAM={FmtMetric(metrics.RamUsagePercent)}% GPU={FmtMetric(metrics.GpuUsagePercent)}%. " +
                "Reducing chunk load and unloading distant meshes.");
        }

        return true;
    }

    private static bool IsMetricAbove(float? metric, float threshold)
    {
        if (!metric.HasValue || threshold <= 0.0f)
        {
            return false;
        }

        return metric.Value >= threshold;
    }

    private static string FmtMetric(float? value)
    {
        return value.HasValue ? value.Value.ToString("F1") : "n/a";
    }

    private void EnforceLoadedChunkBudget(int centerChunkX, int centerChunkZ, int maxLoadedChunks, int unloadBatchSize)
    {
        int loaded = _chunkMeshes.Count;
        if (loaded <= maxLoadedChunks)
        {
            return;
        }

        int toUnload = Math.Min(unloadBatchSize, loaded - maxLoadedChunks);
        if (toUnload <= 0)
        {
            return;
        }

        foreach (var pos in GetFarthestChunks(centerChunkX, centerChunkZ, toUnload))
        {
            UnloadChunkMesh(pos);
        }
    }

    private void TrimChunksByDistance(int centerChunkX, int centerChunkZ, int keepDistance, int unloadBatchSize)
    {
        int keepDistanceSq = keepDistance * keepDistance;
        var candidates = _chunkMeshes.Keys
            .Where(pos =>
            {
                int dx = pos.X - centerChunkX;
                int dz = pos.Y - centerChunkZ;
                return (dx * dx + dz * dz) > keepDistanceSq;
            })
            .OrderByDescending(pos =>
            {
                int dx = pos.X - centerChunkX;
                int dz = pos.Y - centerChunkZ;
                return dx * dx + dz * dz;
            })
            .Take(unloadBatchSize)
            .ToArray();

        foreach (var pos in candidates)
        {
            UnloadChunkMesh(pos);
        }
    }

    private Vector2D<int>[] GetFarthestChunks(int centerChunkX, int centerChunkZ, int take)
    {
        return _chunkMeshes.Keys
            .OrderByDescending(pos =>
            {
                int dx = pos.X - centerChunkX;
                int dz = pos.Y - centerChunkZ;
                return dx * dx + dz * dz;
            })
            .Take(take)
            .ToArray();
    }

    private unsafe void UnloadChunkMesh(Vector2D<int> pos)
    {
        if (!_chunkMeshes.TryRemove(pos, out var mesh))
        {
            return;
        }

        _world?.UnloadChunk(pos.X, pos.Y);
        _pendingUploads.Enqueue(() =>
        {
            if (_device.Handle == 0 || _vk == null)
            {
                return;
            }

            _vk.DestroyBuffer(_device, mesh.VertexBuffer, null);
            _vk.FreeMemory(_device, mesh.VertexMemory, null);
            _vk.DestroyBuffer(_device, mesh.IndexBuffer, null);
            _vk.FreeMemory(_device, mesh.IndexMemory, null);
        });
    }
}

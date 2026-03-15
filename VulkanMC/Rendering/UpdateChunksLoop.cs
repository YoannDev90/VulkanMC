using Silk.NET.Maths;
using VulkanMC.Core;

namespace VulkanMC;

public partial class VulkanEngine
{
    private void UpdateChunksLoop()
    {
        const int maxEffectiveRenderDistance = 12;

        while (_window != null)
        {
            if (_world == null) { Thread.Sleep(100); continue; }

            var camPos = _cameraPos;
            int renderDistance = Math.Clamp(Config.Data.Rendering.RenderDistanceThreshold, 2, maxEffectiveRenderDistance);
            int chunkSize = Config.Data.Rendering.ChunkSize;

            int centerChunkX = (int)MathF.Floor(camPos.X / chunkSize);
            int centerChunkZ = (int)MathF.Floor(camPos.Z / chunkSize);

            var chunksToLoad = new List<(int x, int z, float distSq)>();
            for (int x = -renderDistance; x <= renderDistance; x++)
            {
                for (int z = -renderDistance; z <= renderDistance; z++)
                {
                    float distSq = x * x + z * z;
                    if (distSq > renderDistance * renderDistance) continue;

                    int chunkX = centerChunkX + x;
                    int chunkZ = centerChunkZ + z;
                    var pos = new Vector2D<int>(chunkX, chunkZ);

                    if (!_chunkMeshes.ContainsKey(pos))
                    {
                        chunksToLoad.Add((chunkX, chunkZ, distSq));
                    }
                }
            }

            chunksToLoad.Sort((a, b) => a.distSq.CompareTo(b.distSq));

            Parallel.ForEach(chunksToLoad, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount }, chunk =>
            {
                var pos = new Vector2D<int>(chunk.x, chunk.z);
                if (!_chunkMeshes.ContainsKey(pos))
                {
                    var (vertices, indices) = _world.GenerateChunk(chunk.x, chunk.z, chunkSize, chunkSize);
                    if (indices.Length > 0)
                    {
                        _pendingUploads.Enqueue(() => UploadMesh(pos, vertices, indices));
                    }
                }
            });

            Thread.Sleep(100);
        }
    }
}

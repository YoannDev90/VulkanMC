using Silk.NET.Vulkan;
using Silk.NET.Maths;
using VulkanMC.Core;

namespace VulkanMC;

public partial class VulkanEngine
{
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
}

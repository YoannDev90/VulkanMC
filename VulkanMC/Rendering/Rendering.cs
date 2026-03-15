using System.Runtime.InteropServices;
using Silk.NET.Core.Native;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.KHR;
using Silk.NET.Maths;
using Matrix4x4 = System.Numerics.Matrix4x4;
using Semaphore = Silk.NET.Vulkan.Semaphore;
using VulkanMC.Core;

namespace VulkanMC;

public partial class VulkanEngine
{
    private unsafe void OnRender(double dt)
    {
        _vk!.WaitForFences(_device, 1, in _inFlightFence, true, ulong.MaxValue);
        _vk.ResetFences(_device, 1, in _inFlightFence);
        uint imageIndex = 0;
        var result = _khrSwapchain!.AcquireNextImage(_device, _swapchain, ulong.MaxValue, _imageAvailableSemaphore, default, ref imageIndex);
        if (result == Result.ErrorOutOfDateKhr) { RecreateSwapchain(); return; }
        DrawFrame(imageIndex, dt);

        var waitSem = _imageAvailableSemaphore;
        var waitStage = PipelineStageFlags.ColorAttachmentOutputBit;
        var signalSem = _renderFinishedSemaphore;
        var cmdBuf = _commandBuffers[imageIndex];

        var submitInfo = new SubmitInfo
        {
            SType = StructureType.SubmitInfo,
            WaitSemaphoreCount = 1,
            PWaitSemaphores = &waitSem,
            PWaitDstStageMask = &waitStage,
            CommandBufferCount = 1,
            PCommandBuffers = &cmdBuf,
            SignalSemaphoreCount = 1,
            PSignalSemaphores = &signalSem
        };
        _vk!.QueueSubmit(_graphicsQueue, 1, &submitInfo, _inFlightFence);

        var swapchain = _swapchain;
        var presentInfo = new PresentInfoKHR
        {
            SType = StructureType.PresentInfoKhr,
            WaitSemaphoreCount = 1,
            PWaitSemaphores = &signalSem,
            SwapchainCount = 1,
            PSwapchains = &swapchain,
            PImageIndices = &imageIndex
        };
        _khrSwapchain.QueuePresent(_graphicsQueue, &presentInfo);
    }

    private unsafe void DrawFrame(uint imageIndex, double dt)
    {
        ProcessPendingUploads();
        var cb = _commandBuffers[imageIndex];
        var beginInfo = new CommandBufferBeginInfo { SType = StructureType.CommandBufferBeginInfo };
        _vk!.BeginCommandBuffer(cb, &beginInfo);
        var clear = stackalloc ClearValue[2];
        clear[0].Color = new ClearColorValue(0.40f, 0.65f, 0.82f, 1.0f);
        clear[1].DepthStencil = new ClearDepthStencilValue(1.0f, 0);
        var rpInfo = new RenderPassBeginInfo { SType = StructureType.RenderPassBeginInfo, RenderPass = _renderPass, Framebuffer = _framebuffers[imageIndex], RenderArea = new Rect2D(new Offset2D(0, 0), _swapchainExtent), ClearValueCount = 2, PClearValues = clear };
        _vk.CmdBeginRenderPass(cb, &rpInfo, SubpassContents.Inline);
        _vk.CmdBindPipeline(cb, PipelineBindPoint.Graphics, _graphicsPipeline);
        _vk.CmdBindDescriptorSets(cb, PipelineBindPoint.Graphics, _pipelineLayout, 0, 1, ref _descriptorSet, 0, null);
        var view = Matrix4x4.CreateLookAt(Convert(_cameraPos), Convert(_cameraPos + _cameraFront), Convert(_cameraUp));
        var proj = Matrix4x4.CreatePerspectiveFieldOfView(1.0f, (float)_swapchainExtent.Width / _swapchainExtent.Height, 0.1f, 1000.0f);
        proj.M22 *= -1;
        var viewProj = view * proj;

        uint drawCount = 0;
        uint ignoredCount = 0;
        var frustum = new Frustum(viewProj);
        foreach (var mesh in _chunkMeshes.Values)
        {
            if (!mesh.IsReady) { ignoredCount++; Logger.Debug($"Ignored (not ready): chunk {mesh.ChunkPos}"); continue; }
            if (!IsChunkVisible(mesh, frustum))
            {
                ignoredCount++;
                Logger.Debug($"Ignored (culling): chunk {mesh.ChunkPos} boundsMin={mesh.BoundsMin} boundsMax={mesh.BoundsMax} cam={_cameraPos}");
                continue;
            }

            var push = new PushConstant { MVP = viewProj };
            _vk.CmdPushConstants(cb, _pipelineLayout, ShaderStageFlags.VertexBit, 0, (uint)sizeof(PushConstant), &push);
            var vBuf = mesh.VertexBuffer; ulong off = 0;
            _vk.CmdBindVertexBuffers(cb, 0, 1, &vBuf, &off);
            _vk.CmdBindIndexBuffer(cb, mesh.IndexBuffer, 0, IndexType.Uint32);
            _vk.CmdDrawIndexed(cb, mesh.IndexCount, 1, 0, 0, 0);
            drawCount++;
        }
        Logger.Info($"Chunks drawn: {drawCount}, ignored: {ignoredCount}, total: {_chunkMeshes.Count}");

        if (_debugOverlay != null)
        {
            _debugOverlay.Update(dt, _cameraPos, (int)Math.Floor(_cameraPos.X / 16), (int)Math.Floor(_cameraPos.Z / 16));
            DrawUi(cb);
        }

        _vk.CmdEndRenderPass(cb);
        _vk.EndCommandBuffer(cb);
        _frameCount++;
    }

    private bool IsChunkVisible(ChunkMesh mesh, Frustum frustum)
    {
        // Fallback: si bounds non initialisés, toujours afficher
        if (mesh.BoundsMin == mesh.BoundsMax)
            return true;
        return frustum.IsBoxInFrustum(mesh.BoundsMin, mesh.BoundsMax);
    }

    private unsafe void DrawUi(CommandBuffer cb)
    {
        if (_debugOverlay == null || string.IsNullOrEmpty(_debugOverlay.CurrentDebugString)) return;

        UpdateUiBuffers();
        if (_uiVertexCount == 0) return;

        _vk!.CmdBindPipeline(cb, PipelineBindPoint.Graphics, _uiPipeline);
        _vk.CmdBindDescriptorSets(cb, PipelineBindPoint.Graphics, _uiPipelineLayout, 0, 1, ref _uiDescriptorSet, 0, null);

        var push = new UiPushConstants { Scale = new Vector2D<float>(2.0f / _swapchainExtent.Width, 2.0f / _swapchainExtent.Height), Translate = new Vector2D<float>(-1, -0.95f) };
        _vk.CmdPushConstants(cb, _uiPipelineLayout, ShaderStageFlags.VertexBit, 0, (uint)sizeof(UiPushConstants), &push);

        ulong offset = 0;
        _vk.CmdBindVertexBuffers(cb, 0, 1, ref _uiVertexBuffer, ref offset);
        _vk.CmdDraw(cb, _uiVertexCount, 1, 0, 0);
    }

    private unsafe void UpdateUiBuffers()
    {
        if (_debugOverlay == null) return;
        string text = _debugOverlay.CurrentDebugString;
        var vertices = new List<float>();

        float x = 10;
        float y = 30; // Increased from 10 to 30 to move it down
        float scale = 1.0f;

        foreach (char c in text)
        {
            if (!_debugOverlay.Glyphs.TryGetValue(c, out var glyph)) continue;

            float x0 = x + glyph.Bearing.X * scale;
            float y1 = y + (glyph.Size.Y - glyph.Bearing.Y) * scale;
            float x1 = x0 + glyph.Size.X * scale;
            float y0 = y1 - glyph.Size.Y * scale;

            float u0 = glyph.UVStart.X;
            float v0 = glyph.UVStart.Y;
            float u1 = glyph.UVEnd.X;
            float v1 = glyph.UVEnd.Y;

            // Two triangles
            vertices.AddRange(new[] { x0, y0, u0, v0 });
            vertices.AddRange(new[] { x0, y1, u0, v1 });
            vertices.AddRange(new[] { x1, y1, u1, v1 });

            vertices.AddRange(new[] { x0, y0, u0, v0 });
            vertices.AddRange(new[] { x1, y1, u1, v1 });
            vertices.AddRange(new[] { x1, y0, u1, v0 });

            x += glyph.Advance * scale;
        }

        _uiVertexCount = (uint)(vertices.Count / 4);
        if (_uiVertexCount == 0) return;

        ulong size = (ulong)(vertices.Count * sizeof(float));
        if (_uiVertexBuffer.Handle == 0 || size > GetBufferSize(_uiVertexBuffer))
        {
            if (_uiVertexBuffer.Handle != 0)
            {
                _vk!.DestroyBuffer(_device, _uiVertexBuffer, null);
                _vk.FreeMemory(_device, _uiVertexMemory, null);
            }
            CreateBuffer(size, BufferUsageFlags.VertexBufferBit, MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit, out _uiVertexBuffer, out _uiVertexMemory);
        }

        void* data;
        _vk!.MapMemory(_device, _uiVertexMemory, 0, size, 0, &data);
        fixed (float* p = vertices.ToArray())
        {
            System.Buffer.MemoryCopy(p, data, (long)size, (long)size);
        }
        _vk.UnmapMemory(_device, _uiVertexMemory);
    }

    private ulong GetBufferSize(Silk.NET.Vulkan.Buffer buffer)
    {
        _vk!.GetBufferMemoryRequirements(_device, buffer, out var req);
        return req.Size;
    }

    private void RecreateSwapchain()
    {
        if (_device.Handle != 0)
        {
            _vk!.DeviceWaitIdle(_device);
        }
        InitVulkan();
    }
    private void ProcessPendingUploads() { while (_pendingUploads.TryDequeue(out var a)) a(); }
    private System.Numerics.Vector3 Convert(Vector3D<float> v) => new(v.X, v.Y, v.Z);
}

public class Frustum
{
    private Plane[] planes = new Plane[6];
    public Frustum(Matrix4x4 m)
    {
        planes[0] = new Plane(m.M14 + m.M11, m.M24 + m.M21, m.M34 + m.M31, m.M44 + m.M41); // Left
        planes[1] = new Plane(m.M14 - m.M11, m.M24 - m.M21, m.M34 - m.M31, m.M44 - m.M41); // Right
        planes[2] = new Plane(m.M14 - m.M12, m.M24 - m.M22, m.M34 - m.M32, m.M44 - m.M42); // Top
        planes[3] = new Plane(m.M14 + m.M12, m.M24 + m.M22, m.M34 + m.M32, m.M44 + m.M42); // Bottom
        planes[4] = new Plane(m.M13, m.M23, m.M33, m.M43); // Near
        planes[5] = new Plane(m.M14 - m.M13, m.M24 - m.M23, m.M34 - m.M33, m.M44 - m.M43); // Far
        for (int i = 0; i < 6; i++) planes[i].Normalize();
    }

    public bool IsBoxInFrustum(Vector3D<float> min, Vector3D<float> max)
    {
        for (int i = 0; i < 6; i++)
        {
            if (planes[i].Dot(new Vector3D<float>(min.X, min.Y, min.Z)) < 0 &&
                planes[i].Dot(new Vector3D<float>(max.X, min.Y, min.Z)) < 0 &&
                planes[i].Dot(new Vector3D<float>(min.X, max.Y, min.Z)) < 0 &&
                planes[i].Dot(new Vector3D<float>(max.X, max.Y, min.Z)) < 0 &&
                planes[i].Dot(new Vector3D<float>(min.X, min.Y, max.Z)) < 0 &&
                planes[i].Dot(new Vector3D<float>(max.X, min.Y, max.Z)) < 0 &&
                planes[i].Dot(new Vector3D<float>(min.X, max.Y, max.Z)) < 0 &&
                planes[i].Dot(new Vector3D<float>(max.X, max.Y, max.Z)) < 0)
            {
                return false;
            }
        }
        return true;
    }
}

public struct Plane
{
    public float A, B, C, D;
    public Plane(float a, float b, float c, float d) { A = a; B = b; C = c; D = d; }
    public void Normalize() { float len = MathF.Sqrt(A * A + B * B + C * C); A /= len; B /= len; C /= len; D /= len; }
    public float Dot(Vector3D<float> p) => A * p.X + B * p.Y + C * p.Z + D;
}

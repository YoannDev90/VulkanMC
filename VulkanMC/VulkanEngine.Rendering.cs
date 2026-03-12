using System.Runtime.InteropServices;
using Silk.NET.Core.Native;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.KHR;
using Silk.NET.Maths;
using Matrix4x4 = System.Numerics.Matrix4x4;
using Semaphore = Silk.NET.Vulkan.Semaphore;

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
        DrawFrame(imageIndex);

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

    private unsafe void DrawFrame(uint imageIndex)
    {
        ProcessPendingUploads();
        var cb = _commandBuffers[imageIndex];
        var beginInfo = new CommandBufferBeginInfo { SType = StructureType.CommandBufferBeginInfo };
        _vk!.BeginCommandBuffer(cb, &beginInfo);
        var clear = stackalloc ClearValue[2];
        clear[0].Color = new ClearColorValue(0.53f, 0.81f, 0.92f, 1.0f);
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
        foreach (var mesh in _chunkMeshes.Values)
        {
            if (!mesh.IsReady) continue;
            
            var push = new PushConstant { MVP = viewProj };
            _vk.CmdPushConstants(cb, _pipelineLayout, ShaderStageFlags.VertexBit, 0, (uint)sizeof(PushConstant), &push);
            var vBuf = mesh.VertexBuffer; ulong off = 0;
            _vk.CmdBindVertexBuffers(cb, 0, 1, &vBuf, &off);
            _vk.CmdBindIndexBuffer(cb, mesh.IndexBuffer, 0, IndexType.Uint32);
            _vk.CmdDrawIndexed(cb, mesh.IndexCount, 1, 0, 0, 0);
            drawCount++;
        }

        if (drawCount == 0 && _frameCount % 60 == 0)
        {
             Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [WARN] DrawFrame sending 0 draw calls! ChunkCount: {_chunkMeshes.Count}");
        }

        _vk.CmdEndRenderPass(cb);
        _vk.EndCommandBuffer(cb);
        _frameCount++;
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
    public void Normalize() { float len = MathF.Sqrt(A*A + B*B + C*C); A /= len; B /= len; C /= len; D /= len; } 
    public float Dot(Vector3D<float> p) => A * p.X + B * p.Y + C * p.Z + D;
}

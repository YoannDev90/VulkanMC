using System.Runtime.InteropServices;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.KHR;
using Silk.NET.Maths;

namespace VulkanMC;

public partial class VulkanEngine
{
    private unsafe void CreateFramebuffers()
    {
        _framebuffers = new Framebuffer[_swapchainImageViews.Length];
        for (int i = 0; i < _swapchainImageViews.Length; i++)
        {
            var atts = new ImageView[] { _colorImageView, _depthImageView, _swapchainImageViews[i] };
            fixed (ImageView* pAtts = atts)
            {
                var info = new FramebufferCreateInfo { SType = StructureType.FramebufferCreateInfo, RenderPass = _renderPass, AttachmentCount = 3, PAttachments = pAtts, Width = _swapchainExtent.Width, Height = _swapchainExtent.Height, Layers = 1 };
                if (_vk!.CreateFramebuffer(_device, &info, null, out _framebuffers[i]) != Result.Success) throw new Exception("FB split fail!");
            }
        }
    }

    private unsafe void CreateColorResources()
    {
        CreateImage(_swapchainExtent.Width, _swapchainExtent.Height, _swapchainFormat, ImageTiling.Optimal, ImageUsageFlags.TransientAttachmentBit | ImageUsageFlags.ColorAttachmentBit, MemoryPropertyFlags.DeviceLocalBit, out _colorImage, out _colorImageMemory, SampleCountFlags.Count4Bit);
        _colorImageView = CreateImageView(_colorImage, _swapchainFormat, ImageAspectFlags.ColorBit);
    }

    private unsafe void CreateDepthResources()
    {
        CreateImage(_swapchainExtent.Width, _swapchainExtent.Height, Format.D32Sfloat, ImageTiling.Optimal, ImageUsageFlags.DepthStencilAttachmentBit, MemoryPropertyFlags.DeviceLocalBit, out _depthImage, out _depthImageMemory, SampleCountFlags.Count4Bit);
        _depthImageView = CreateImageView(_depthImage, Format.D32Sfloat, ImageAspectFlags.DepthBit);
    }
}

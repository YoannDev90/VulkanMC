using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using Silk.NET.Core;
using Silk.NET.Core.Native;
using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.KHR;
using StbImageSharp;
using Buffer = Silk.NET.Vulkan.Buffer;
using Semaphore = Silk.NET.Vulkan.Semaphore;
using Matrix4x4 = System.Numerics.Matrix4x4;

namespace VulkanMC;

public partial class VulkanEngine
{
    private unsafe void InitVulkan()
    {
        _vk = Vk.GetApi();
        CreateInstance();
        CreateSurface();
        PickPhysicalDevice();
        CreateLogicalDevice();
        CreateSwapchain();
        CreateImageViews();
        CreateRenderPass();
        CreateDescriptorSetLayout();
        CreateGraphicsPipeline();
        CreateCommandPool();
        CreateColorResources(); 
        CreateDepthResources();
        CreateTextureImage();
        CreateTextureImageView();
        CreateTextureSampler();
        CreateDescriptorPool();
        CreateDescriptorSets();
        CreateFramebuffers();
        CreateCommandBuffers();
        CreateSyncObjects();
    }

    private unsafe void CreateInstance()
    {
        var appInfo = new ApplicationInfo { SType = StructureType.ApplicationInfo, PApplicationName = (byte*)Marshal.StringToHGlobalAnsi("VulkanMC"), ApiVersion = Vk.Version12 };
        var extensions = _window!.VkSurface!.GetRequiredExtensions(out var extCount);
        var createInfo = new InstanceCreateInfo { SType = StructureType.InstanceCreateInfo, PApplicationInfo = &appInfo, EnabledExtensionCount = extCount, PpEnabledExtensionNames = extensions };
        _vk!.CreateInstance(&createInfo, null, out _instance);
        if (!_vk.TryGetInstanceExtension(_instance, out _khrSurface))
        {
            throw new NotSupportedException("KHR_surface extension not found.");
        }
    }

    private unsafe void CreateSurface() => _surface = _window!.VkSurface!.Create<AllocationCallbacks>(_instance.ToHandle(), null).ToSurface();

    private unsafe void PickPhysicalDevice()
    {
        uint devCount = 0; _vk!.EnumeratePhysicalDevices(_instance, &devCount, null);
        var devs = stackalloc PhysicalDevice[(int)devCount]; _vk.EnumeratePhysicalDevices(_instance, &devCount, devs);
        _physicalDevice = devs[0]; // Defaulting for simplicity in split, but preferably use Config
    }

    private unsafe void CreateLogicalDevice()
    {
        float priority = 1.0f;
        _graphicsQueueFamilyIndex = 0; // Defaulting for simple split, but preferably use Config
        var queueInfo = new DeviceQueueCreateInfo { SType = StructureType.DeviceQueueCreateInfo, QueueFamilyIndex = _graphicsQueueFamilyIndex, QueueCount = 1, PQueuePriorities = &priority };
        var features = new PhysicalDeviceFeatures { SamplerAnisotropy = true };
        var exts = new[] { Silk.NET.Vulkan.Extensions.KHR.KhrSwapchain.ExtensionName };
        var pExts = (byte**)SilkMarshal.StringArrayToPtr(exts);
        var info = new DeviceCreateInfo { SType = StructureType.DeviceCreateInfo, QueueCreateInfoCount = 1, PQueueCreateInfos = &queueInfo, PEnabledFeatures = &features, EnabledExtensionCount = (uint)exts.Length, PpEnabledExtensionNames = pExts };
        _vk!.CreateDevice(_physicalDevice, &info, null, out _device);
        _vk.GetDeviceQueue(_device, 0, 0, out _graphicsQueue);
        SilkMarshal.Free((IntPtr)pExts);
        
        if (!_vk.TryGetDeviceExtension(_instance, _device, out _khrSwapchain))
        {
            throw new NotSupportedException("KHR_swapchain extension not found.");
        }
    }

    private unsafe void CreateSwapchain()
    {
        _khrSurface!.GetPhysicalDeviceSurfaceCapabilities(_physicalDevice, _surface, out var caps);

        uint formatCount = 0;
        _khrSurface.GetPhysicalDeviceSurfaceFormats(_physicalDevice, _surface, &formatCount, null);
        var formats = stackalloc SurfaceFormatKHR[(int)formatCount];
        _khrSurface.GetPhysicalDeviceSurfaceFormats(_physicalDevice, _surface, &formatCount, formats);

        bool formatFound = false;
        for (int i = 0; i < (int)formatCount; i++)
        {
            if (formats[i].Format == Format.B8G8R8A8Unorm || formats[i].Format == Format.R8G8B8A8Unorm)
            {
                _swapchainFormat = formats[i].Format;
                formatFound = true;
                break;
            }
        }

        if (!formatFound)
        {
            _swapchainFormat = formats[0].Format;
        }

        _swapchainExtent = caps.CurrentExtent;
        if (_swapchainExtent.Width == 0xFFFFFFFF)
        {
            _swapchainExtent = new Extent2D((uint)_window!.Size.X, (uint)_window!.Size.Y);
        }

        // Clamp extent between min and max supported by the surface
        _swapchainExtent.Width = Math.Clamp(_swapchainExtent.Width, caps.MinImageExtent.Width, caps.MaxImageExtent.Width);
        _swapchainExtent.Height = Math.Clamp(_swapchainExtent.Height, caps.MinImageExtent.Height, caps.MaxImageExtent.Height);

        // Ensure extent is valid (e.g. if window is minimized)
        if (_swapchainExtent.Width == 0 || _swapchainExtent.Height == 0)
        {
            _swapchainExtent = new Extent2D(1, 1);
        }

        uint imageCount = Math.Max(caps.MinImageCount, 3);
        if (caps.MaxImageCount > 0 && imageCount > caps.MaxImageCount)
        {
            imageCount = caps.MaxImageCount;
        }

        var info = new SwapchainCreateInfoKHR
        {
            SType = StructureType.SwapchainCreateInfoKhr,
            Surface = _surface,
            MinImageCount = imageCount,
            ImageFormat = _swapchainFormat,
            ImageColorSpace = ColorSpaceKHR.SpaceSrgbNonlinearKhr,
            ImageExtent = _swapchainExtent,
            ImageArrayLayers = 1,
            ImageUsage = ImageUsageFlags.ColorAttachmentBit,
            PreTransform = caps.CurrentTransform,
            CompositeAlpha = CompositeAlphaFlagsKHR.OpaqueBitKhr,
            PresentMode = PresentModeKHR.FifoKhr,
            Clipped = true
        };

        var result = _khrSwapchain!.CreateSwapchain(_device, &info, null, out _swapchain);
        if (result != Result.Success)
        {
            throw new Exception($"Failed to create swapchain: {result}");
        }

        uint count = 0;
        _khrSwapchain.GetSwapchainImages(_device, _swapchain, &count, null);
        if (count == 0)
        {
            throw new Exception("Swapchain returned 0 images.");
        }

        _swapchainImages = new Image[count];
        fixed (Image* pImages = _swapchainImages)
        {
            _khrSwapchain.GetSwapchainImages(_device, _swapchain, &count, pImages);
        }
    }

    private unsafe void CreateImageViews()
    {
        _swapchainImageViews = new ImageView[_swapchainImages.Length];
        for (int i = 0; i < _swapchainImages.Length; i++) _swapchainImageViews[i] = CreateImageView(_swapchainImages[i], _swapchainFormat, ImageAspectFlags.ColorBit);
    }

    private unsafe void CreateDescriptorSetLayout()
    {
        var binding0 = new DescriptorSetLayoutBinding { Binding = 0, DescriptorType = DescriptorType.CombinedImageSampler, DescriptorCount = 1, StageFlags = ShaderStageFlags.FragmentBit };
        var info = new DescriptorSetLayoutCreateInfo { SType = StructureType.DescriptorSetLayoutCreateInfo, BindingCount = 1, PBindings = &binding0 };
        _vk!.CreateDescriptorSetLayout(_device, &info, null, out _descriptorSetLayout);
    }

    private unsafe void CreateDescriptorPool()
    {
        var size = new DescriptorPoolSize { Type = DescriptorType.CombinedImageSampler, DescriptorCount = 1 };
        var info = new DescriptorPoolCreateInfo { SType = StructureType.DescriptorPoolCreateInfo, MaxSets = 1, PoolSizeCount = 1, PPoolSizes = &size };
        _vk!.CreateDescriptorPool(_device, &info, null, out _descriptorPool);
    }

    private unsafe void CreateDescriptorSets()
    {
        fixed(DescriptorSetLayout* pL = &_descriptorSetLayout) {
            var alloc = new DescriptorSetAllocateInfo { SType = StructureType.DescriptorSetAllocateInfo, DescriptorPool = _descriptorPool, DescriptorSetCount = 1, PSetLayouts = pL };
            _vk!.AllocateDescriptorSets(_device, &alloc, out _descriptorSet);
        }
        var imageInfo = new DescriptorImageInfo { ImageLayout = ImageLayout.ShaderReadOnlyOptimal, ImageView = _textureImageView, Sampler = _textureSampler };
        var write = new WriteDescriptorSet { SType = StructureType.WriteDescriptorSet, DstSet = _descriptorSet, DstBinding = 0, DstArrayElement = 0, DescriptorType = DescriptorType.CombinedImageSampler, DescriptorCount = 1, PImageInfo = &imageInfo };
        _vk.UpdateDescriptorSets(_device, 1, &write, 0, null);
    }

    private unsafe void CreateCommandPool()
    {
        var info = new CommandPoolCreateInfo { SType = StructureType.CommandPoolCreateInfo, QueueFamilyIndex = 0, Flags = CommandPoolCreateFlags.ResetCommandBufferBit };
        _vk!.CreateCommandPool(_device, &info, null, out _commandPool);
    }

    private unsafe void CreateCommandBuffers()
    {
        _commandBuffers = new CommandBuffer[_swapchainImages.Length];
        var alloc = new CommandBufferAllocateInfo { SType = StructureType.CommandBufferAllocateInfo, CommandPool = _commandPool, Level = CommandBufferLevel.Primary, CommandBufferCount = (uint)_commandBuffers.Length };
        fixed (CommandBuffer* pCmdBuffers = _commandBuffers)
        {
            _vk!.AllocateCommandBuffers(_device, &alloc, pCmdBuffers);
        }
    }

    private unsafe void CreateSyncObjects()
    {
        var sInfo = new SemaphoreCreateInfo { SType = StructureType.SemaphoreCreateInfo };
        var fInfo = new FenceCreateInfo { SType = StructureType.FenceCreateInfo, Flags = FenceCreateFlags.SignaledBit };
        _vk!.CreateSemaphore(_device, &sInfo, null, out _imageAvailableSemaphore);
        _vk.CreateSemaphore(_device, &sInfo, null, out _renderFinishedSemaphore);
        _vk.CreateFence(_device, &fInfo, null, out _inFlightFence);
    }
}

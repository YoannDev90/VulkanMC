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

namespace VulkanMC.Engine.Vulkan;

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
        
        CreateTextResources();
        // Supprimé car incompatible avec le backend Vulkan pur (nécessite OpenGL)
        // _imGuiController = new Silk.NET.OpenGL.Extensions.ImGui.ImGuiController(...);
    }

    private unsafe void CreateTextResources()
    {
        uint w = _debugOverlay.AtlasWidth;
        uint h = _debugOverlay.AtlasHeight;
        byte[] data = _debugOverlay.GetAtlasData();

        CreateImage(w, h, Format.R8Unorm, ImageTiling.Optimal, ImageUsageFlags.TransferDstBit | ImageUsageFlags.SampledBit, MemoryPropertyFlags.DeviceLocalBit, out _textAtlasImage, out _textAtlasMemory);
        
        ulong size = (ulong)data.Length;
        CreateBuffer(size, BufferUsageFlags.TransferSrcBit, MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit, out var staging, out var stagingMem);
        void* pData; _vk!.MapMemory(_device, stagingMem, 0, size, 0, &pData);
        fixed (byte* pSrc = data) { System.Buffer.MemoryCopy(pSrc, pData, (long)size, (long)size); }
        _vk.UnmapMemory(_device, stagingMem);

        TransitionImageLayout(_textAtlasImage, Format.R8Unorm, ImageLayout.Undefined, ImageLayout.TransferDstOptimal);
        CopyBufferToImage(staging, _textAtlasImage, w, h);
        TransitionImageLayout(_textAtlasImage, Format.R8Unorm, ImageLayout.TransferDstOptimal, ImageLayout.ShaderReadOnlyOptimal);

        _vk.DestroyBuffer(_device, staging, null);
        _vk.FreeMemory(_device, stagingMem, null);

        _textAtlasView = CreateImageView(_textAtlasImage, Format.R8Unorm, ImageAspectFlags.ColorBit);

        var binding = new DescriptorSetLayoutBinding { Binding = 0, DescriptorType = DescriptorType.CombinedImageSampler, DescriptorCount = 1, StageFlags = ShaderStageFlags.FragmentBit };
        var dsInfo = new DescriptorSetLayoutCreateInfo { SType = StructureType.DescriptorSetLayoutCreateInfo, BindingCount = 1, PBindings = &binding };
        _vk.CreateDescriptorSetLayout(_device, &dsInfo, null, out _textSetLayout);

        fixed (DescriptorSetLayout* pL = &_textSetLayout)
        {
            var alloc = new DescriptorSetAllocateInfo { SType = StructureType.DescriptorSetAllocateInfo, DescriptorPool = _descriptorPool, DescriptorSetCount = 1, PSetLayouts = pL };
            var result = _vk.AllocateDescriptorSets(_device, &alloc, out _textDescriptorSet);
            if (result != Result.Success)
            {
                throw new Exception($"Failed to allocate text descriptor set: {result}");
            }
        }

        var imgInfo = new DescriptorImageInfo 
        { 
            ImageLayout = ImageLayout.ShaderReadOnlyOptimal, 
            ImageView = _textAtlasView, 
            Sampler = _textureSampler 
        };
        
        var write = new WriteDescriptorSet 
        { 
            SType = StructureType.WriteDescriptorSet, 
            DstSet = _textDescriptorSet, 
            DstBinding = 0, 
            DstArrayElement = 0, 
            DescriptorType = DescriptorType.CombinedImageSampler, 
            DescriptorCount = 1, 
            PImageInfo = &imgInfo 
        };
        _vk.UpdateDescriptorSets(_device, 1, &write, 0, null);

        fixed (DescriptorSetLayout* pDSL = &_textSetLayout)
        {
            var pLayoutInfo = new PipelineLayoutCreateInfo { SType = StructureType.PipelineLayoutCreateInfo, SetLayoutCount = 1, PSetLayouts = pDSL };
            _vk.CreatePipelineLayout(_device, &pLayoutInfo, null, out _textPipelineLayout);
        }

        CreateTextPipeline();

        ulong vSize = (ulong)(sizeof(TextVertex) * 6 * 1024); 
        CreateBuffer(vSize, BufferUsageFlags.VertexBufferBit, MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit, out _textVertexBuffer, out _textVertexMemory);
    }

    private unsafe void CreateTextPipeline()
    {
        string baseDir = "/home/yoann/Documents/GitHub/VulkanMC/VulkanMC";
        byte[] vCode = File.ReadAllBytes(Path.Combine(baseDir, "Shaders/text.vert.spv"));
        byte[] fCode = File.ReadAllBytes(Path.Combine(baseDir, "Shaders/text.frag.spv"));
        var vMod = CreateShaderModule(vCode);
        var fMod = CreateShaderModule(fCode);
        var entry = (byte*)SilkMarshal.StringToPtr("main");

        var stages = stackalloc PipelineShaderStageCreateInfo[2];
        stages[0] = new PipelineShaderStageCreateInfo { SType = StructureType.PipelineShaderStageCreateInfo, Stage = ShaderStageFlags.VertexBit, Module = vMod, PName = entry };
        stages[1] = new PipelineShaderStageCreateInfo { SType = StructureType.PipelineShaderStageCreateInfo, Stage = ShaderStageFlags.FragmentBit, Module = fMod, PName = entry };

        var bindingDesc = new VertexInputBindingDescription { Binding = 0, Stride = (uint)sizeof(TextVertex), InputRate = VertexInputRate.Vertex };
        var attrDesc = stackalloc VertexInputAttributeDescription[2];
        attrDesc[0] = new VertexInputAttributeDescription { Binding = 0, Location = 0, Format = Format.R32G32Sfloat, Offset = 0 };
        attrDesc[1] = new VertexInputAttributeDescription { Binding = 0, Location = 1, Format = Format.R32G32Sfloat, Offset = 8 };

        var pInfo = new PipelineVertexInputStateCreateInfo { SType = StructureType.PipelineVertexInputStateCreateInfo, VertexBindingDescriptionCount = 1, PVertexBindingDescriptions = &bindingDesc, VertexAttributeDescriptionCount = 2, PVertexAttributeDescriptions = attrDesc };
        var iaInfo = new PipelineInputAssemblyStateCreateInfo { SType = StructureType.PipelineInputAssemblyStateCreateInfo, Topology = PrimitiveTopology.TriangleList };
        var vp = new Viewport(0, 0, _swapchainExtent.Width, _swapchainExtent.Height, 0, 1);
        var sc = new Rect2D(new Offset2D(0, 0), _swapchainExtent);
        var vpInfo = new PipelineViewportStateCreateInfo { SType = StructureType.PipelineViewportStateCreateInfo, ViewportCount = 1, PViewports = &vp, ScissorCount = 1, PScissors = &sc };
        var rsInfo = new PipelineRasterizationStateCreateInfo { SType = StructureType.PipelineRasterizationStateCreateInfo, CullMode = CullModeFlags.None, FrontFace = FrontFace.CounterClockwise, LineWidth = 1.0f };
        var msState = new PipelineMultisampleStateCreateInfo { SType = StructureType.PipelineMultisampleStateCreateInfo, RasterizationSamples = SampleCountFlags.Count4Bit };
        var dsInfo = new PipelineDepthStencilStateCreateInfo { SType = StructureType.PipelineDepthStencilStateCreateInfo, DepthTestEnable = false, DepthWriteEnable = false };
        
        var blend = new PipelineColorBlendAttachmentState { ColorWriteMask = ColorComponentFlags.RBit | ColorComponentFlags.GBit | ColorComponentFlags.BBit | ColorComponentFlags.ABit, BlendEnable = true, SrcColorBlendFactor = BlendFactor.SrcAlpha, DstColorBlendFactor = BlendFactor.OneMinusSrcAlpha, ColorBlendOp = BlendOp.Add, SrcAlphaBlendFactor = BlendFactor.One, DstAlphaBlendFactor = BlendFactor.Zero, AlphaBlendOp = BlendOp.Add };
        var cbInfo = new PipelineColorBlendStateCreateInfo { SType = StructureType.PipelineColorBlendStateCreateInfo, AttachmentCount = 1, PAttachments = &blend };

        var pipeInfo = new GraphicsPipelineCreateInfo { SType = StructureType.GraphicsPipelineCreateInfo, StageCount = 2, PStages = stages, PVertexInputState = &pInfo, PInputAssemblyState = &iaInfo, PViewportState = &vpInfo, PRasterizationState = &rsInfo, PMultisampleState = &msState, PDepthStencilState = &dsInfo, PColorBlendState = &cbInfo, Layout = _textPipelineLayout, RenderPass = _renderPass };
        _vk!.CreateGraphicsPipelines(_device, default, 1, &pipeInfo, null, out _textPipeline);

        _vk.DestroyShaderModule(_device, vMod, null);
        _vk.DestroyShaderModule(_device, fMod, null);
        SilkMarshal.Free((IntPtr)entry);
    }

    private struct TextVertex { public Vector2D<float> Pos; public Vector2D<float> UV; }

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
        uint devCount = 0;
        _vk!.EnumeratePhysicalDevices(_instance, &devCount, null);
        if (devCount == 0)
        {
            throw new Exception("No Vulkan physical devices found.");
        }

        var devs = stackalloc PhysicalDevice[(int)devCount];
        _vk.EnumeratePhysicalDevices(_instance, &devCount, devs);

        string? preferredGpuName = Config.Data.Hardware.PreferredGpuName;
        int bestScore = int.MinValue;
        PhysicalDevice bestDevice = default;
        uint bestQueueFamilyIndex = 0;

        for (int i = 0; i < devCount; i++)
        {
            var candidate = devs[i];
            _vk.GetPhysicalDeviceProperties(candidate, out var properties);
            string deviceName = SilkMarshal.PtrToString((nint)properties.DeviceName) ?? $"GPU {i}";

            if (!TryFindGraphicsPresentQueueFamily(candidate, out uint queueFamilyIndex))
            {
                Logger.Warning($"Skipping Vulkan GPU '{deviceName}' because it has no graphics+present queue family.");
                continue;
            }

            int score = ScorePhysicalDevice(properties, deviceName, preferredGpuName);
            Logger.Info($"Vulkan GPU candidate: {deviceName} | Type: {properties.DeviceType} | Score: {score} | QueueFamily: {queueFamilyIndex}");

            if (score > bestScore)
            {
                bestScore = score;
                bestDevice = candidate;
                bestQueueFamilyIndex = queueFamilyIndex;
            }
        }

        if (bestDevice.Handle == 0)
        {
            throw new Exception("No suitable Vulkan GPU found.");
        }

        _physicalDevice = bestDevice;
        _graphicsQueueFamilyIndex = bestQueueFamilyIndex;

        _vk.GetPhysicalDeviceProperties(_physicalDevice, out var selectedProperties);
        string selectedDeviceName = SilkMarshal.PtrToString((nint)selectedProperties.DeviceName) ?? "Unknown GPU";
        Logger.Info($"Selected Vulkan GPU: {selectedDeviceName} | Type: {selectedProperties.DeviceType} | QueueFamily: {_graphicsQueueFamilyIndex}");
    }

    private unsafe void CreateLogicalDevice()
    {
        float priority = 1.0f;
        var queueInfo = new DeviceQueueCreateInfo { SType = StructureType.DeviceQueueCreateInfo, QueueFamilyIndex = _graphicsQueueFamilyIndex, QueueCount = 1, PQueuePriorities = &priority };
        var features = new PhysicalDeviceFeatures { SamplerAnisotropy = true };
        var exts = new[] { Silk.NET.Vulkan.Extensions.KHR.KhrSwapchain.ExtensionName };
        var pExts = (byte**)SilkMarshal.StringArrayToPtr(exts);
        var info = new DeviceCreateInfo { SType = StructureType.DeviceCreateInfo, QueueCreateInfoCount = 1, PQueueCreateInfos = &queueInfo, PEnabledFeatures = &features, EnabledExtensionCount = (uint)exts.Length, PpEnabledExtensionNames = pExts };
        _vk!.CreateDevice(_physicalDevice, &info, null, out _device);
        _vk.GetDeviceQueue(_device, _graphicsQueueFamilyIndex, 0, out _graphicsQueue);
        SilkMarshal.Free((IntPtr)pExts);
        
        if (!_vk.TryGetDeviceExtension(_instance, _device, out _khrSwapchain))
        {
            throw new NotSupportedException("KHR_swapchain extension not found.");
        }
    }

    private unsafe bool TryFindGraphicsPresentQueueFamily(PhysicalDevice device, out uint queueFamilyIndex)
    {
        queueFamilyIndex = 0;

        uint queueFamilyCount = 0;
        _vk!.GetPhysicalDeviceQueueFamilyProperties(device, &queueFamilyCount, null);
        if (queueFamilyCount == 0)
        {
            return false;
        }

        var queueFamilies = stackalloc QueueFamilyProperties[(int)queueFamilyCount];
        _vk.GetPhysicalDeviceQueueFamilyProperties(device, &queueFamilyCount, queueFamilies);

        for (uint i = 0; i < queueFamilyCount; i++)
        {
            bool supportsGraphics = (queueFamilies[i].QueueFlags & QueueFlags.GraphicsBit) != 0;
            if (!supportsGraphics)
            {
                continue;
            }

            Bool32 supportsPresent = false;
            _khrSurface!.GetPhysicalDeviceSurfaceSupport(device, i, _surface, &supportsPresent);
            if (supportsPresent)
            {
                queueFamilyIndex = i;
                return true;
            }
        }

        return false;
    }

    private static int ScorePhysicalDevice(PhysicalDeviceProperties properties, string deviceName, string? preferredGpuName)
    {
        int score = properties.DeviceType switch
        {
            PhysicalDeviceType.DiscreteGpu => 10000,
            PhysicalDeviceType.IntegratedGpu => 5000,
            PhysicalDeviceType.VirtualGpu => 1000,
            PhysicalDeviceType.Cpu => 100,
            _ => 0
        };

        score += (int)properties.Limits.MaxImageDimension2D;

        if (!string.IsNullOrWhiteSpace(preferredGpuName) && deviceName.Contains(preferredGpuName, StringComparison.OrdinalIgnoreCase))
        {
            score += 100000;
        }

        return score;
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
        var size = new DescriptorPoolSize { Type = DescriptorType.CombinedImageSampler, DescriptorCount = 2 };
        var info = new DescriptorPoolCreateInfo { SType = StructureType.DescriptorPoolCreateInfo, MaxSets = 2, PoolSizeCount = 1, PPoolSizes = &size };
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

using System.Runtime.InteropServices;
using Silk.NET.Core.Native;
using Silk.NET.Vulkan;
using StbImageSharp;
using Buffer = Silk.NET.Vulkan.Buffer;

namespace VulkanMC;

public partial class VulkanEngine
{
    private unsafe void CreateImage(uint w, uint h, Format f, ImageTiling t, ImageUsageFlags u, MemoryPropertyFlags m, out Image img, out DeviceMemory mem, SampleCountFlags s = SampleCountFlags.Count1Bit)
    {
        var info = new ImageCreateInfo { SType = StructureType.ImageCreateInfo, ImageType = ImageType.Type2D, Extent = new Extent3D(w, h, 1), MipLevels = 1, ArrayLayers = 1, Format = f, Tiling = t, InitialLayout = ImageLayout.Undefined, Usage = u, SharingMode = SharingMode.Exclusive, Samples = s };
        _vk!.CreateImage(_device, &info, null, out img);
        _vk.GetImageMemoryRequirements(_device, img, out var req);
        var alloc = new MemoryAllocateInfo { SType = StructureType.MemoryAllocateInfo, AllocationSize = req.Size, MemoryTypeIndex = FindMemoryType(req.MemoryTypeBits, m) };
        _vk.AllocateMemory(_device, &alloc, null, out mem);
        _vk.BindImageMemory(_device, img, mem, 0);
    }

    private unsafe ImageView CreateImageView(Image img, Format f, ImageAspectFlags a)
    {
        var info = new ImageViewCreateInfo { SType = StructureType.ImageViewCreateInfo, Image = img, ViewType = ImageViewType.Type2D, Format = f, SubresourceRange = new ImageSubresourceRange(a, 0, 1, 0, 1) };
        _vk!.CreateImageView(_device, &info, null, out var view);
        return view;
    }

    private unsafe void CreateBuffer(ulong size, BufferUsageFlags u, MemoryPropertyFlags p, out Buffer buf, out DeviceMemory mem)
    {
        var info = new BufferCreateInfo { SType = StructureType.BufferCreateInfo, Size = size, Usage = u, SharingMode = SharingMode.Exclusive };
        _vk!.CreateBuffer(_device, &info, null, out buf);
        _vk.GetBufferMemoryRequirements(_device, buf, out var req);
        var alloc = new MemoryAllocateInfo { SType = StructureType.MemoryAllocateInfo, AllocationSize = req.Size, MemoryTypeIndex = FindMemoryType(req.MemoryTypeBits, p) };
        _vk.AllocateMemory(_device, &alloc, null, out mem);
        _vk.BindBufferMemory(_device, buf, mem, 0);
    }

    private uint FindMemoryType(uint filter, MemoryPropertyFlags props)
    {
        _vk!.GetPhysicalDeviceMemoryProperties(_physicalDevice, out var p);
        for (int i = 0; i < p.MemoryTypeCount; i++) if ((filter & (1 << i)) != 0 && (p.MemoryTypes[i].PropertyFlags & props) == props) return (uint)i;
        return 0;
    }

    private unsafe void TransitionImageLayout(Image img, Format f, ImageLayout oldL, ImageLayout newL)
    {
        var cb = BeginSingleTimeCommands();
        var barrier = new ImageMemoryBarrier { SType = StructureType.ImageMemoryBarrier, OldLayout = oldL, NewLayout = newL, SrcQueueFamilyIndex = Vk.QueueFamilyIgnored, DstQueueFamilyIndex = Vk.QueueFamilyIgnored, Image = img, SubresourceRange = new ImageSubresourceRange(ImageAspectFlags.ColorBit, 0, 1, 0, 1) };
        _vk!.CmdPipelineBarrier(cb, PipelineStageFlags.TopOfPipeBit, PipelineStageFlags.FragmentShaderBit, 0, 0, null, 0, null, 1, &barrier);
        EndSingleTimeCommands(cb);
    }

    private unsafe void CopyBufferToImage(Buffer buf, Image img, uint w, uint h)
    {
        var cb = BeginSingleTimeCommands();
        var region = new BufferImageCopy { BufferOffset = 0, ImageSubresource = new ImageSubresourceLayers(ImageAspectFlags.ColorBit, 0, 0, 1), ImageExtent = new Extent3D(w, h, 1) };
        _vk!.CmdCopyBufferToImage(cb, buf, img, ImageLayout.TransferDstOptimal, 1, &region);
        EndSingleTimeCommands(cb);
    }

    private unsafe CommandBuffer BeginSingleTimeCommands()
    {
        var alloc = new CommandBufferAllocateInfo { SType = StructureType.CommandBufferAllocateInfo, Level = CommandBufferLevel.Primary, CommandPool = _commandPool, CommandBufferCount = 1 };
        _vk!.AllocateCommandBuffers(_device, &alloc, out var cb);
        var begin = new CommandBufferBeginInfo { SType = StructureType.CommandBufferBeginInfo, Flags = CommandBufferUsageFlags.OneTimeSubmitBit };
        _vk.BeginCommandBuffer(cb, &begin);
        return cb;
    }

    private unsafe void EndSingleTimeCommands(CommandBuffer cb)
    {
        _vk!.EndCommandBuffer(cb);
        var submit = new SubmitInfo { SType = StructureType.SubmitInfo, CommandBufferCount = 1, PCommandBuffers = &cb };
        _vk.QueueSubmit(_graphicsQueue, 1, &submit, default);
        _vk.QueueWaitIdle(_graphicsQueue);
        _vk.FreeCommandBuffers(_device, _commandPool, 1, &cb);
    }

    private unsafe void CreateTextureImage()
    {
        string baseDir = "/home/yoann/Documents/GitHub/VulkanMC/VulkanMC";
        string[] textures = { "grass_block_top.png", "stone.png", "snow.png", "grass_block_side.png" };
        uint atlasWidth = 32;  // 2x2 grid of 16x16 textures
        uint atlasHeight = 32;

        byte[] atlasData = new byte[atlasWidth * atlasHeight * 4];

        for (int i = 0; i < textures.Length; i++)
        {
            string path = Path.Combine(baseDir, "Textures", "block", textures[i]);
            if (!File.Exists(path)) path = Path.Combine("Textures", "block", textures[i]);

            using var stream = File.OpenRead(path);
            var img = ImageResult.FromStream(stream, ColorComponents.RedGreenBlueAlpha);
            
            int offsetX = (i % 2) * 16;
            int offsetY = (i / 2) * 16;

            for (int y = 0; y < 16; y++)
            {
                for (int x = 0; x < 16; x++)
                {
                    int atlasIdx = ((offsetY + y) * (int)atlasWidth + (offsetX + x)) * 4;
                    int imgIdx = (y * 16 + x) * 4;
                    atlasData[atlasIdx] = img.Data[imgIdx];
                    atlasData[atlasIdx + 1] = img.Data[imgIdx + 1];
                    atlasData[atlasIdx + 2] = img.Data[imgIdx + 2];
                    atlasData[atlasIdx + 3] = img.Data[imgIdx + 3];
                }
            }
        }

        ulong size = (ulong)atlasData.Length;
        CreateBuffer(size, BufferUsageFlags.TransferSrcBit, MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit, out var staging, out var stagingMem);
        void* data; _vk!.MapMemory(_device, stagingMem, 0, size, 0, &data);
        fixed (byte* p = atlasData) { System.Buffer.MemoryCopy(p, data, (long)size, (long)size); }
        _vk.UnmapMemory(_device, stagingMem);

        CreateImage(atlasWidth, atlasHeight, Format.R8G8B8A8Srgb, ImageTiling.Optimal, ImageUsageFlags.TransferDstBit | ImageUsageFlags.SampledBit, MemoryPropertyFlags.DeviceLocalBit, out _textureImage, out _textureImageMemory);
        TransitionImageLayout(_textureImage, Format.R8G8B8A8Srgb, ImageLayout.Undefined, ImageLayout.TransferDstOptimal);
        CopyBufferToImage(staging, _textureImage, atlasWidth, atlasHeight);
        TransitionImageLayout(_textureImage, Format.R8G8B8A8Srgb, ImageLayout.TransferDstOptimal, ImageLayout.ShaderReadOnlyOptimal);
        _vk.DestroyBuffer(_device, staging, null); _vk.FreeMemory(_device, stagingMem, null);
    }

    private unsafe void CreateTextureImageView() => _textureImageView = CreateImageView(_textureImage, Format.R8G8B8A8Srgb, ImageAspectFlags.ColorBit);
    private unsafe void CreateTextureSampler()
    {
        var info = new SamplerCreateInfo { SType = StructureType.SamplerCreateInfo, MagFilter = Filter.Nearest, MinFilter = Filter.Nearest, AddressModeU = SamplerAddressMode.Repeat, AddressModeV = SamplerAddressMode.Repeat, AddressModeW = SamplerAddressMode.Repeat, AnisotropyEnable = true, MaxAnisotropy = 16, MipmapMode = SamplerMipmapMode.Linear };
        _vk!.CreateSampler(_device, &info, null, out _textureSampler);
    }
}

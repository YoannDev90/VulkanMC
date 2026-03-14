using System.Runtime.InteropServices;
using Silk.NET.Core.Native;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.KHR;
using Silk.NET.Maths;
using StbImageSharp;

namespace VulkanMC.Engine.Vulkan;

public partial class VulkanEngine
{
    private unsafe void CreateRenderPass()
    {
        var colorAtt = new AttachmentDescription { Format = _swapchainFormat, Samples = SampleCountFlags.Count4Bit, LoadOp = AttachmentLoadOp.Clear, StoreOp = AttachmentStoreOp.Store, StencilLoadOp = AttachmentLoadOp.DontCare, StencilStoreOp = AttachmentStoreOp.DontCare, InitialLayout = ImageLayout.Undefined, FinalLayout = ImageLayout.ColorAttachmentOptimal };
        var depthAtt = new AttachmentDescription { Format = Format.D32Sfloat, Samples = SampleCountFlags.Count4Bit, LoadOp = AttachmentLoadOp.Clear, StoreOp = AttachmentStoreOp.DontCare, StencilLoadOp = AttachmentLoadOp.DontCare, StencilStoreOp = AttachmentStoreOp.DontCare, InitialLayout = ImageLayout.Undefined, FinalLayout = ImageLayout.DepthStencilAttachmentOptimal };
        var colorRes = new AttachmentDescription { Format = _swapchainFormat, Samples = SampleCountFlags.Count1Bit, LoadOp = AttachmentLoadOp.DontCare, StoreOp = AttachmentStoreOp.Store, StencilLoadOp = AttachmentLoadOp.DontCare, StencilStoreOp = AttachmentStoreOp.DontCare, InitialLayout = ImageLayout.Undefined, FinalLayout = ImageLayout.PresentSrcKhr };

        var colorAttRef = new AttachmentReference { Attachment = 0, Layout = ImageLayout.ColorAttachmentOptimal };
        var depthAttRef = new AttachmentReference { Attachment = 1, Layout = ImageLayout.DepthStencilAttachmentOptimal };
        var colorResRef = new AttachmentReference { Attachment = 2, Layout = ImageLayout.ColorAttachmentOptimal };

        var subpass = new SubpassDescription { PipelineBindPoint = PipelineBindPoint.Graphics, ColorAttachmentCount = 1, PColorAttachments = &colorAttRef, PDepthStencilAttachment = &depthAttRef, PResolveAttachments = &colorResRef };
        var dependency = new SubpassDependency { SrcSubpass = Vk.SubpassExternal, DstSubpass = 0, SrcStageMask = PipelineStageFlags.ColorAttachmentOutputBit | PipelineStageFlags.EarlyFragmentTestsBit, SrcAccessMask = 0, DstStageMask = PipelineStageFlags.ColorAttachmentOutputBit | PipelineStageFlags.EarlyFragmentTestsBit, DstAccessMask = AccessFlags.ColorAttachmentWriteBit | AccessFlags.DepthStencilAttachmentWriteBit };
        var atts = stackalloc [] { colorAtt, depthAtt, colorRes };
        var info = new RenderPassCreateInfo { SType = StructureType.RenderPassCreateInfo, AttachmentCount = 3, PAttachments = atts, SubpassCount = 1, PSubpasses = &subpass, DependencyCount = 1, PDependencies = &dependency };
        if (_vk!.CreateRenderPass(_device, &info, null, out _renderPass) != Result.Success) throw new Exception("Failed to create RP!");
    }

    private unsafe void CreateGraphicsPipeline()
    {
        string baseDir = "/home/yoann/Documents/GitHub/VulkanMC/VulkanMC";
        bool useShaders = VulkanMC.Config.Config.Data.Rendering.UseShaders;
        // Log the minimal-shader fallback once when pipeline is created.
        if (!useShaders && !_hasLoggedShaderFallback)
        {
            Logger.Info("Shaders disabled via config; using minimal shader pipeline.");
            _hasLoggedShaderFallback = true;
        }
        string vPath = Path.Combine(baseDir, "Shaders", useShaders ? "shader.vert.spv" : "shader_min.vert.spv");
        string fPath = Path.Combine(baseDir, "Shaders", useShaders ? "shader.frag.spv" : "shader_min.frag.spv");

        if (!File.Exists(vPath)) vPath = "Shaders/shader.vert.spv";
        if (!File.Exists(fPath)) fPath = "Shaders/shader.frag.spv";

        if (!File.Exists(vPath) || !File.Exists(fPath))
        {
            // Fallback: use original shaders if minimal SPV not present
            string altV = Path.Combine(baseDir, "Shaders", "shader.vert.spv");
            string altF = Path.Combine(baseDir, "Shaders", "shader.frag.spv");
            if (File.Exists(altV) && File.Exists(altF))
            {
                vPath = altV; fPath = altF;
                Logger.Warning("Minimal shader SPV not found; falling back to full shaders.");
            }
        }

        byte[] vCode = File.ReadAllBytes(vPath);
        byte[] fCode = File.ReadAllBytes(fPath);
        var vMod = CreateShaderModule(vCode);
        var fMod = CreateShaderModule(fCode);
        var entry = (byte*)SilkMarshal.StringToPtr("main");
        var stages = stackalloc PipelineShaderStageCreateInfo[2];
        stages[0] = new PipelineShaderStageCreateInfo { SType = StructureType.PipelineShaderStageCreateInfo, Stage = ShaderStageFlags.VertexBit, Module = vMod, PName = entry };
        stages[1] = new PipelineShaderStageCreateInfo { SType = StructureType.PipelineShaderStageCreateInfo, Stage = ShaderStageFlags.FragmentBit, Module = fMod, PName = entry };

        var vInfo = Vertex.GetBindingDescription();
        var aInfo = Vertex.GetAttributeDescriptions();
        fixed (VertexInputAttributeDescription* pA = aInfo)
        {
            var pInfo = new PipelineVertexInputStateCreateInfo { SType = StructureType.PipelineVertexInputStateCreateInfo, VertexBindingDescriptionCount = 1, PVertexBindingDescriptions = &vInfo, VertexAttributeDescriptionCount = (uint)aInfo.Length, PVertexAttributeDescriptions = pA };
            var iaInfo = new PipelineInputAssemblyStateCreateInfo { SType = StructureType.PipelineInputAssemblyStateCreateInfo, Topology = PrimitiveTopology.TriangleList };
            var vp = new Viewport(0, 0, _swapchainExtent.Width, _swapchainExtent.Height, 0, 1);
            var sc = new Rect2D(new Offset2D(0, 0), _swapchainExtent);
            var vpInfo = new PipelineViewportStateCreateInfo { SType = StructureType.PipelineViewportStateCreateInfo, ViewportCount = 1, PViewports = &vp, ScissorCount = 1, PScissors = &sc };
            var dynamicStates = stackalloc DynamicState[2] { DynamicState.Viewport, DynamicState.Scissor };
            var dynamicStateInfo = new PipelineDynamicStateCreateInfo
            {
                SType = StructureType.PipelineDynamicStateCreateInfo,
                DynamicStateCount = 2,
                PDynamicStates = dynamicStates
            };
            var rsInfo = new PipelineRasterizationStateCreateInfo { SType = StructureType.PipelineRasterizationStateCreateInfo, CullMode = CullModeFlags.BackBit, FrontFace = FrontFace.CounterClockwise, LineWidth = 1.0f };
            var msInfo = new PipelineColorBlendAttachmentState { ColorWriteMask = ColorComponentFlags.RBit | ColorComponentFlags.GBit | ColorComponentFlags.BBit | ColorComponentFlags.ABit };
            var msState = new PipelineMultisampleStateCreateInfo { SType = StructureType.PipelineMultisampleStateCreateInfo, RasterizationSamples = SampleCountFlags.Count4Bit };
            var dsInfo = new PipelineDepthStencilStateCreateInfo { SType = StructureType.PipelineDepthStencilStateCreateInfo, DepthTestEnable = true, DepthWriteEnable = true, DepthCompareOp = CompareOp.Less };
            var cbInfo = new PipelineColorBlendStateCreateInfo { SType = StructureType.PipelineColorBlendStateCreateInfo, AttachmentCount = 1, PAttachments = &msInfo };
            var push = new PushConstantRange { StageFlags = ShaderStageFlags.VertexBit, Size = (uint)sizeof(PushConstant) };
            fixed (DescriptorSetLayout* pDSL = &_descriptorSetLayout)
            {
                var plInfo = new PipelineLayoutCreateInfo { SType = StructureType.PipelineLayoutCreateInfo, SetLayoutCount = 1, PSetLayouts = pDSL, PushConstantRangeCount = 1, PPushConstantRanges = &push };
                _vk!.CreatePipelineLayout(_device, &plInfo, null, out _pipelineLayout);
            }
            var pipeInfo = new GraphicsPipelineCreateInfo { SType = StructureType.GraphicsPipelineCreateInfo, StageCount = 2, PStages = stages, PVertexInputState = &pInfo, PInputAssemblyState = &iaInfo, PViewportState = &vpInfo, PDynamicState = &dynamicStateInfo, PRasterizationState = &rsInfo, PMultisampleState = &msState, PDepthStencilState = &dsInfo, PColorBlendState = &cbInfo, Layout = _pipelineLayout, RenderPass = _renderPass };
            _vk.CreateGraphicsPipelines(_device, default, 1, &pipeInfo, null, out _graphicsPipeline);
        }
        _vk.DestroyShaderModule(_device, vMod, null);
        _vk.DestroyShaderModule(_device, fMod, null);
        SilkMarshal.Free((IntPtr)entry);
    }

    private unsafe ShaderModule CreateShaderModule(byte[] code) { fixed (byte* p = code) { var info = new ShaderModuleCreateInfo { SType = StructureType.ShaderModuleCreateInfo, CodeSize = (nuint)code.Length, PCode = (uint*)p }; _vk!.CreateShaderModule(_device, &info, null, out var mod); return mod; } }
}

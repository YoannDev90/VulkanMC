using Silk.NET.Vulkan;
using Silk.NET.Maths;
using Silk.NET.Windowing;
using Silk.NET.Input;
using System.Collections.Concurrent;
using Buffer = Silk.NET.Vulkan.Buffer;
using Semaphore = Silk.NET.Vulkan.Semaphore;

namespace VulkanMC.Engine.Vulkan;

public partial class VulkanEngine : IDisposable
{
    private class ChunkMesh
    {
#pragma warning disable CS0649
        public Buffer VertexBuffer;
        public DeviceMemory VertexMemory;
        public Buffer IndexBuffer;
        public DeviceMemory IndexMemory;
        public uint IndexCount;
        public bool IsReady;
        public Vector2D<int> ChunkPos;
        public int LOD;
#pragma warning restore CS0649
    }

    private IWindow? _window;
    private Vk? _vk;
    private Instance _instance;
    private Device _device;
    private PhysicalDevice _physicalDevice;
    private Queue _graphicsQueue;
    private uint _graphicsQueueFamilyIndex;
    private SurfaceKHR _surface;
    private Silk.NET.Vulkan.Extensions.KHR.KhrSurface? _khrSurface;
    private Silk.NET.Vulkan.Extensions.KHR.KhrSwapchain? _khrSwapchain;
    private SwapchainKHR _swapchain;
    private Image[] _swapchainImages = [];
    private ImageView[] _swapchainImageViews = [];
    private Format _swapchainFormat;
    private Extent2D _swapchainExtent;
    private RenderPass _renderPass;
    private PipelineLayout _pipelineLayout;
    private Pipeline _graphicsPipeline;
    private PipelineLayout _textPipelineLayout;
    private Pipeline _textPipeline;
    private DescriptorSetLayout _textSetLayout;
    private DescriptorSet _textDescriptorSet;
    private Image _textAtlasImage;
    private DeviceMemory _textAtlasMemory;
    private ImageView _textAtlasView;
    private Buffer _textVertexBuffer;
    private DeviceMemory _textVertexMemory;

    private Framebuffer[] _framebuffers = [];
    private CommandPool _commandPool;
    private CommandBuffer[] _commandBuffers = [];
    private Semaphore _imageAvailableSemaphore;
    private Semaphore _renderFinishedSemaphore;
    private Fence _inFlightFence;

    private Vector3D<float> _cameraPos = new(0, 50, 0);
    private Vector3D<float> _cameraFront = new(0, 0, -1);
    private Vector3D<float> _cameraUp = Vector3D<float>.UnitY;
    
    private Image _textureImage;
    private DeviceMemory _textureImageMemory;
    private ImageView _textureImageView;
    private Sampler _textureSampler;
    private DescriptorPool _descriptorPool;
    private DescriptorSet _descriptorSet;
    private DescriptorSetLayout _descriptorSetLayout;

    private Image _colorImage;
    private DeviceMemory _colorImageMemory;
    private ImageView _colorImageView;
    private Image _depthImage;
    private DeviceMemory _depthImageMemory;
    private ImageView _depthImageView;

    private World? _world;
    private IInputContext? _input;
    private double? _lastMouseX, _lastMouseY;
    private float _yaw = -90f, _pitch = 0f;
    private float _verticalVelocity = 0;
    private bool _isPaused = false;
    private int _frameCount = 0;
    private double _fpsTimer = 0;
    private int _lastFps = 0;
    private int _fpsCounter = 0;
    private DebugTextOverlay _debugOverlay = new();
    // Supprimé car incompatible avec Vulkan
    // private Silk.NET.OpenGL.Extensions.ImGui.ImGuiController? _imGuiController;

    private ConcurrentDictionary<Vector2D<int>, ChunkMesh> _chunkMeshes = new();
    private ConcurrentQueue<Action> _pendingUploads = new();

    public struct PushConstant { public System.Numerics.Matrix4x4 MVP; }

    public VulkanEngine()
    {
        Config.Load();
        var opt = WindowOptions.DefaultVulkan with
        {
            Size = new Vector2D<int>(1280, 720),
            Title = "VulkanMC",
            FramesPerSecond = 0,
            UpdatesPerSecond = 60
        };
        _window = Window.Create(opt);
        _window.Load += OnLoad;
        _window.Update += (dt) => OnUpdate(dt);
        _window.Render += (dt) => OnRender(dt);
        _window.Closing += OnClosing;
    }

    private void OnClosing() => Dispose();

    public void Run() => _window?.Run();

    public unsafe void Dispose()
    {
        if (_device.Handle != 0)
        {
            _vk?.DeviceWaitIdle(_device);
            foreach (var mesh in _chunkMeshes.Values)
            {
                _vk?.DestroyBuffer(_device, mesh.VertexBuffer, null);
                _vk?.FreeMemory(_device, mesh.VertexMemory, null);
            }
            _vk?.DestroyDevice(_device, null);
            _device = default;
        }
    }
}

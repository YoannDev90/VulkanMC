using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.IO;
using System.Threading.Tasks;
using System.Linq;
using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.Vulkan;
using AppConfig = VulkanMC.Config.Config;

namespace VulkanMC.Engine.Vulkan;

public partial class VulkanEngine
{
    private int _lastFootBlockX = int.MinValue;
    private int _lastFootBlockZ = int.MinValue;

    private void OnUpdate(double dt)
    {
        _fpsTimer += dt;
        _fpsCounter++;
        
        int cx = (int)MathF.Floor(_cameraPos.X / AppConfig.Data.Rendering.ChunkSize);
        int cz = (int)MathF.Floor(_cameraPos.Z / AppConfig.Data.Rendering.ChunkSize);
        _debugOverlay.Update(dt, _cameraPos, cx, cz);

        if (_fpsTimer >= 1.0)
        {
            _lastFps = _fpsCounter;
            _fpsCounter = 0;
            _fpsTimer -= 1.0;
            
            if (_window != null)
            {
                _window.Title = AppConfig.Data.Debug.ShowFpsInWindowTitle
                    ? $"{AppConfig.Data.Window.Title} | FPS: {_lastFps}"
                    : AppConfig.Data.Window.Title;
            }
        }

        var controls = AppConfig.Data.Controls;
        if (_input!.Keyboards[0].IsKeyPressed(controls.EscapeKey))
        {
            _isPaused = !_isPaused;
            _input.Mice[0].Cursor.CursorMode = _isPaused ? CursorMode.Normal : CursorMode.Raw;
            Thread.Sleep(200);
        }
        if (_input.Keyboards[0].IsKeyPressed(controls.QuickExitKey))
        {
            Logger.Info("Quick exit requested by keybind.");
            _window?.Close();
            return;
        }
        if (_isPaused) return;

        UpdatePositionWithCollisions((float)dt);

        _lastMouseX ??= _input.Mice[0].Position.X;
        _lastMouseY ??= _input.Mice[0].Position.Y;
        float dx = (float)(_input.Mice[0].Position.X - _lastMouseX.Value);
        float dy = (float)(_input.Mice[0].Position.Y - _lastMouseY.Value);
        _lastMouseX = _input.Mice[0].Position.X;
        _lastMouseY = _input.Mice[0].Position.Y;

        float mouseSensitivity = AppConfig.Data.Camera.MouseSensitivity;
        
        _yaw += dx * mouseSensitivity;
        _pitch -= dy * mouseSensitivity;
        _pitch = Math.Clamp(_pitch, AppConfig.Data.Camera.MinPitch, AppConfig.Data.Camera.MaxPitch);

        Vector3D<float> f;
        f.X = MathF.Cos(MathF.PI / 180 * _yaw) * MathF.Cos(MathF.PI / 180 * _pitch);
        f.Y = MathF.Sin(MathF.PI / 180 * _pitch);
        f.Z = MathF.Sin(MathF.PI / 180 * _yaw) * MathF.Cos(MathF.PI / 180 * _pitch);
        _cameraFront = Vector3D.Normalize(f);

        if (AppConfig.Data.Rendering.EnableEntities)
        {
            foreach (var entity in _entities)
            {
                entity.Update((float)dt);
            }
        }
    }

    // Debug frames counter to emit detailed physics traces right after spawn
    private int _physicsDebugFrames = 120;
    private void UpdatePositionWithCollisions(float dt)
    {
        var k = _input!.Keyboards[0];
        var physics = AppConfig.Data.Physics;

        // Clamp dt for physics to avoid huge jumps when the update loop experiences a large frame
        // time (e.g., when the app is resumed or a long GC pause occurs). Use a small maximum
        // step so gravity and movement remain stable.
        float simDt = MathF.Min(dt, 0.05f);

        var controls = AppConfig.Data.Controls;
        float moveSpeed = physics.BaseMovementSpeed;
        bool isCrouching = k.IsKeyPressed(controls.CrouchKey);
        bool isSprinting = k.IsKeyPressed(controls.SprintKey) && !isCrouching;
        if (isSprinting)
        {
            moveSpeed *= Math.Max(1.0f, physics.SprintMultiplier);
        }
        if (isCrouching)
        {
            moveSpeed *= Math.Max(0.01f, physics.CrouchMultiplier);
        }

        float s = moveSpeed * simDt;
        var nextPos = _cameraPos;
        var forward = new Vector3D<float>(_cameraFront.X, 0, _cameraFront.Z);
        if (forward.Length != 0) forward = Vector3D.Normalize(forward);
        var right = Vector3D.Normalize(Vector3D.Cross(forward, Vector3D<float>.UnitY));

        if (k.IsKeyPressed(controls.ForwardKey)) nextPos += forward * s;
        if (k.IsKeyPressed(controls.BackwardKey)) nextPos -= forward * s;
        if (k.IsKeyPressed(controls.LeftKey)) nextPos -= right * s;
        if (k.IsKeyPressed(controls.RightKey)) nextPos += right * s;

        if (physics.GravityEnabled)
        {
            if (_physicsDebugFrames > 0)
            {
                Logger.Info($"[PHYSICS DEBUG] Pre-gravity: dt={dt:F4} simDt={simDt:F4} y={nextPos.Y:F3} vel={_verticalVelocity:F3}");
            }
            // If within spawn grace window, clamp vertical velocity to avoid large negative impulses
            long nowTick = Environment.TickCount64;
            if (nowTick < _spawnGraceUntil)
            {
                _verticalVelocity = Math.Clamp(_verticalVelocity, -2.0f, 2.0f);
            }

            // Apply gravity
            _verticalVelocity -= physics.Gravity * simDt;
            // clamp maximum per-frame vertical travel to avoid tunnelling or extreme teleport
            float dy = _verticalVelocity * simDt;
            const float maxDyPerFrame = -10.0f;
            if (dy < maxDyPerFrame) dy = maxDyPerFrame;
            nextPos.Y += dy;

            if (_physicsDebugFrames > 0)
            {
                Logger.Info($"[PHYSICS DEBUG] Post-gravity: y={nextPos.Y:F3} vel={_verticalVelocity:F3}");
                _physicsDebugFrames--;
            }

            // Use a consistent eye height for the camera (meters/blocks)
            const float eyeHeight = 1.62f;
            // Subtract a tiny epsilon before flooring so that being exactly at integer heights
            // (eye sitting at spawnH + eyeHeight) maps to the block below as expected.
            float feetY = nextPos.Y - eyeHeight - 0.001f;
            int blockX = (int)MathF.Floor(nextPos.X);
            int blockY = (int)MathF.Floor(feetY);
            int blockZ = (int)MathF.Floor(nextPos.Z);

            bool grounded = false;
            if (_world != null)
            {
                // Check nearby blocks around feet for robust collision
                bool footBlock = _world.IsBlockAt(blockX, blockY, blockZ)
                    || _world.IsBlockAt((int)MathF.Floor(nextPos.X + 0.3f), blockY, (int)MathF.Floor(nextPos.Z))
                    || _world.IsBlockAt((int)MathF.Floor(nextPos.X - 0.3f), blockY, (int)MathF.Floor(nextPos.Z))
                    || _world.IsBlockAt((int)MathF.Floor(nextPos.X), blockY, (int)MathF.Floor(nextPos.Z + 0.3f))
                    || _world.IsBlockAt((int)MathF.Floor(nextPos.X), blockY, (int)MathF.Floor(nextPos.Z - 0.3f));

                if (footBlock)
                {
                    // Place camera so that eye is at top-of-block + eyeHeight
                    // blockY is the block index under the feet; the top surface is at blockY + 1
                    nextPos.Y = blockY + 1 + eyeHeight + 0.01f;
                    _verticalVelocity = 0;
                    grounded = true;
                }
            }

            // Safety floor to prevent falling through when world not loaded
            // During spawn grace, allow a bit more leeway above the very low safety floor
            if (nowTick < _spawnGraceUntil)
            {
                float minAllowedY = _initialSpawnY - 2.0f;
                if (nextPos.Y < minAllowedY)
                {
                    nextPos.Y = minAllowedY;
                    _verticalVelocity = 0;
                    grounded = true;
                    Logger.Warning($"Spawn-grace floor applied: nextPos.Y was below {minAllowedY:F2}, forcing to {minAllowedY:F2}. current camera pos XZ: {nextPos.X:F2},{nextPos.Z:F2}");
                }
            }
            else
            {
                if (nextPos.Y < 1.0f)
                {
                    nextPos.Y = 1.0f;
                    _verticalVelocity = 0;
                    grounded = true;
                    Logger.Warning($"Safety floor applied: nextPos.Y was below 1.0, forcing to 1.0. current camera pos XZ: {nextPos.X:F2},{nextPos.Z:F2}");
                }
            }

            // Jump (disabled while crouching)
            if (grounded && !isCrouching && k.IsKeyPressed(controls.JumpKey))
            {
                _verticalVelocity = physics.JumpForce;
            }

            // Auto-jump behaviour: check one block ahead at foot level
            if (physics.AutoJump && grounded && (k.IsKeyPressed(controls.ForwardKey) || k.IsKeyPressed(controls.BackwardKey) || k.IsKeyPressed(controls.LeftKey) || k.IsKeyPressed(controls.RightKey)))
            {
                Vector3D<float> moveDir = nextPos - _cameraPos;
                moveDir.Y = 0;
                if (moveDir.Length > 0.001f)
                {
                    moveDir = Vector3D.Normalize(moveDir);
                    Vector3D<float> checkPos = nextPos + moveDir * 0.6f;
                    int cx = (int)MathF.Floor(checkPos.X);
                    int cy = (int)MathF.Floor(checkPos.Y - eyeHeight + 1.0f);
                    int cz = (int)MathF.Floor(checkPos.Z);

                    if (_world != null && _world.IsBlockAt(cx, cy, cz))
                    {
                        if (!_world.IsBlockAt(cx, cy + 1, cz))
                        {
                            _verticalVelocity = MathF.Max(_verticalVelocity, physics.JumpForce * 0.9f);
                        }
                    }
                }
            }

            // Play footstep sound when moving across block boundaries while grounded
            try
            {
                int currentBlockX = (int)MathF.Floor(nextPos.X);
                int currentBlockZ = (int)MathF.Floor(nextPos.Z);
                if (grounded && (currentBlockX != _lastFootBlockX || currentBlockZ != _lastFootBlockZ))
                {
                    _lastFootBlockX = currentBlockX;
                    _lastFootBlockZ = currentBlockZ;
                    if (_world != null)
                    {
                        var bt = _world.GetBlockTypeAt(currentBlockX, blockY, currentBlockZ);
                        if (bt.HasValue)
                        {
                            VulkanMC.Engine.Audio.BackgroundSoundEngine.PlayStepSound(bt.Value);
                        }
                    }
                }
            }
            catch { }

            if (_frameCount % 60 == 0)
            {
                Logger.Debug($"[Physics] Pos: {nextPos.X:F1}, {nextPos.Y:F1}, {nextPos.Z:F1} | Grounded: {grounded} | Vel: {_verticalVelocity:F2}");
            }
        }
        _cameraPos = nextPos;
        
        if (_frameCount % 60 == 0)
        {
            Logger.Debug($"Pos: {_cameraPos.X:F2}, {_cameraPos.Y:F2}, {_cameraPos.Z:F2} | Vel: {_verticalVelocity:F2}");
        }
    }

    private unsafe void OnLoad()
    {
        Logger.Info("Starting OnLoad...");
        var sw = System.Diagnostics.Stopwatch.StartNew();

        _input = _window!.CreateInput();
        _input.Mice[0].Cursor.CursorMode = CursorMode.Normal;
        if (_world == null) _world = new World();
        
        // Initialiser Vulkan AVANT pour pouvoir faire UploadMesh
        Logger.Info("Initializing Vulkan...");
        InitVulkan();

        // Initialiser le moteur sonore d'arrière-plan (utilise un lecteur système si disponible)
        try { VulkanMC.Engine.Audio.BackgroundSoundEngine.Init(); } catch { }

        // Générer une zone centrale 3x3 pour un spawn sûr
        Logger.Info("Pre-generating central 9 chunks...");
        for (int x = -1; x <= 1; x++)
        {
            for (int z = -1; z <= 1; z++)
            {
                var (v, i) = _world.GenerateChunk(x, z, AppConfig.Data.Rendering.ChunkSize, AppConfig.Data.Rendering.ChunkSize, 0);
                UploadMesh(new Vector2D<int>(x, z), v, i);
                Logger.Info($"Loaded spawn chunk ({x}, {z}): {v.Length} vertices.");
            }
        }

        // Camera spawn was set earlier (constructor preloaded heights).
        _verticalVelocity = 0;

        // Ensure camera is placed above ground after chunks generated/uploaded
        try
        {
            const float eyeHeight = 1.62f;
            int camBlockX = (int)MathF.Floor(_cameraPos.X);
            int camBlockZ = (int)MathF.Floor(_cameraPos.Z);
            float groundH = _world?.GetHeightAt(camBlockX, camBlockZ) ?? 0;
            float desiredY = groundH + eyeHeight;
            if (_cameraPos.Y < desiredY)
            {
                Logger.Info($"Adjusting camera Y from {_cameraPos.Y:F2} up to {desiredY:F2} to avoid spawning underground.");
                _cameraPos = new Vector3D<float>(_cameraPos.X, desiredY, _cameraPos.Z);
            }
        }
        catch { }

        // Diagnostic: log final spawn and ground height
        try
        {
            int cx = (int)MathF.Floor(_cameraPos.X);
            int cz = (int)MathF.Floor(_cameraPos.Z);
            float gh = _world?.GetHeightAt(cx, cz) ?? -999;
            Logger.Info($"Final spawn camera: {_cameraPos} | Ground height at chunk({cx},{cz}) = {gh}");
        }
        catch { }

        _input.Mice[0].Cursor.CursorMode = AppConfig.Data.Camera.LockCursorWhenPlaying ? CursorMode.Raw : CursorMode.Normal;
        
        sw.Stop();
        Logger.Info($"OnLoad completed in {sw.ElapsedMilliseconds}ms.");

        Task.Run(UpdateChunksLoop);
    }

}

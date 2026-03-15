using System.Diagnostics;
using Silk.NET.Input;
using Silk.NET.Maths;
using VulkanMC.Core;

namespace VulkanMC;

public partial class VulkanEngine
{
    private void OnUpdate(double dt)
    {
        _fpsTimer += dt;
        _fpsCounter++;

        if (_fpsTimer >= 1.0)
        {
            _lastFps = _fpsCounter;
            _fpsCounter = 0;
            _fpsTimer = 0;

            if (_window != null)
            {
                _window.Title = Config.Data.Window.Title;
            }
        }

        _debugOverlay?.Update(dt, _cameraPos, (int)MathF.Floor(_cameraPos.X / Config.Data.Rendering.ChunkSize), (int)MathF.Floor(_cameraPos.Z / Config.Data.Rendering.ChunkSize));

        if (_input!.Keyboards.Count > 0)
        {
            if (_input.Keyboards[0].IsKeyPressed(Key.Escape))
            {
                _isPaused = !_isPaused;
                _input.Mice[0].Cursor.CursorMode = _isPaused ? CursorMode.Normal : CursorMode.Raw;
                Thread.Sleep(200);
            }
            if (_input.Keyboards[0].IsKeyPressed(Key.Delete))
            {
                Process.GetCurrentProcess().Kill();
            }
            // Natural auto-jump
            if (Config.Data.Physics.AutoJump)
            {
                // Check if player is on ground and moving forward
                bool isMoving = _input.Keyboards[0].IsKeyPressed(Config.Data.Controls.Forward) ||
                                _input.Keyboards[0].IsKeyPressed(Config.Data.Controls.Backward) ||
                                _input.Keyboards[0].IsKeyPressed(Config.Data.Controls.Left) ||
                                _input.Keyboards[0].IsKeyPressed(Config.Data.Controls.Right);
                bool isOnGround = IsPlayerOnGround();
                bool jumpKeyHeld = _input.Keyboards[0].IsKeyPressed(Config.Data.Controls.Jump);
                if (isMoving && isOnGround && !jumpKeyHeld)
                {
                    PerformJump(Config.Data.Physics.JumpForce);
                }
            }
        }
        if (_isPaused) return;

        UpdatePositionWithCollisions((float)dt);

        if (_input.Mice.Count > 0)
        {
            _lastMouseX ??= _input.Mice[0].Position.X;
            _lastMouseY ??= _input.Mice[0].Position.Y;
            float dx = (float)(_input.Mice[0].Position.X - _lastMouseX.Value);
            float dy = (float)(_input.Mice[0].Position.Y - _lastMouseY.Value);
            _lastMouseX = _input.Mice[0].Position.X;
            _lastMouseY = _input.Mice[0].Position.Y;

            _yaw += dx * 0.1f;
            _pitch -= dy * 0.1f;
            _pitch = Math.Clamp(_pitch, -89f, 89f);
        }

        Vector3D<float> f;
        f.X = MathF.Cos(MathF.PI / 180 * _yaw) * MathF.Cos(MathF.PI / 180 * _pitch);
        f.Y = MathF.Sin(MathF.PI / 180 * _pitch);
        f.Z = MathF.Sin(MathF.PI / 180 * _yaw) * MathF.Cos(MathF.PI / 180 * _pitch);
        _cameraFront = Vector3D.Normalize(f);
    }


    // Returns true if player is on ground
    private bool IsPlayerOnGround()
    {
        int blockX = (int)MathF.Floor(_cameraPos.X);
        int blockY = (int)MathF.Floor(_cameraPos.Y - 1.8f);
        int blockZ = (int)MathF.Floor(_cameraPos.Z);
        float targetY = (float)blockY + 2.8f;
        const float epsilon = 0.005f;
        bool blockDetected = _world != null && (
            _world.IsBlockAt(blockX, blockY, blockZ) ||
            _world.IsBlockAt((int)MathF.Floor(_cameraPos.X + 0.3f), blockY, (int)MathF.Floor(_cameraPos.Z)) ||
            _world.IsBlockAt((int)MathF.Floor(_cameraPos.X - 0.3f), blockY, (int)MathF.Floor(_cameraPos.Z)) ||
            _world.IsBlockAt((int)MathF.Floor(_cameraPos.X), blockY, (int)MathF.Floor(_cameraPos.Z + 0.3f)) ||
            _world.IsBlockAt((int)MathF.Floor(_cameraPos.X), blockY, (int)MathF.Floor(_cameraPos.Z - 0.3f)));
        if (blockDetected && _cameraPos.Y < targetY - epsilon)
            return true;
        if (_cameraPos.Y < 2.0f)
            return true;
        return false;
    }

    // Applies jump force to vertical velocity
    private void PerformJump(float jumpForce)
    {
        _verticalVelocity = jumpForce;
    }
}
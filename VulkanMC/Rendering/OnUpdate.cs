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
}

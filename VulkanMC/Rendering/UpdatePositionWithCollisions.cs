using Silk.NET.Input;
using Silk.NET.Maths;
using VulkanMC.Core;

namespace VulkanMC;

public partial class VulkanEngine
{
    private void UpdatePositionWithCollisions(float dt)
    {
        if (_input == null || _input.Keyboards.Count == 0) return;
        var k = _input.Keyboards[0];
        float s = 10f * dt;
        var nextPos = _cameraPos;
        var forward = new Vector3D<float>(_cameraFront.X, 0, _cameraFront.Z);
        if (forward.Length != 0) forward = Vector3D.Normalize(forward);
        var right = Vector3D.Normalize(Vector3D.Cross(forward, Vector3D<float>.UnitY));

        if (k.IsKeyPressed(Key.W)) nextPos += forward * s;
        if (k.IsKeyPressed(Key.S)) nextPos -= forward * s;
        if (k.IsKeyPressed(Key.A)) nextPos -= right * s;
        if (k.IsKeyPressed(Key.D)) nextPos += right * s;
        if (k.IsKeyPressed(Key.Space)) nextPos.Y += s;
        if (k.IsKeyPressed(Key.ShiftLeft)) nextPos.Y -= s;

        if (Config.Data.Physics.GravityEnabled)
        {
            _verticalVelocity -= 20f * dt;
            nextPos.Y += _verticalVelocity * dt;

            int blockX = (int)MathF.Floor(nextPos.X);
            int blockY = (int)MathF.Floor(nextPos.Y - 1.8f);
            int blockZ = (int)MathF.Floor(nextPos.Z);

            bool grounded = false;
            const float epsilon = 0.005f;
            float targetY = (float)blockY + 2.8f;
            bool blockDetected = _world != null && (
                _world.IsBlockAt(blockX, blockY, blockZ) ||
                _world.IsBlockAt((int)MathF.Floor(nextPos.X + 0.3f), blockY, (int)MathF.Floor(nextPos.Z)) ||
                _world.IsBlockAt((int)MathF.Floor(nextPos.X - 0.3f), blockY, (int)MathF.Floor(nextPos.Z)) ||
                _world.IsBlockAt((int)MathF.Floor(nextPos.X), blockY, (int)MathF.Floor(nextPos.Z + 0.3f)) ||
                _world.IsBlockAt((int)MathF.Floor(nextPos.X), blockY, (int)MathF.Floor(nextPos.Z - 0.3f)));
            if (blockDetected && nextPos.Y < targetY - epsilon)
            {
                nextPos.Y = targetY;
                _verticalVelocity = 0;
                grounded = true;
            }

            if (nextPos.Y < 2.0f)
            {
                nextPos.Y = 2.0f;
                _verticalVelocity = 0;
                grounded = true;
            }

            if (grounded && k.IsKeyPressed(Key.Space))
            {
                _verticalVelocity = 8.0f;
            }

            if (Config.Data.Physics.AutoJump && grounded && (k.IsKeyPressed(Key.W) || k.IsKeyPressed(Key.S) || k.IsKeyPressed(Key.A) || k.IsKeyPressed(Key.D)))
            {
                Vector3D<float> moveDir = nextPos - _cameraPos;
                moveDir.Y = 0;
                if (moveDir.Length > 0.001f)
                {
                    moveDir = Vector3D.Normalize(moveDir);
                    Vector3D<float> checkPos = nextPos + moveDir * 0.5f;
                    int cx = (int)MathF.Floor(checkPos.X);
                    int cy = (int)MathF.Floor(checkPos.Y - 0.8f);
                    int cz = (int)MathF.Floor(checkPos.Z);

                    if (_world != null && _world.IsBlockAt(cx, cy, cz))
                    {
                        if (!_world.IsBlockAt(cx, cy + 1, cz))
                        {
                            _verticalVelocity = 5.5f;
                        }
                    }
                }
            }

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
}

using System.Numerics;
using Silk.NET.Maths;
using VulkanMC.Config;

namespace VulkanMC.Engine.Entities;

public class Mob : Entity
{
    private float _moveTimer;
    private Vector3D<float> _targetDirection;

    public Mob(Vector3D<float> position)
    {
        Position = position;
        _targetDirection = new Vector3D<float>((float)Random.Shared.NextDouble() * 2 - 1, 0, (float)Random.Shared.NextDouble() * 2 - 1);
    }

    public override void Update(float dt)
    {
        _moveTimer += dt;
        if (_moveTimer > 2.0f)
        {
            _moveTimer = 0;
            _targetDirection = new Vector3D<float>((float)Random.Shared.NextDouble() * 2 - 1, 0, (float)Random.Shared.NextDouble() * 2 - 1);
            if (_targetDirection.Length > 0) _targetDirection = Vector3D.Normalize(_targetDirection);
        }

        Velocity = new Vector3D<float>(_targetDirection.X * 2.0f, Velocity.Y, _targetDirection.Z * 2.0f);
        
        // Gravity
        Velocity = new Vector3D<float>(Velocity.X, Velocity.Y - 9.81f * dt, Velocity.Z);
        
        Position += Velocity * dt;
        
        // Very basic terrain sticking for now if no physics engine is integrated for mobs
        // (Real implementation would use Physics system like the player)
    }
}

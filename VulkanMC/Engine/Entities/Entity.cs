using System.Numerics;
using Silk.NET.Maths;

namespace VulkanMC.Engine.Entities;

public abstract class Entity
{
    public Vector3D<float> Position { get; set; }
    public Vector3D<float> Velocity { get; set; }
    public float Yaw { get; set; }
    public float Pitch { get; set; }

    public abstract void Update(float dt);
}

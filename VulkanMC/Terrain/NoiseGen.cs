namespace VulkanMC.Terrain;

public sealed class NoiseGen
{
    private readonly SimplexNoise _noise;
    private readonly int _octaves;
    private readonly float _gain;
    private readonly float _lacunarity;

    public NoiseGen(int seed)
    {
        _noise = new SimplexNoise(seed);
        _octaves = 6;
        _gain = 0.5f;
        _lacunarity = 2.0f;
    }

    public float Get(float x, float z)
    {
        return _noise.FractalNoise(x, 0.0f, z, _octaves, _gain, _lacunarity);
    }
}

namespace VulkanMC.Terrain;

public class SimplexNoise
{
    private readonly short[] _perm;
    private readonly short[] _permMod12;

    private static readonly short[] _grad3 = {
        1,1,0, -1,1,0, 1,-1,0, -1,-1,0,
        1,0,1, -1,0,1, 1,0,-1, -1,0,-1,
        0,1,1, 0,-1,1, 0,1,-1, 0,-1,-1
    };

    public SimplexNoise(int seed = 0)
    {
        var random = new Random(seed);
        var p = new byte[256];
        for (int i = 0; i < 256; i++) p[i] = (byte)i;
        
        // Shuffle
        for (int i = 255; i > 0; i--)
        {
            int j = random.Next(i + 1);
            (p[i], p[j]) = (p[j], p[i]);
        }

        _perm = new short[512];
        _permMod12 = new short[512];
        for (int i = 0; i < 512; i++)
        {
            _perm[i] = p[i & 255];
            _permMod12[i] = (short)(_perm[i] % 12);
        }
    }

    // Skewing and unskewing factors for 2D and 3D
    private const float F2 = 0.5f * (1.73205080757f - 1.0f);
    private const float G2 = (3.0f - 1.73205080757f) / 6.0f;
    private const float F3 = 1.0f / 3.0f;
    private const float G3 = 1.0f / 6.0f;

    public float Noise(float xin, float yin, float zin)
    {
        float n0, n1, n2, n3; // Noise contributions from the four corners

        // Skew the input space to determine which simplex cell we're in
        float s = (xin + yin + zin) * F3;
        int i = (int)Math.Floor(xin + s);
        int j = (int)Math.Floor(yin + s);
        int k = (int)Math.Floor(zin + s);

        float t = (i + j + k) * G3;
        float X0 = i - t; // Unskew the cell origin back to (x,y,z) space
        float Y0 = j - t;
        float Z0 = k - t;
        float x0 = xin - X0; // The x,y,z distances from the cell origin
        float y0 = yin - Y0;
        float z0 = zin - Z0;

        // For the 3D case, the simplex shape is a slightly skewed tetrahedron.
        // Determine which simplex we are in.
        int i1, j1, k1; // Offsets for second corner of simplex in (i,j,k) coords
        int i2, j2, k2; // Offsets for third corner of simplex in (i,j,k) coords

        if (x0 >= y0)
        {
            if (y0 >= z0) { i1 = 1; j1 = 0; k1 = 0; i2 = 1; j2 = 1; k2 = 0; } // X Y Z order
            else if (x0 >= z0) { i1 = 1; j1 = 0; k1 = 0; i2 = 1; j2 = 0; k2 = 1; } // X Z Y order
            else { i1 = 0; j1 = 0; k1 = 1; i2 = 1; j2 = 0; k2 = 1; } // Z X Y order
        }
        else
        { // x0 < y0
            if (y0 < z0) { i1 = 0; j1 = 0; k1 = 1; i2 = 0; j2 = 1; k2 = 1; } // Z Y X order
            else if (x0 < z0) { i1 = 0; j1 = 1; k1 = 0; i2 = 0; j2 = 1; k2 = 1; } // Y Z X order
            else { i1 = 0; j1 = 1; k1 = 0; i2 = 1; j2 = 1; k2 = 0; } // Y X Z order
        }

        // A step of (1,0,0) in (i,j,k) means a step of (1-c, -c, -c) in (x,y,z),
        // a step of (0,1,0) in (i,j,k) means a step of (-c, 1-c, -c) in (x,y,z), and
        // a step of (0,0,1) in (i,j,k) means a step of (-c, -c, 1-c) in (x,y,z), where c = 1/6.
        float x1 = x0 - i1 + G3; // Offsets for second corner in (x,y,z) coords
        float y1 = y0 - j1 + G3;
        float z1 = z0 - k1 + G3;
        float x2 = x0 - i2 + 2.0f * G3; // Offsets for third corner in (x,y,z) coords
        float y2 = y0 - j2 + 2.0f * G3;
        float z2 = z0 - k2 + 2.0f * G3;
        float x3 = x0 - 1.0f + 3.0f * G3; // Offsets for last corner in (x,y,z) coords
        float y3 = y0 - 1.0f + 3.0f * G3;
        float z3 = z0 - 1.0f + 3.0f * G3;

        // Work out the hashed gradient indices of the four simplex corners
        int ii = i & 255;
        int jj = j & 255;
        int kk = k & 255;

        // Calculate the contribution from the four corners
        float t0 = 0.6f - x0 * x0 - y0 * y0 - z0 * z0;
        if (t0 < 0) n0 = 0.0f;
        else
        {
            t0 *= t0;
            n0 = t0 * t0 * Dot(_grad3, _permMod12[ii + _perm[jj + _perm[kk]]], x0, y0, z0);
        }

        float t1 = 0.6f - x1 * x1 - y1 * y1 - z1 * z1;
        if (t1 < 0) n1 = 0.0f;
        else
        {
            t1 *= t1;
            n1 = t1 * t1 * Dot(_grad3, _permMod12[ii + i1 + _perm[jj + j1 + _perm[kk + k1]]], x1, y1, z1);
        }

        float t2 = 0.6f - x2 * x2 - y2 * y2 - z2 * z2;
        if (t2 < 0) n2 = 0.0f;
        else
        {
            t2 *= t2;
            n2 = t2 * t2 * Dot(_grad3, _permMod12[ii + i2 + _perm[jj + j2 + _perm[kk + k2]]], x2, y2, z2);
        }

        float t3 = 0.6f - x3 * x3 - y3 * y3 - z3 * z3;
        if (t3 < 0) n3 = 0.0f;
        else
        {
            t3 *= t3;
            n3 = t3 * t3 * Dot(_grad3, _permMod12[ii + 1 + _perm[jj + 1 + _perm[kk + 1]]], x3, y3, z3);
        }

        // Add contributions from each corner to get the final noise value.
        // The result is scaled to stay just inside [-1,1]
        return 32.0f * (n0 + n1 + n2 + n3);
    }

    public float FractalNoise(float x, float y, float z, int octaves, float persistence, float lacunarity)
    {
        float total = 0;
        float frequency = 1;
        float amplitude = 1;
        float maxValue = 0;
        for (int i = 0; i < octaves; i++)
        {
            total += Noise(x * frequency, y * frequency, z * frequency) * amplitude;
            maxValue += amplitude;
            amplitude *= persistence;
            frequency *= lacunarity;
        }
        return total / maxValue;
    }

    private static float Dot(short[] g, int gradIdx, float x, float y, float z)
    {
        return g[gradIdx * 3] * x + g[gradIdx * 3 + 1] * y + g[gradIdx * 3 + 2] * z;
    }
}

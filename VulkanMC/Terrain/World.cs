using System.Collections.Generic;
using Silk.NET.Maths;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using AppConfig = VulkanMC.Config.Config;
using VulkanMC.Graphics;
using System;

namespace VulkanMC.Terrain;

public class World
{
    private readonly SimplexNoise _noise;
    // Stockage des blocs pour les collisions (x, y, z)
    private readonly HashSet<(int, int, int)> _blocks = new();
    private readonly Dictionary<(int, int, int), BlockType> _specialBlocks = new(); // Stores non-procedural blocks like trees
    private readonly Dictionary<(int, int), List<(int, int, int)>> _chunkBlockIndices = new();

    public World(int seed = 42)
    {
        _noise = new SimplexNoise(seed);
    }

    public float GetHeightAt(int x, int z)
    {
        float globalX = x;
        float globalZ = z;

        float baseNoise = _noise.FractalNoise(globalX * 0.01f, 0, globalZ * 0.01f, 3, 0.5f, 2.0f);
        float detailNoise = _noise.FractalNoise(globalX * 0.05f, 0, globalZ * 0.05f, 2, 0.4f, 2.0f);

        float noiseValue = (baseNoise + 1.0f) * 0.5f;
        float detailValue = (detailNoise + 1.0f) * 0.5f;

        int h = (int)(noiseValue * 25.0f + detailValue * 5.0f) + 5;
        if (h > 60) h = 60;
        return h;
    }

    private int[,] GenerateHeightmap(int offsetX, int offsetZ, int width, int depth)
    {
        var heights = new int[width, depth];

        for (int x = 0; x < width; x++)
        {
            for (int z = 0; z < depth; z++)
            {
                heights[x, z] = (int)GetHeightAt(offsetX + x, offsetZ + z);
            }
        }

        return heights;
    }

    public (Vertex[] vertices, uint[] indices) GenerateChunk(int chunkX, int chunkZ, int width, int depth, int lod = 0)
    {
        var vertices = new List<Vertex>();
        var indices = new List<uint>();

        int offsetX = chunkX * width;
        int offsetZ = chunkZ * depth;

        var blockList = new List<(int, int, int)>();

        // Step size based on LOD
        int step = (int)MathF.Pow(2, lod);
        
        // 1. Générer ou charger la carte des hauteurs
        int[,] heights;
        if (!TryLoadChunkHeights(chunkX, chunkZ, width, depth, out heights))
        {
            heights = GenerateHeightmap(offsetX, offsetZ, width, depth);
            // Sauvegarde en arrière-plan pour réutilisation
            try { SaveChunkHeightsAsync(chunkX, chunkZ, heights); } catch { }
        }

        for (int x = 0; x < width; x++)
        {
            for (int z = 0; z < depth; z++)
            {
                // Only add to collision list if LOD 0
                if (lod == 0)
                {
                    for (int y = 0; y < heights[x, z]; y++)
                    {
                        var blockPos = (offsetX + x, y, offsetZ + z);
                        _blocks.Add(blockPos);
                        blockList.Add(blockPos);
                    }
                }
            }
        }
        if (lod == 0) _chunkBlockIndices[(chunkX, chunkZ)] = blockList;

        if (AppConfig.Data.Rendering.EnableTrees && lod == 0)
        {
            Random rand = new Random(chunkX * 1000 + chunkZ + AppConfig.Data.Rendering.WorldSeed);
            for (int x = 2; x < width - 2; x++)
            {
                for (int z = 2; z < depth - 2; z++)
                {
                    if (rand.NextDouble() < 0.02 && heights[x, z] < 40 && heights[x, z] > 5)
                    {
                        GenerateTree(offsetX + x, heights[x, z], offsetZ + z, blockList);
                    }
                }
            }
        }

        // 2. Générer les faces visibles
        for (int x = 0; x < width; x += step)
        {
            for (int z = 0; z < depth; z += step)
            {
                int h;
                // Compute height taking LOD into account to avoid seams: use max height over the LOD cell
                if (lod == 0)
                {
                    h = heights[x, z];
                }
                else
                {
                    int maxh = 0;
                    for (int sx = 0; sx < step; sx++)
                    {
                        for (int sz = 0; sz < step; sz++)
                        {
                            maxh = Math.Max(maxh, (int)GetHeightAt(offsetX + x + sx, offsetZ + z + sz));
                        }
                    }
                    h = maxh;
                }

                // For distant chunks, we might only render the top layer or fewer layers
                int startY = (lod > 0) ? h - 1 : 0;
                
                for (int y = startY; y < h + 15; y++) // Extend loop for trees
                {
                    Vector3D<float> blockPos = new Vector3D<float>(offsetX + x, y, offsetZ + z);
                    
                    BlockType blockType;
                    bool isSpecial = _specialBlocks.TryGetValue((offsetX + x, y, offsetZ + z), out var specialType);

                    if (y < h)
                    {
                        if (y == h - 1)
                        {
                            if (h > 45) blockType = BlockType.Snow;
                            else blockType = BlockType.Grass;
                        }
                        else if (y > h - 4) blockType = BlockType.Dirt;
                        else blockType = BlockType.Stone;
                    }
                    else if (isSpecial)
                    {
                        blockType = specialType;
                    }
                    else continue;

                    Vector3D<float> color = new Vector3D<float>(1.0f, 1.0f, 1.0f);
                    
                    // Culling logic: Don't render if surrounded by opaque blocks
                    bool fBack, fFront, fLeft, fRight, fBottom, fTop;
                    
                    if (lod == 0)
                    {
                        fBack = !IsOpaque(offsetX + x, y, offsetZ + z - 1);
                        fFront = !IsOpaque(offsetX + x, y, offsetZ + z + 1);
                        fLeft = !IsOpaque(offsetX + x - 1, y, offsetZ + z);
                        fRight = !IsOpaque(offsetX + x + 1, y, offsetZ + z);
                        fBottom = !IsOpaque(offsetX + x, y - 1, offsetZ + z);
                        fTop = !IsOpaque(offsetX + x, y + 1, offsetZ + z);
                    }
                    else
                    {
                        // Simplified culling for LOD: always show sides if at chunk edge or if top
                        fBack = (z == 0);
                        fFront = (z == depth - step);
                        fLeft = (x == 0);
                        fRight = (x == width - step);
                        fBottom = false;
                        fTop = (y >= h - 1);
                    }

                    if (fBack || fFront || fLeft || fRight || fBottom || fTop)
                    {
                        AddVisibleFacesLOD(vertices, indices, blockPos, color, fBack, fFront, fLeft, fRight, fBottom, fTop, blockType, step);
                    }
                }
            }
        }

        return (vertices.ToArray(), indices.ToArray());
    }

    public void UnloadChunk(int chunkX, int chunkZ)
    {
        if (_chunkBlockIndices.Remove((chunkX, chunkZ), out var blockList))
        {
            foreach (var pos in blockList)
            {
                _blocks.Remove(pos);
                _specialBlocks.Remove(pos);
            }
        }
    }

    private void GenerateTree(int x, int y, int z, List<(int, int, int)> blockList)
    {
        int height = 5;
        // Trunk
        for (int i = 0; i < height; i++)
        {
            var pos = (x, y + i, z);
            if (!_blocks.Contains(pos))
            {
                _blocks.Add(pos);
                _specialBlocks[pos] = BlockType.Wood;
                blockList.Add(pos);
            }
        }
        // Leaves
        for (int lx = -2; lx <= 2; lx++)
        {
            for (int lz = -2; lz <= 2; lz++)
            {
                for (int ly = 0; ly <= 2; ly++)
                {
                    if (Math.Abs(lx) == 2 && Math.Abs(lz) == 2) continue;
                    var pos = (x + lx, y + height + ly - 1, z + lz);
                    if (!_blocks.Contains(pos) || (_specialBlocks.TryGetValue(pos, out var current) && current == BlockType.Leaves))
                    {
                        _blocks.Add(pos);
                        _specialBlocks[pos] = BlockType.Leaves;
                        blockList.Add(pos);
                    }
                }
            }
        }
    }

    private bool IsOpaque(int x, int y, int z)
    {
        if (_specialBlocks.TryGetValue((x, y, z), out var type))
        {
            return type != BlockType.Leaves;
        }
        return y < (int)GetHeightAt(x, z);
    }

    private static string ChunkCacheDir()
    {
        try
        {
            var baseDir = AppContext.BaseDirectory ?? Directory.GetCurrentDirectory();
            var dir = Path.Combine(baseDir, "chunk_cache");
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            return dir;
        }
        catch
        {
            var dir = Path.Combine(Directory.GetCurrentDirectory(), "chunk_cache");
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            return dir;
        }
    }

    private static string ChunkFilePath(int chunkX, int chunkZ) => Path.Combine(ChunkCacheDir(), $"chunk_{chunkX}_{chunkZ}.bin");

    private bool TryLoadChunkHeights(int chunkX, int chunkZ, int w, int d, out int[,] heights)
    {
        heights = new int[w, d];
        var path = ChunkFilePath(chunkX, chunkZ);
        if (!File.Exists(path)) return false;
        try
        {
            var bytes = File.ReadAllBytes(path);
            int expected = w * d * sizeof(int);
            if (bytes.Length != expected) return false;
            int[] flat = new int[w * d];
            System.Buffer.BlockCopy(bytes, 0, flat, 0, expected);
            for (int x = 0; x < w; x++) for (int z = 0; z < d; z++) heights[x, z] = flat[x * d + z];
            return true;
        }
        catch
        {
            return false;
        }
    }

    private void SaveChunkHeightsAsync(int chunkX, int chunkZ, int[,] heights)
    {
        var path = ChunkFilePath(chunkX, chunkZ);
        // Fire-and-forget write
        Task.Run(() =>
        {
            try
            {
                int w = heights.GetLength(0);
                int d = heights.GetLength(1);
                int[] flat = new int[w * d];
                for (int x = 0; x < w; x++) for (int z = 0; z < d; z++) flat[x * d + z] = heights[x, z];
                var bytes = new byte[flat.Length * sizeof(int)];
                System.Buffer.BlockCopy(flat, 0, bytes, 0, bytes.Length);
                File.WriteAllBytes(path, bytes);
            }
            catch { }
        });
    }

    public bool IsChunkLoaded(int chunkX, int chunkZ) => _chunkBlockIndices.ContainsKey((chunkX, chunkZ));

    public void UpdateChunks(Vector3D<float> cameraPos, int renderDistance)
    {
        // Chunk management logic to be implemented here
    }

    public bool IsBlockAt(int x, int y, int z)
    {
        return _blocks.Contains((x, y, z));
    }

    // Try to determine the BlockType at a given coordinate. Returns null if no block exists there.
    public BlockType? GetBlockTypeAt(int x, int y, int z)
    {
        if (_specialBlocks.TryGetValue((x, y, z), out var specialType)) return specialType;
        if (!IsBlockAt(x, y, z)) return null;
        int h = (int)GetHeightAt(x, z);
        if (y == h - 1)
        {
            if (h > 45) return BlockType.Snow;
            return BlockType.Grass;
        }
        else if (y > h - 4) return BlockType.Dirt;
        else return BlockType.Stone;
    }

    private (float uMin, float vMin, float uMax, float vMax) GetUVs(BlockType type, bool isSide = false)
    {
        int index = type switch
        {
            BlockType.Grass => isSide ? 3 : 0,
            BlockType.Stone => 1,
            BlockType.Snow => 2,
            BlockType.Dirt => 3,
            BlockType.Wood => 2,
            BlockType.Leaves => 0,
            _ => 1
        };

        float u = (index % 2) * 0.5f;
        float v = (index / 2) * 0.5f;
        return (u + 0.01f, v + 0.01f, u + 0.49f, v + 0.49f);
    }

    private void AddVisibleFacesLOD(List<Vertex> vertices, List<uint> indices, Vector3D<float> pos, Vector3D<float> color, 
        bool back, bool front, bool left, bool right, bool bottom, bool top, BlockType type, int step)
    {
        float s = (float)step;
        var (uMin, vMin, uMax, vMax) = GetUVs(type, false);
        var (suMin, svMin, suMax, svMax) = GetUVs(type, true);

        Vector3D<float> topColor = (type == BlockType.Grass)  ? new Vector3D<float>(0.4f, 0.8f, 0.3f) : 
                                   (type == BlockType.Leaves) ? new Vector3D<float>(0.3f, 0.7f, 0.3f) :
                                   (type == BlockType.Snow)   ? new Vector3D<float>(1.0f, 1.0f, 1.0f) : color;
        
        Vector3D<float> sideColor = (type == BlockType.Leaves) ? new Vector3D<float>(0.3f, 0.7f, 0.3f) : color;

        // Face Back (z-)
        if (back)
        {
            uint b = (uint)vertices.Count;
            vertices.Add(new Vertex(pos + new Vector3D<float>(0, 0, 0), sideColor, new Vector2D<float>(suMax, svMax)));
            vertices.Add(new Vertex(pos + new Vector3D<float>(0, s, 0), sideColor, new Vector2D<float>(suMax, svMin)));
            vertices.Add(new Vertex(pos + new Vector3D<float>(s, s, 0), sideColor, new Vector2D<float>(suMin, svMin)));
            vertices.Add(new Vertex(pos + new Vector3D<float>(s, 0, 0), sideColor, new Vector2D<float>(suMin, svMax)));
            indices.AddRange(new uint[] { b, b + 1, b + 2, b, b + 2, b + 3 });
        }

        // Face Front (z+)
        if (front)
        {
            uint b = (uint)vertices.Count;
            vertices.Add(new Vertex(pos + new Vector3D<float>(0, 0, s), sideColor, new Vector2D<float>(suMin, svMax)));
            vertices.Add(new Vertex(pos + new Vector3D<float>(s, 0, s), sideColor, new Vector2D<float>(suMax, svMax)));
            vertices.Add(new Vertex(pos + new Vector3D<float>(s, s, s), sideColor, new Vector2D<float>(suMax, svMin)));
            vertices.Add(new Vertex(pos + new Vector3D<float>(0, s, s), sideColor, new Vector2D<float>(suMin, svMin)));
            indices.AddRange(new uint[] { b, b + 1, b + 2, b, b + 2, b + 3 });
        }

        // Face Left (x-)
        if (left)
        {
            uint b = (uint)vertices.Count;
            vertices.Add(new Vertex(pos + new Vector3D<float>(0, 0, 0), sideColor, new Vector2D<float>(suMin, svMax)));
            vertices.Add(new Vertex(pos + new Vector3D<float>(0, 0, s), sideColor, new Vector2D<float>(suMax, svMax)));
            vertices.Add(new Vertex(pos + new Vector3D<float>(0, s, s), sideColor, new Vector2D<float>(suMax, svMin)));
            vertices.Add(new Vertex(pos + new Vector3D<float>(0, s, 0), sideColor, new Vector2D<float>(suMin, svMin)));
            indices.AddRange(new uint[] { b, b + 1, b + 2, b, b + 2, b + 3 });
        }

        // Face Right (x+)
        if (right)
        {
            uint b = (uint)vertices.Count;
            vertices.Add(new Vertex(pos + new Vector3D<float>(s, 0, 0), sideColor, new Vector2D<float>(suMax, svMax)));
            vertices.Add(new Vertex(pos + new Vector3D<float>(s, s, 0), sideColor, new Vector2D<float>(suMax, svMin)));
            vertices.Add(new Vertex(pos + new Vector3D<float>(s, s, s), sideColor, new Vector2D<float>(suMin, svMin)));
            vertices.Add(new Vertex(pos + new Vector3D<float>(s, 0, s), sideColor, new Vector2D<float>(suMin, svMax)));
            indices.AddRange(new uint[] { b, b + 1, b + 2, b, b + 2, b + 3 });
        }

        // Face Bottom
        if (bottom)
        {
            uint b = (uint)vertices.Count;
            vertices.Add(new Vertex(pos + new Vector3D<float>(0, 0, 0), color, new Vector2D<float>(uMin, vMin)));
            vertices.Add(new Vertex(pos + new Vector3D<float>(s, 0, 0), color, new Vector2D<float>(uMax, vMin)));
            vertices.Add(new Vertex(pos + new Vector3D<float>(s, 0, s), color, new Vector2D<float>(uMax, vMax)));
            vertices.Add(new Vertex(pos + new Vector3D<float>(0, 0, s), color, new Vector2D<float>(uMin, vMax)));
            indices.AddRange(new uint[] { b, b + 1, b + 2, b, b + 2, b + 3 });
        }

        // Face Top
        if (top)
        {
            uint b = (uint)vertices.Count;
            vertices.Add(new Vertex(pos + new Vector3D<float>(0, s, 0), topColor, new Vector2D<float>(uMin, vMin)));
            vertices.Add(new Vertex(pos + new Vector3D<float>(0, s, s), topColor, new Vector2D<float>(uMin, vMax)));
            vertices.Add(new Vertex(pos + new Vector3D<float>(s, s, s), topColor, new Vector2D<float>(uMax, vMax)));
            vertices.Add(new Vertex(pos + new Vector3D<float>(s, s, 0), topColor, new Vector2D<float>(uMax, vMin)));
            indices.AddRange(new uint[] { b, b + 1, b + 2, b, b + 2, b + 3 });
        }
    }
}

public enum BlockType
{
    Grass,
    Dirt,
    Stone,
    Snow,
    Wood,
    Leaves
}

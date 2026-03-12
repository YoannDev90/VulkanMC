using System.Collections.Generic;
using Silk.NET.Maths;

namespace VulkanMC;

public class World
{
    private readonly SimplexNoise _noise;
    // Stockage des blocs pour les collisions (x, y, z)
    private readonly HashSet<(int, int, int)> _blocks = new();
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
        return (float)h;
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
        
        // 1. Générer la carte des hauteurs avec plus de variété
        int[,] heights = new int[width, depth];
        for (int x = 0; x < width; x++)
        {
            for (int z = 0; z < depth; z++)
            {
                heights[x, z] = (int)GetHeightAt(offsetX + x, offsetZ + z);

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

        // 2. Générer les faces visibles
        for (int x = 0; x < width; x += step)
        {
            for (int z = 0; z < depth; z += step)
            {
                int h = heights[x, z];
                
                // For distant chunks, we might only render the top layer or fewer layers
                int startY = (lod > 0) ? h - 1 : 0; 
                
                for (int y = startY; y < h; y++)
                {
                    Vector3D<float> blockPos = new Vector3D<float>(offsetX + x, y, offsetZ + z);
                    Vector3D<float> blockSize = new Vector3D<float>(step, (lod > 0 && y == h - 1) ? 1 : 1, step); // Larger blocks for high LOD
                    
                    Vector3D<float> color = new Vector3D<float>(1.0f, 1.0f, 1.0f);
                    BlockType blockType;
                    
                    if (y == h - 1)
                    {
                        if (h > 45) { // Neige pour les montagnes hautes
                            blockType = BlockType.Snow;
                        } else {
                            blockType = BlockType.Grass;
                        }
                    }
                    else if (y > h - 4)
                    {
                        blockType = BlockType.Dirt;
                    }
                    else
                    {
                        blockType = BlockType.Stone;
                    }

                    // Culling logic: Don't render if surrounded by 6 opaque blocks
                    bool fBack, fFront, fLeft, fRight, fBottom, fTop;
                    
                    if (lod == 0)
                    {
                        // Hidden from view if it has neighbors at same or higher height
                        fBack   = (z == 0          || y >= (x < width && z > 0 ? heights[x, z - 1] : 0));
                        fFront  = (z == depth - 1  || y >= (x < width && z < depth - 1 ? heights[x, z + 1] : 0));
                        fLeft   = (x == 0          || y >= (x > 0 && z < depth ? heights[x - 1, z] : 0));
                        fRight  = (x == width - 1  || y >= (x < width - 1 && z < depth ? heights[x + 1, z] : 0));
                        fBottom = (y == 0);
                        fTop    = (y == h - 1);
                        
                        // Internal occlusion: if block is below the surface of ALL 4 neighbors, it's hidden from ground-level
                        // But wait, we only render faces that are exposed.
                    }
                    else
                    {
                        // Simplified culling for LOD: always show sides if at chunk edge or if top
                        fBack = (z == 0);
                        fFront = (z == depth - step);
                        fLeft = (x == 0);
                        fRight = (x == width - step);
                        fBottom = false;
                        fTop = true;
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
            }
        }
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

    private (float uMin, float vMin, float uMax, float vMax) GetUVs(BlockType type, bool isSide = false)
    {
        int index = type switch
        {
            BlockType.Grass => isSide ? 3 : 0,
            BlockType.Stone => 1,
            BlockType.Snow => 2,
            BlockType.Dirt => 3, // Fallback dirt to side texture for now or stone
            _ => 1
        };

        float u = (index % 2) * 0.5f;
        float v = (index / 2) * 0.5f;
        return (u + 0.01f, v + 0.01f, u + 0.49f, v + 0.49f); // Petits offsets pour éviter le bleeding
    }

    private void AddVisibleFaces(List<Vertex> vertices, List<uint> indices, Vector3D<float> pos, Vector3D<float> color, 
        bool back, bool front, bool left, bool right, bool bottom, bool top, BlockType type)
    {
        var (uMin, vMin, uMax, vMax) = GetUVs(type, false);
        var (suMin, svMin, suMax, svMax) = GetUVs(type, true);

        // Teinte verte pour le dessus de l'herbe
        Vector3D<float> topColor = (type == BlockType.Grass) ? new Vector3D<float>(0.4f, 0.8f, 0.3f) : 
                                   (type == BlockType.Snow)  ? new Vector3D<float>(1.0f, 1.0f, 1.0f) :
                                   (type == BlockType.Dirt)  ? new Vector3D<float>(1.0f, 1.0f, 1.0f) : 
                                   (type == BlockType.Stone) ? new Vector3D<float>(1.0f, 1.0f, 1.0f) : color;
        
        // Teinte pour les côtés (on n'applique pas de teinte sur la texture grass_block_side par défaut, ou une légère)
        Vector3D<float> sideColor = (type == BlockType.Grass) ? new Vector3D<float>(1.0f, 1.0f, 1.0f) : 
                                    (type == BlockType.Snow)  ? new Vector3D<float>(1.0f, 1.0f, 1.0f) :
                                    (type == BlockType.Dirt)  ? new Vector3D<float>(1.0f, 1.0f, 1.0f) : 
                                    (type == BlockType.Stone) ? new Vector3D<float>(1.0f, 1.0f, 1.0f) : color;

        // Face Back (z-)
        if (back)
        {
            uint b = (uint)vertices.Count;
            vertices.Add(new Vertex(pos + new Vector3D<float>(0, 0, 0), sideColor, new Vector2D<float>(suMax, svMax)));
            vertices.Add(new Vertex(pos + new Vector3D<float>(0, 1, 0), sideColor, new Vector2D<float>(suMax, svMin)));
            vertices.Add(new Vertex(pos + new Vector3D<float>(1, 1, 0), sideColor, new Vector2D<float>(suMin, svMin)));
            vertices.Add(new Vertex(pos + new Vector3D<float>(1, 0, 0), sideColor, new Vector2D<float>(suMin, svMax)));
            indices.AddRange(new uint[] { b, b + 1, b + 2, b, b + 2, b + 3 });
        }

        // Face Front (z+)
        if (front)
        {
            uint b = (uint)vertices.Count;
            vertices.Add(new Vertex(pos + new Vector3D<float>(0, 0, 1), sideColor, new Vector2D<float>(suMin, svMax)));
            vertices.Add(new Vertex(pos + new Vector3D<float>(1, 0, 1), sideColor, new Vector2D<float>(suMax, svMax)));
            vertices.Add(new Vertex(pos + new Vector3D<float>(1, 1, 1), sideColor, new Vector2D<float>(suMax, svMin)));
            vertices.Add(new Vertex(pos + new Vector3D<float>(0, 1, 1), sideColor, new Vector2D<float>(suMin, svMin)));
            indices.AddRange(new uint[] { b, b + 1, b + 2, b, b + 2, b + 3 });
        }

        // Face Left (x-)
        if (left)
        {
            uint b = (uint)vertices.Count;
            vertices.Add(new Vertex(pos + new Vector3D<float>(0, 0, 0), sideColor, new Vector2D<float>(suMin, svMax)));
            vertices.Add(new Vertex(pos + new Vector3D<float>(0, 0, 1), sideColor, new Vector2D<float>(suMax, svMax)));
            vertices.Add(new Vertex(pos + new Vector3D<float>(0, 1, 1), sideColor, new Vector2D<float>(suMax, svMin)));
            vertices.Add(new Vertex(pos + new Vector3D<float>(0, 1, 0), sideColor, new Vector2D<float>(suMin, svMin)));
            indices.AddRange(new uint[] { b, b + 1, b + 2, b, b + 2, b + 3 });
        }

        // Face Right (x+)
        if (right)
        {
            uint b = (uint)vertices.Count;
            vertices.Add(new Vertex(pos + new Vector3D<float>(1, 0, 0), sideColor, new Vector2D<float>(suMax, svMax)));
            vertices.Add(new Vertex(pos + new Vector3D<float>(1, 1, 0), sideColor, new Vector2D<float>(suMax, svMin)));
            vertices.Add(new Vertex(pos + new Vector3D<float>(1, 1, 1), sideColor, new Vector2D<float>(suMin, svMin)));
            vertices.Add(new Vertex(pos + new Vector3D<float>(1, 0, 1), sideColor, new Vector2D<float>(suMin, svMax)));
            indices.AddRange(new uint[] { b, b + 1, b + 2, b, b + 2, b + 3 });
        }

        // Face Bottom (y-)
        if (bottom)
        {
            uint b = (uint)vertices.Count;
            vertices.Add(new Vertex(pos + new Vector3D<float>(0, 0, 0), color, new Vector2D<float>(uMin, vMin)));
            vertices.Add(new Vertex(pos + new Vector3D<float>(1, 0, 0), color, new Vector2D<float>(uMax, vMin)));
            vertices.Add(new Vertex(pos + new Vector3D<float>(1, 0, 1), color, new Vector2D<float>(uMax, vMax)));
            vertices.Add(new Vertex(pos + new Vector3D<float>(0, 0, 1), color, new Vector2D<float>(uMin, vMax)));
            indices.AddRange(new uint[] { b, b + 1, b + 2, b, b + 2, b + 3 });
        }

        // Face Top (y+)
        if (top)
        {
            uint b = (uint)vertices.Count;
            vertices.Add(new Vertex(pos + new Vector3D<float>(0, 1, 0), topColor, new Vector2D<float>(uMin, vMin)));
            vertices.Add(new Vertex(pos + new Vector3D<float>(0, 1, 1), topColor, new Vector2D<float>(uMin, vMax)));
            vertices.Add(new Vertex(pos + new Vector3D<float>(1, 1, 1), topColor, new Vector2D<float>(uMax, vMax)));
            vertices.Add(new Vertex(pos + new Vector3D<float>(1, 1, 0), topColor, new Vector2D<float>(uMax, vMin)));
            indices.AddRange(new uint[] { b, b + 1, b + 2, b, b + 2, b + 3 });
        }
    }

    private void AddVisibleFacesLOD(List<Vertex> vertices, List<uint> indices, Vector3D<float> pos, Vector3D<float> color, 
        bool back, bool front, bool left, bool right, bool bottom, bool top, BlockType type, int step)
    {
        float s = (float)step;
        var (uMin, vMin, uMax, vMax) = GetUVs(type, false);
        var (suMin, svMin, suMax, svMax) = GetUVs(type, true);

        Vector3D<float> topColor = (type == BlockType.Grass) ? new Vector3D<float>(0.4f, 0.8f, 0.3f) : 
                                   (type == BlockType.Snow)  ? new Vector3D<float>(1.0f, 1.0f, 1.0f) :
                                   (type == BlockType.Dirt)  ? new Vector3D<float>(1.0f, 1.0f, 1.0f) : 
                                   (type == BlockType.Stone) ? new Vector3D<float>(1.0f, 1.0f, 1.0f) : color;
        
        Vector3D<float> sideColor = (type == BlockType.Grass) ? new Vector3D<float>(1.0f, 1.0f, 1.0f) : 
                                    (type == BlockType.Snow)  ? new Vector3D<float>(1.0f, 1.0f, 1.0f) :
                                    (type == BlockType.Dirt)  ? new Vector3D<float>(1.0f, 1.0f, 1.0f) : 
                                    (type == BlockType.Stone) ? new Vector3D<float>(1.0f, 1.0f, 1.0f) : color;

        // Face Back (z-)
        if (back)
        {
            uint b = (uint)vertices.Count;
            vertices.Add(new Vertex(pos + new Vector3D<float>(0, 0, 0), sideColor, new Vector2D<float>(suMax, svMax)));
            vertices.Add(new Vertex(pos + new Vector3D<float>(0, 1, 0), sideColor, new Vector2D<float>(suMax, svMin)));
            vertices.Add(new Vertex(pos + new Vector3D<float>(s, 1, 0), sideColor, new Vector2D<float>(suMin, svMin)));
            vertices.Add(new Vertex(pos + new Vector3D<float>(s, 0, 0), sideColor, new Vector2D<float>(suMin, svMax)));
            indices.AddRange(new uint[] { b, b + 1, b + 2, b, b + 2, b + 3 });
        }

        // Face Front (z+)
        if (front)
        {
            uint b = (uint)vertices.Count;
            vertices.Add(new Vertex(pos + new Vector3D<float>(0, 0, s), sideColor, new Vector2D<float>(suMin, svMax)));
            vertices.Add(new Vertex(pos + new Vector3D<float>(s, 0, s), sideColor, new Vector2D<float>(suMax, svMax)));
            vertices.Add(new Vertex(pos + new Vector3D<float>(s, 1, s), sideColor, new Vector2D<float>(suMax, svMin)));
            vertices.Add(new Vertex(pos + new Vector3D<float>(0, 1, s), sideColor, new Vector2D<float>(suMin, svMin)));
            indices.AddRange(new uint[] { b, b + 1, b + 2, b, b + 2, b + 3 });
        }

        // Face Left (x-)
        if (left)
        {
            uint b = (uint)vertices.Count;
            vertices.Add(new Vertex(pos + new Vector3D<float>(0, 0, 0), sideColor, new Vector2D<float>(suMin, svMax)));
            vertices.Add(new Vertex(pos + new Vector3D<float>(0, 0, s), sideColor, new Vector2D<float>(suMax, svMax)));
            vertices.Add(new Vertex(pos + new Vector3D<float>(0, 1, s), sideColor, new Vector2D<float>(suMax, svMin)));
            vertices.Add(new Vertex(pos + new Vector3D<float>(0, 1, 0), sideColor, new Vector2D<float>(suMin, svMin)));
            indices.AddRange(new uint[] { b, b + 1, b + 2, b, b + 2, b + 3 });
        }

        // Face Right (x+)
        if (right)
        {
            uint b = (uint)vertices.Count;
            vertices.Add(new Vertex(pos + new Vector3D<float>(s, 0, 0), sideColor, new Vector2D<float>(suMax, svMax)));
            vertices.Add(new Vertex(pos + new Vector3D<float>(s, 1, 0), sideColor, new Vector2D<float>(suMax, svMin)));
            vertices.Add(new Vertex(pos + new Vector3D<float>(s, 1, s), sideColor, new Vector2D<float>(suMin, svMin)));
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
    Snow
}

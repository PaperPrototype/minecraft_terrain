using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;
using Unity.Jobs;
using Unity.Collections;
using System.Runtime.CompilerServices;

public class Defs
{
    public const int chunkNum = 10;

    public const int chunkSize = 16;

    public static readonly Vector3 VertexOffset = new Vector3(0.5f, 0.5f, 0.5f);

    public static readonly Vector3[] Vertices = new Vector3[8]
    {
        new Vector3(-0.5f, -0.5f, -0.5f) + VertexOffset,
        new Vector3(0.5f, -0.5f, -0.5f) + VertexOffset,
        new Vector3(0.5f, 0.5f, -0.5f) + VertexOffset,
        new Vector3(-0.5f, 0.5f, -0.5f) + VertexOffset,
        new Vector3(-0.5f, -0.5f, 0.5f) + VertexOffset,
        new Vector3(0.5f, -0.5f, 0.5f) + VertexOffset,
        new Vector3(0.5f, 0.5f, 0.5f) + VertexOffset,
        new Vector3(-0.5f, 0.5f, 0.5f) + VertexOffset,
    };

    public static readonly int[,] BuildOrder = new int[6, 4]
    {
        // right, left, up, down, front, back

        // 0 1 2 2 1 3 <- triangle order
        
        {1, 2, 5, 6}, // right face
        {4, 7, 0, 3}, // left face
        
        {3, 7, 2, 6}, // up face
        {1, 5, 0, 4}, // down face
        
        {5, 6, 4, 7}, // front face
        {0, 3, 1, 2}, // back face
    };

    public static readonly int[] BuildOrder1D = new int[6 * 4]
    {
        // right, left, up, down, front, back

        // 0 1 2 2 1 3 <- triangle order
        
        1, 2, 5, 6, // right face
        4, 7, 0, 3, // left face
        
        3, 7, 2, 6, // up face
        1, 5, 0, 4, // down face
        
        5, 6, 4, 7, // front face
        0, 3, 1, 2, // back face
    };

    public static int BuildOrderIndex(int first, int second)
    {
        return BuildOrder1D[(first * 6) + second];
    }

    public static readonly int3[] NeighborOffset = new int3[6]
    {
        new int3(1, 0, 0),  // right
        new int3(-1, 0, 0), // left
        new int3(0, 1, 0),  // up
        new int3(0, -1, 0), // down
        new int3(0, 0, 1),  // front
        new int3(0, 0, -1), // back
    };

    public static readonly Vector2[] UVs = new Vector2[4]
    {
        new Vector2(0, 0),
        new Vector2(0, 1),
        new Vector2(1, 0),
        new Vector2(1, 1),
    };

    public struct ChunkJob : IJob
    {
        // write info to these
        public NativeArray<Vector3> vertices;
        public NativeArray<int> triangles;
        public NativeArray<Vector2> uvs;
        public NativeArray<int> data;
        public NativeArray<int> vertexIndex;
        public NativeArray<int> triangleIndex;

        // read from these
        [ReadOnly]
        public NativeArray<int> atlasSize;
        [ReadOnly]
        public NativeArray<JobVoxelType> voxelTypes;

        // copied variables (not referenced)
        public Vector3 position;

        public void Execute()
        {
            FastNoiseLite noise = new FastNoiseLite();
            noise.SetNoiseType(FastNoiseLite.NoiseType.OpenSimplex2);

            for (int x = 0; x < Defs.chunkSize; x++)
            {
                for (int y = 0; y < Defs.chunkSize; y++)
                {
                    for (int z = 0; z < Defs.chunkSize; z++)
                    {
                        data[Utils.To1D(x, y, z)] = VoxelType(x, y, z, noise);
                    }
                }
            }

            for (int x = 0; x < Defs.chunkSize; x++)
            {
                for (int y = 0; y < Defs.chunkSize; y++)
                {
                    for (int z = 0; z < Defs.chunkSize; z++)
                    {
                        if (IsSolid(data[Utils.To1D(x, y, z)]))
                        {
                            DrawVoxel(x, y, z);
                        }
                    }
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void DrawVoxel(int x, int y, int z)
        {
            Vector3 pos = new Vector3(x, y, z);

            for (int side = 0; side < 6; side++)
            {
                // if we are outside of the chunk then just draw as if there was a neighbor
                if (IsOutsideChunk(x + Defs.NeighborOffset[side].x, y + Defs.NeighborOffset[side].y, z + Defs.NeighborOffset[side].z)) {
                    vertices[vertexIndex[0] + 0] = Defs.Vertices[Defs.BuildOrder[side, 0]] + pos;
                    vertices[vertexIndex[0] + 1] = Defs.Vertices[Defs.BuildOrder[side, 1]] + pos;
                    vertices[vertexIndex[0] + 2] = Defs.Vertices[Defs.BuildOrder[side, 2]] + pos;
                    vertices[vertexIndex[0] + 3] = Defs.Vertices[Defs.BuildOrder[side, 3]] + pos;

                    // get the correct triangle index
                    triangles[triangleIndex[0] + 0] = vertexIndex[0] + 0;
                    triangles[triangleIndex[0] + 1] = vertexIndex[0] + 1;
                    triangles[triangleIndex[0] + 2] = vertexIndex[0] + 2;
                    triangles[triangleIndex[0] + 3] = vertexIndex[0] + 2;
                    triangles[triangleIndex[0] + 4] = vertexIndex[0] + 1;
                    triangles[triangleIndex[0] + 5] = vertexIndex[0] + 3;

                    // max size of a textures width and height
                    float size = 1f / (float)atlasSize[0];

                    // get the offset for that texture
                    Vector2 offset = voxelTypes[data[Utils.To1D(x, y, z)]].offset / (float)atlasSize[0];

                    // set the uv's (different than the quad uv's due to the order of the lookup tables in DataDefs.cs)
                    uvs[vertexIndex[0] + 0] = new Vector2(offset.x, offset.y);
                    uvs[vertexIndex[0] + 1] = new Vector2(offset.x, offset.y + size);
                    uvs[vertexIndex[0] + 2] = new Vector2(offset.x + size, offset.y);
                    uvs[vertexIndex[0] + 3] = new Vector2(offset.x + size, offset.y + size);

                    // set the uv's (different than the quad uv's due to the order of the lookup tables in DataDefs.cs)
                    //m_uvs[m_vertexIndex + 0] = new Vector2(0, 0);
                    //m_uvs[m_vertexIndex + 1] = new Vector2(0, 1);
                    //m_uvs[m_vertexIndex + 2] = new Vector2(1, 0);
                    //m_uvs[m_vertexIndex + 3] = new Vector2(1, 1);

                    //Debug.Log("voxel x:" + x + " y:" + y + " z:" + z);
                    //Debug.Log("uv x:" + uvs[vertexIndex[0] + 0].x + " y:" + uvs[vertexIndex[0] + 0].y);
                    //Debug.Log("uv x:" + uvs[vertexIndex[0] + 1].x + " y:" + uvs[vertexIndex[0] + 1].y);
                    //Debug.Log("uv x:" + uvs[vertexIndex[0] + 2].x + " y:" + uvs[vertexIndex[0] + 2].y);
                    //Debug.Log("uv x:" + uvs[vertexIndex[0] + 3].x + " y:" + uvs[vertexIndex[0] + 3].y);

                    // increment by 4 because we only added 4 vertices
                    vertexIndex[0] += 4;

                    // increment by 6 because we added 6 int's to our triangles array
                    triangleIndex[0] += 6;

                }

                // if we are inside of the chunk check if sibling voxels exist
                else
                {
                    if (!IsSolid(data[Utils.To1D(x + Defs.NeighborOffset[side].x, y + Defs.NeighborOffset[side].y, z + Defs.NeighborOffset[side].z)]))
                    {
                        vertices[vertexIndex[0] + 0] = Defs.Vertices[Defs.BuildOrder[side, 0]] + pos;
                        vertices[vertexIndex[0] + 1] = Defs.Vertices[Defs.BuildOrder[side, 1]] + pos;
                        vertices[vertexIndex[0] + 2] = Defs.Vertices[Defs.BuildOrder[side, 2]] + pos;
                        vertices[vertexIndex[0] + 3] = Defs.Vertices[Defs.BuildOrder[side, 3]] + pos;

                        // get the correct triangle index
                        triangles[triangleIndex[0] + 0] = vertexIndex[0] + 0;
                        triangles[triangleIndex[0] + 1] = vertexIndex[0] + 1;
                        triangles[triangleIndex[0] + 2] = vertexIndex[0] + 2;
                        triangles[triangleIndex[0] + 3] = vertexIndex[0] + 2;
                        triangles[triangleIndex[0] + 4] = vertexIndex[0] + 1;
                        triangles[triangleIndex[0] + 5] = vertexIndex[0] + 3;

                        // max size of a textures width and height
                        float size = 1f / (float)atlasSize[0];

                        // get the offset for that texture
                        Vector2 offset = voxelTypes[data[Utils.To1D(x, y, z)]].offset / (float)atlasSize[0];

                        // set the uv's (different than the quad uv's due to the order of the lookup tables in DataDefs.cs)
                        uvs[vertexIndex[0] + 0] = new Vector2(offset.x, offset.y);
                        uvs[vertexIndex[0] + 1] = new Vector2(offset.x, offset.y + size);
                        uvs[vertexIndex[0] + 2] = new Vector2(offset.x + size, offset.y);
                        uvs[vertexIndex[0] + 3] = new Vector2(offset.x + size, offset.y + size);

                        // set the uv's (different than the quad uv's due to the order of the lookup tables in DataDefs.cs)
                        //m_uvs[m_vertexIndex + 0] = new Vector2(0, 0);
                        //m_uvs[m_vertexIndex + 1] = new Vector2(0, 1);
                        //m_uvs[m_vertexIndex + 2] = new Vector2(1, 0);
                        //m_uvs[m_vertexIndex + 3] = new Vector2(1, 1);

                        //Debug.Log("voxel x:" + x + " y:" + y + " z:" + z);
                        //Debug.Log("uv x:" + uvs[vertexIndex[0] + 0].x + " y:" + uvs[vertexIndex[0] + 0].y);
                        //Debug.Log("uv x:" + uvs[vertexIndex[0] + 1].x + " y:" + uvs[vertexIndex[0] + 1].y);
                        //Debug.Log("uv x:" + uvs[vertexIndex[0] + 2].x + " y:" + uvs[vertexIndex[0] + 2].y);
                        //Debug.Log("uv x:" + uvs[vertexIndex[0] + 3].x + " y:" + uvs[vertexIndex[0] + 3].y);

                        // increment by 4 because we only added 4 vertices
                        vertexIndex[0] += 4;

                        // increment by 6 because we added 6 int's to our triangles array
                        triangleIndex[0] += 6;
                    }
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool IsSolid(int type)
        {
            return voxelTypes[type].solid;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int VoxelType(int x, int y, int z, FastNoiseLite noise)
        {
            float grassHeight = ((noise.GetNoise(x + position.x, z + position.z + 10) + 1) / 2 * Defs.chunkSize);

            // the voxels y position
            float yPos = position.y + (float)y;

            // if y is above grass height
            if (yPos > grassHeight)
            {
                // return air
                return 0;
            }

            // if below ground
            else
            {
                float dirtHeight = ((noise.GetNoise(x + position.x, z + position.z) + 1) / 2 * Defs.chunkSize - 3f);
                // below grass
                if (yPos <= dirtHeight)
                {
                    // return dirt
                    return 1;
                }

                // above dirt
                // below grass
                // return grass
                return 2;
            }
            //float islands = noise.GetNoise(x + position[0].x + 100000, y + position[0].y + 100000, z + position[0].z + 100000);

            //if (islands >= 0.9f)
            //{
            //    // dirt
            //    return 1;
            //}

            //float grassIslands = noise.GetNoise(x + position[0].x - 100000, y - position[0].y + 100000, z + position[0].z + 100000);

            //if (grassIslands >= 0.8f)
            //{
            //    // grass
            //    return 2;
            //}
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool IsOutsideChunk(int x, int y, int z)
        {
            // if is outside of bounds
            if (x > Defs.chunkSize - 1 || x < 0 ||
                y > Defs.chunkSize - 1 || y < 0 ||
                z > Defs.chunkSize - 1 || z < 0)
            {
                return true;
            }

            return false;
        }
    }
}

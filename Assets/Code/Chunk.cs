using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Unity.Collections;
using Unity.Jobs;
using System.Runtime.CompilerServices;

public class Chunk
{
    private NativeArray<Vector3> m_vertices;
    private NativeArray<int> m_triangles;
    private NativeArray<Vector2> m_uvs;
    private NativeArray<int> m_data;
    private NativeArray<int> m_vertexIndex;
    private NativeArray<int> m_triangleIndex;
    private NativeArray<int> m_atlasSize;
    private NativeArray<JobVoxelType> m_voxelTypes;

    private JobHandle m_jobHandle;
    private Defs.ChunkJob m_job;

    private MeshFilter m_meshFilter;
    private MeshRenderer m_meshRenderer;
    private MeshCollider m_meshCollider;
    private GameObject m_gameObject;

    private bool needsDrawn;

    public Chunk(Material material, NativeArray<JobVoxelType> voxelTypes, Vector3 pos, Transform parent, int atlasSize)
    {
        m_voxelTypes = voxelTypes;

        m_gameObject = new GameObject();
        m_gameObject.transform.position = pos;
        m_gameObject.transform.parent = parent;
        m_gameObject.name = "x" + pos.x + " y" + pos.y + " z" + pos.z;
        m_meshFilter = m_gameObject.AddComponent<MeshFilter>();
        m_meshRenderer = m_gameObject.AddComponent<MeshRenderer>();
        m_meshCollider = m_gameObject.AddComponent<MeshCollider>();
        m_meshRenderer.material = material;

        m_data = new NativeArray<int>(Defs.chunkSize * Defs.chunkSize * Defs.chunkSize, Allocator.Persistent);
        m_vertices = new NativeArray<Vector3>(24 * Defs.chunkSize * Defs.chunkSize * Defs.chunkSize, Allocator.Persistent);
        m_triangles = new NativeArray<int>(36 * Defs.chunkSize * Defs.chunkSize * Defs.chunkSize, Allocator.Persistent);
        m_uvs = new NativeArray<Vector2>(24 * Defs.chunkSize * Defs.chunkSize * Defs.chunkSize, Allocator.Persistent);
        m_vertexIndex = new NativeArray<int>(1, Allocator.Persistent);
        m_triangleIndex = new NativeArray<int>(1, Allocator.Persistent);
        m_atlasSize = new NativeArray<int>(1, Allocator.Persistent);

        // set vars
        m_atlasSize[0] = atlasSize;

        needsDrawn = true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Vector3 GetPosition()
    {
        return m_gameObject.transform.position;
    }

    // call this right before scheduling the chunks draw job
    // check if the chunk can be recycled
    /// <summary>
    /// If the job is complete, resets all variables and resets the position.
    /// Also completes the job and sets the mesh of the gameObject.
    /// </summary>
    /// <param name="newPos"></param>
    /// <returns></returns>
    public bool TryReset(Vector3 offset)
    {
        // if in the middle of a job
        if (!m_jobHandle.IsCompleted)
        {
            return false;
        }


        // the position to move to and that we will draw at
        m_meshFilter.mesh = new Mesh();
        m_meshCollider.sharedMesh = new Mesh();

        m_gameObject.transform.position += offset;
        m_gameObject.name = "x" + offset.x + " y" + offset.y + " z" + offset.z;

        // set needs Drawn to true
        needsDrawn = true;

        // successfully reset the chunk
        return true;
    }

    public bool TryDispose()
    {
        // if in the middle of a job
        if (!m_jobHandle.IsCompleted)
        {
            return false;
        }

        m_vertices.Dispose();
        m_triangles.Dispose();
        m_uvs.Dispose();
        m_vertexIndex.Dispose();
        m_triangleIndex.Dispose();

        
        m_atlasSize.Dispose();

        // chunk data
        m_data.Dispose();

        return true;
    }

    // only schedules if the previous job is completed
    public bool TrySchedule()
    {
        if (needsDrawn && m_jobHandle.IsCompleted)
        {
            Debug.Log("scheduling a draw");

            // create a new job
            m_job = new Defs.ChunkJob();

            m_job.vertices = m_vertices;
            m_job.triangles = m_triangles;
            m_job.uvs = m_uvs;
            m_job.data = m_data;
            m_job.vertexIndex = m_vertexIndex;
            m_job.triangleIndex = m_triangleIndex;
            m_job.voxelTypes = m_voxelTypes;
            m_job.atlasSize = m_atlasSize;
            m_job.position = m_gameObject.transform.position;

            m_jobHandle = new JobHandle();
            m_jobHandle = m_job.Schedule();

            return true;
        }

        return false;
    }

    public bool TryComplete()
    {
        if (needsDrawn)
        {
            m_jobHandle.Complete();

            Mesh mesh = new Mesh
            {
                indexFormat = IndexFormat.UInt32,
                vertices = m_vertices.Slice<Vector3>(0, m_job.vertexIndex[0]).ToArray(),
                triangles = m_triangles.Slice<int>(0, m_job.triangleIndex[0]).ToArray(),
                uv = m_uvs.Slice<Vector2>(0, m_job.vertexIndex[0]).ToArray()
            };

            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            m_meshFilter.mesh = mesh;
            m_meshCollider.sharedMesh = mesh;

            needsDrawn = false;

            // job is completed
            return true;
        }

        return false;
    }

    // ALL FUNCTIONS BELOW THIS ARE NOT BEING USED
    // I JUST KEEP THEM ARAOUND FOR REFERENCE
    // ACTUAL CHUNK MESHING IS IN ChunkJob in Defs.cs

    private void DrawVoxel(int x, int y, int z)
    {
        Vector3 pos = new Vector3(x, y, z);

        for (int side = 0; side < 6; side++)
        {
            // if we are outside of the chunk then just draw as if there was a neighbor
            if (IsOutsideChunk(x + Defs.NeighborOffset[side].x, y + Defs.NeighborOffset[side].y, z + Defs.NeighborOffset[side].z))
            {
                m_vertices[m_vertexIndex[0] + 0] = Defs.Vertices[Defs.BuildOrder[side, 0]] + pos;
                m_vertices[m_vertexIndex[0] + 1] = Defs.Vertices[Defs.BuildOrder[side, 1]] + pos;
                m_vertices[m_vertexIndex[0] + 2] = Defs.Vertices[Defs.BuildOrder[side, 2]] + pos;
                m_vertices[m_vertexIndex[0] + 3] = Defs.Vertices[Defs.BuildOrder[side, 3]] + pos;

                // get the correct triangle index
                m_triangles[m_triangleIndex[0] + 0] = m_vertexIndex[0] + 0;
                m_triangles[m_triangleIndex[0] + 1] = m_vertexIndex[0] + 1;
                m_triangles[m_triangleIndex[0] + 2] = m_vertexIndex[0] + 2;
                m_triangles[m_triangleIndex[0] + 3] = m_vertexIndex[0] + 2;
                m_triangles[m_triangleIndex[0] + 4] = m_vertexIndex[0] + 1;
                m_triangles[m_triangleIndex[0] + 5] = m_vertexIndex[0] + 3;

                // max size of a textures width and height
                float size = 1f / (float)m_atlasSize[0];

                // get the offset for that texture
                Vector2 offset = m_voxelTypes[m_data[Utils.To1D(x, y, z)]].offset / (float)m_atlasSize[0];

                // set the uv's (different than the quad uv's due to the order of the lookup tables in DataDefs.cs)
                m_uvs[m_vertexIndex[0] + 0] = new Vector2(offset.x, offset.y);
                m_uvs[m_vertexIndex[0] + 1] = new Vector2(offset.x, offset.y + size);
                m_uvs[m_vertexIndex[0] + 2] = new Vector2(offset.x + size, offset.y);
                m_uvs[m_vertexIndex[0] + 3] = new Vector2(offset.x + size, offset.y + size);

                // set the uv's (different than the quad uv's due to the order of the lookup tables in DataDefs.cs)
                //m_uvs[m_vertexIndex + 0] = new Vector2(0, 0);
                //m_uvs[m_vertexIndex + 1] = new Vector2(0, 1);
                //m_uvs[m_vertexIndex + 2] = new Vector2(1, 0);
                //m_uvs[m_vertexIndex + 3] = new Vector2(1, 1);

                //Debug.Log("voxel x:" + x + " y:" + y + " z:" + z);
                //Debug.Log("uv x:" + m_uvs[m_vertexIndex[0] + 0].x + " y:" + m_uvs[m_vertexIndex[0] + 0].y);
                //Debug.Log("uv x:" + m_uvs[m_vertexIndex[0] + 1].x + " y:" + m_uvs[m_vertexIndex[0] + 1].y);
                //Debug.Log("uv x:" + m_uvs[m_vertexIndex[0] + 2].x + " y:" + m_uvs[m_vertexIndex[0] + 2].y);
                //Debug.Log("uv x:" + m_uvs[m_vertexIndex[0] + 3].x + " y:" + m_uvs[m_vertexIndex[0] + 3].y);

                // increment by 4 because we only added 4 vertices
                m_vertexIndex[0] += 4;

                // increment by 6 because we added 6 int's to our triangles array
                m_triangleIndex[0] += 6;

                
            }
            // if we are inside of the chunk check if sibling voxels exist
            else
            {
                if (!IsSolid(m_data[Utils.To1D(x + Defs.NeighborOffset[side].x, y + Defs.NeighborOffset[side].y, z + Defs.NeighborOffset[side].z)]))
                {
                    m_vertices[m_vertexIndex[0] + 0] = Defs.Vertices[Defs.BuildOrder[side, 0]] + pos;
                    m_vertices[m_vertexIndex[0] + 1] = Defs.Vertices[Defs.BuildOrder[side, 1]] + pos;
                    m_vertices[m_vertexIndex[0] + 2] = Defs.Vertices[Defs.BuildOrder[side, 2]] + pos;
                    m_vertices[m_vertexIndex[0] + 3] = Defs.Vertices[Defs.BuildOrder[side, 3]] + pos;

                    // get the correct triangle index
                    m_triangles[m_triangleIndex[0] + 0] = m_vertexIndex[0] + 0;
                    m_triangles[m_triangleIndex[0] + 1] = m_vertexIndex[0] + 1;
                    m_triangles[m_triangleIndex[0] + 2] = m_vertexIndex[0] + 2;
                    m_triangles[m_triangleIndex[0] + 3] = m_vertexIndex[0] + 2;
                    m_triangles[m_triangleIndex[0] + 4] = m_vertexIndex[0] + 1;
                    m_triangles[m_triangleIndex[0] + 5] = m_vertexIndex[0] + 3;

                    // max size of a textures width and height
                    float size = 1f / (float)m_atlasSize[0];

                    // get the offset for that texture
                    Vector2 offset = m_voxelTypes[m_data[Utils.To1D(x, y, z)]].offset / (float)m_atlasSize[0];

                    // set the uv's (different than the quad uv's due to the order of the lookup tables in DataDefs.cs)
                    m_uvs[m_vertexIndex[0] + 0] = new Vector2(offset.x, offset.y);
                    m_uvs[m_vertexIndex[0] + 1] = new Vector2(offset.x, offset.y + size);
                    m_uvs[m_vertexIndex[0] + 2] = new Vector2(offset.x + size, offset.y);
                    m_uvs[m_vertexIndex[0] + 3] = new Vector2(offset.x + size, offset.y + size);

                    // set the uv's (different than the quad uv's due to the order of the lookup tables in DataDefs.cs)
                    //m_uvs[m_vertexIndex + 0] = new Vector2(0, 0);
                    //m_uvs[m_vertexIndex + 1] = new Vector2(0, 1);
                    //m_uvs[m_vertexIndex + 2] = new Vector2(1, 0);
                    //m_uvs[m_vertexIndex + 3] = new Vector2(1, 1);

                    //Debug.Log("voxel x:" + x + " y:" + y + " z:" + z);
                    //Debug.Log("uv x:" + m_uvs[m_vertexIndex[0] + 0].x + " y:" + m_uvs[m_vertexIndex[0] + 0].y);
                    //Debug.Log("uv x:" + m_uvs[m_vertexIndex[0] + 1].x + " y:" + m_uvs[m_vertexIndex[0] + 1].y);
                    //Debug.Log("uv x:" + m_uvs[m_vertexIndex[0] + 2].x + " y:" + m_uvs[m_vertexIndex[0] + 2].y);
                    //Debug.Log("uv x:" + m_uvs[m_vertexIndex[0] + 3].x + " y:" + m_uvs[m_vertexIndex[0] + 3].y);

                    // increment by 4 because we only added 4 vertices
                    m_vertexIndex[0] += 4;

                    // increment by 6 because we added 6 int's to our triangles array
                    m_triangleIndex[0] += 6;
                }
            }
        }
    }

    private bool IsSolid(int type)
    {
        return m_voxelTypes[type].solid;
    }

    private int VoxelType(int x, int y, int z, FastNoiseLite noise)
    {
        float grassHeight = (noise.GetNoise(x, z + 10) + 1) / 2 * Defs.chunkSize;
        float dirtHeight = (noise.GetNoise(x, z) + 1) / 2 * Defs.chunkSize - 3f;

        // if y is below grass height
        if (y <= grassHeight)
        {
            return 2;
        }
        // if below ground
        else if (y <= dirtHeight)
        {
            // return dirt
            return 1;
        }
        // above ground
        else
        {
            // return air
            return 0;
        }
    }

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

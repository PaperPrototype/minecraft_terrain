using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Collections;

[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
public class Voxel : MonoBehaviour
{
    private Mesh m_mesh;
    private NativeArray<Vector3> m_vertices;
    private NativeArray<int> m_triangles;
    private NativeArray<Vector2> m_uvs;
    private int m_vertexIndex = 0;
    private int m_triangleIndex = 0;

    MeshFilter meshFilter;
    MeshRenderer meshRenderer;

    // Start is called before the first frame update
    void Start()
    {
        meshFilter = gameObject.GetComponent<MeshFilter>();
        meshRenderer = gameObject.GetComponent<MeshRenderer>();
    }

    // Update is called once per frame
    void Update()
    {
        m_vertices = new NativeArray<Vector3>(24, Allocator.Temp);
        m_triangles = new NativeArray<int>(36, Allocator.Temp);
        m_uvs = new NativeArray<Vector2>(24, Allocator.Temp);

        DrawVoxel();

        m_mesh = new Mesh
        {
            vertices = m_vertices.ToArray(),
            triangles = m_triangles.ToArray(),
            uv = m_uvs.ToArray(),
        };

        m_mesh.RecalculateNormals();
        m_mesh.RecalculateBounds();
        meshFilter.mesh = m_mesh;

        m_vertices.Dispose();
        m_triangles.Dispose();
        m_uvs.Dispose();
    }

    private void DrawVoxel()
    {
        for (int side = 0; side < 6; side++)
        {
            m_vertices[m_vertexIndex + 0] = Defs.Vertices[Defs.BuildOrder[side, 0]];
            m_vertices[m_vertexIndex + 1] = Defs.Vertices[Defs.BuildOrder[side, 1]];
            m_vertices[m_vertexIndex + 2] = Defs.Vertices[Defs.BuildOrder[side, 2]];
            m_vertices[m_vertexIndex + 3] = Defs.Vertices[Defs.BuildOrder[side, 3]];

            // get the correct triangle index
            m_triangles[m_triangleIndex + 0] = m_vertexIndex + 0;
            m_triangles[m_triangleIndex + 1] = m_vertexIndex + 1;
            m_triangles[m_triangleIndex + 2] = m_vertexIndex + 2;
            m_triangles[m_triangleIndex + 3] = m_vertexIndex + 2;
            m_triangles[m_triangleIndex + 4] = m_vertexIndex + 1;
            m_triangles[m_triangleIndex + 5] = m_vertexIndex + 3;

            // set the uv's (different than the quad uv's due to the order of the lookup tables in DataDefs.cs)
            m_uvs[m_vertexIndex + 0] = new Vector2(0, 0);
            m_uvs[m_vertexIndex + 1] = new Vector2(0, 1);
            m_uvs[m_vertexIndex + 2] = new Vector2(1, 0);
            m_uvs[m_vertexIndex + 3] = new Vector2(1, 1);

            // increment by 4 because we only added 4 vertices
            m_vertexIndex += 4;

            // increment by 6 because we added 6 int's to our triangles array
            m_triangleIndex += 6;
        }
    }
}

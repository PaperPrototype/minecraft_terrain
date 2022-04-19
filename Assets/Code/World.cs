using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Collections;
using UnityEngine.UI;

public class World : MonoBehaviour
{
    public List<VoxelType> voxelTypes = new List<VoxelType>();
    private NativeArray<JobVoxelType> m_voxelTypes;

    // offset to the center of the chunks
    private int offset = (Defs.chunkNum * Defs.chunkSize) / 2;

    public Transform spawn;
    public Rigidbody player;
    public Slider slider;

    public int atlasSize = 1;
    public Material material;
    public Chunk[,,] chunks;

    [SerializeField]
    public bool loaded;
    private bool run;

    // Start is called before the first frame update
    private void Start()
    {
        m_voxelTypes = new NativeArray<JobVoxelType>(voxelTypes.Count, Allocator.Persistent);

        // prefill the voxel types array for use in the jobs
        for (int i = 0; i < voxelTypes.Count; i++)
        {
            m_voxelTypes[i] = new JobVoxelType(voxelTypes[i]);
        }

        // gets set to true in Setup() once Setup() is finished
        loaded = false;

        // don't wait in this frame for the chunks to  be finished
        StartCoroutine(Run());
    }

    private void OnDisable()
    {
        run = false;

        m_voxelTypes.Dispose();
    }

    private void Recycle()
    {
        for (int x = 0; x < Defs.chunkNum; x++)
        {
            for (int y = 0; y < Defs.chunkNum; y++)
            {
                for (int z = 0; z < Defs.chunkNum; z++)
                {
                    // x
                    if (player.position.x + offset < chunks[x, y, z].GetPosition().x)
                    {
                        chunks[x, y, z].TryReset(new Vector3(Defs.chunkNum * Defs.chunkSize, 0, 0) * -1f);
                    }
                    if (player.position.x - offset > chunks[x, y, z].GetPosition().x)
                    {
                        chunks[x, y, z].TryReset(new Vector3(Defs.chunkNum * Defs.chunkSize, 0, 0));
                    }

                    // y
                    if (player.position.y + offset < chunks[x, y, z].GetPosition().y)
                    {
                        chunks[x, y, z].TryReset(new Vector3(0, Defs.chunkNum * Defs.chunkSize, 0) * -1f);
                    }
                    if (player.position.y - offset > chunks[x, y, z].GetPosition().y)
                    {
                        chunks[x, y, z].TryReset(new Vector3(0, Defs.chunkNum * Defs.chunkSize, 0));
                    }

                    // z
                    if (player.position.z + offset < chunks[x, y, z].GetPosition().z)
                    {
                        chunks[x, y, z].TryReset(new Vector3(0, 0, Defs.chunkNum * Defs.chunkSize) * -1f);
                    }
                    if (player.position.z - offset > chunks[x, y, z].GetPosition().z)
                    {
                       chunks[x, y, z].TryReset(new Vector3(0, 0, Defs.chunkNum * Defs.chunkSize));
                    }
                }
            }
        }
    }

    private IEnumerator Run()
    {
        run = true;
        chunks = new Chunk[Defs.chunkNum, Defs.chunkNum, Defs.chunkNum];

        // loading bar values
        float total = Defs.chunkNum * Defs.chunkNum * Defs.chunkNum;
        float current = 0f;

        for (int x = 0; x < Defs.chunkNum; x++)
        {
            for (int y = 0; y < Defs.chunkNum; y++)
            {
                for (int z = 0; z < Defs.chunkNum; z++)
                {
                    chunks[x, y, z] = new Chunk(material, m_voxelTypes, new Vector3(x * Defs.chunkSize, y * Defs.chunkSize, z * Defs.chunkSize), gameObject.transform, atlasSize);
                    chunks[x, y, z].TrySchedule();

                    current += 1f;

                    slider.value = current / total;

                    yield return null;
                }
            }
        }

        while (TryComplete() == false)
        {
            TryComplete();
        }

        player.velocity = Vector3.zero;
        player.transform.position = spawn.position;
        slider.gameObject.SetActive(false);

        loaded = true;

        while(run)
        {
            // set chunks to need drawn
            Recycle();

            // schedule jobs
            TrySchedule();

            // we can try to complete those jobs
            TryComplete();

            // this is necessary so we can give time for the regular update loop to start
            yield return null;
        }

        while (TryComplete() == false)
        {
            TryComplete();
            yield return null;
        }

        while (TryDispose() == false)
        {
            TryDispose();
            yield return null;
        }
    }

    private bool TrySchedule()
    {
        // initially set completed to true
        bool scheduled = true;

        for (int x = 0; x < Defs.chunkNum; x++)
        {
            for (int y = 0; y < Defs.chunkNum; y++)
            {
                for (int z = 0; z < Defs.chunkNum; z++)
                {
                    if (!chunks[x, y, z].TrySchedule())
                    {
                        scheduled = false;
                    } 
                }
            }
        }

        return scheduled;
    }

    private bool TryComplete()
    {
        // initially set completed to true
        bool complete = true;

        for (int x = 0; x < Defs.chunkNum; x++)
        {
            for (int y = 0; y < Defs.chunkNum; y++)
            {
                for (int z = 0; z < Defs.chunkNum; z++)
                {
                    // if not completed then complete is false since all chunks have not completed drawing
                    if (!chunks[x, y, z].TryComplete())
                    {
                        complete = false;
                    }
                }
            }
        }

        return complete;
    }

    private bool TryDispose()
    {
        // initially set completed to true
        bool disposed = true;

        for (int x = 0; x < Defs.chunkNum; x++)
        {
            for (int y = 0; y < Defs.chunkNum; y++)
            {
                for (int z = 0; z < Defs.chunkNum; z++)
                {
                    // if not completed then complete is false since all chunks have not completed drawing
                    if (!chunks[x, y, z].TryDispose())
                    {
                        disposed = false;
                    }
                }
            }
        }

        return disposed;
    }
}

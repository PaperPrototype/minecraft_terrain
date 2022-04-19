using System;
using System.Globalization;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using UnityEngine.Internal;
using UnityEngine.Scripting;

[Serializable]
public struct VoxelType
{
    public string name;
    public Vector2 offset;
    public bool solid;
}

public struct JobVoxelType
{
    public Vector2 offset;
    public bool solid;

    public JobVoxelType(VoxelType voxelType)
    {
        this.offset = voxelType.offset;
        this.solid = voxelType.solid;
    }
}
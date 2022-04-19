using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class Utils
{
    public static int To1D(int x, int y, int z)
    {
        return (x * Defs.chunkSize * Defs.chunkSize) + (y * Defs.chunkSize) + z;
    }
}

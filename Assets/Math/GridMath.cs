using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

public static class GridMath
{
    public static int3 WorldToGrid(float3 worldPosition, float gridBlockSize)
    {
        return new int3(Mathf.RoundToInt(worldPosition.x / gridBlockSize),
            Mathf.RoundToInt(worldPosition.y / gridBlockSize),
            Mathf.RoundToInt(worldPosition.z / gridBlockSize));
    }

    public static float3 GridToWorld(int3 chunkPosition, float gridBlockSize)
    {
        return new float3(chunkPosition.x * gridBlockSize,
            chunkPosition.y * gridBlockSize,
            chunkPosition.z * gridBlockSize);
    }

    public static int PositionToIndex(int3 position, int xMax, int yMax)
    {
        return position.x + (position.y * xMax) + (position.z * xMax * yMax);
    }

    public static int3 IndexToPosition(int index, int xMax, int yMax)
    {
        int x = index % xMax;
        int y = (index / xMax) % yMax;
        int z = index / (xMax * yMax);
        return new int3(x, y, z);
    }

    public static bool BlockInBounds(int3 blockPosition, int3 centralBlock, int3 extents)
    {
        return !(blockPosition.x < centralBlock.x - extents.x || blockPosition.x >= centralBlock.x + extents.x ||
                 blockPosition.y < centralBlock.y -  extents.y || blockPosition.y >= centralBlock.y +  extents.y ||
                 blockPosition.z < centralBlock.z - extents.z || blockPosition.z >= centralBlock.z + extents.z);
    }

    public static float3 Clamp(float3 worldPosition, float blockSize)
    {
        return new float3(
            Mathf.RoundToInt(worldPosition.x / blockSize) * blockSize,
            Mathf.RoundToInt(worldPosition.y / blockSize) * blockSize,
            Mathf.RoundToInt(worldPosition.z / blockSize) * blockSize);
    }
}

// Author: Peter Richards.
using Unity.Entities;
using Unity.Mathematics;

[System.Serializable]
[GenerateAuthoringComponent]
public struct BoidSpawnerComponent : IComponentData
{
    public float SpawnRate;
    public float NextSpawnTime;
    public float3 SpawnOffSet;

    public int AliveBoidCount;
    public int TotalBoidCount;
    public bool Enabled;
}

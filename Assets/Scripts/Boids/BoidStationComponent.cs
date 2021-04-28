// Author: Peter Richards.
using Unity.Entities;
using Unity.Mathematics;

[GenerateAuthoringComponent]
public struct BoidStationComponent : IComponentData
{
    public float HP;
    public uint GroupID;
    public float AttractRadius;
    public float PatrolRadius;
    public float3 TargetUp;
    public uint ParticleManagerIdx;
    public bool deaded;

    public static readonly uint DeathParticleIdx = 0;
}

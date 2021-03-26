using Unity.Entities;
using Unity.Mathematics;

[GenerateAuthoringComponent]
public struct BoidComponent : IComponentData
{
    public uint GroupID;
    public float HP;

    public Entity SettingsEntity;
    public float3 MoveForce;
    public float3 TargetUp;
}
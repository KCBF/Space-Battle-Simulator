using Unity.Entities;
using Unity.Mathematics;

[GenerateAuthoringComponent]
public struct BoidComponent : IComponentData
{
    public uint GroupID;
    public Entity SettingsEntity;
    public float3 moveForce;
    public float3 targetUp;
}
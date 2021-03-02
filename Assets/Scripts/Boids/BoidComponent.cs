using Unity.Entities;

[GenerateAuthoringComponent]
public struct BoidComponent : IComponentData
{
    public uint teamID;
}

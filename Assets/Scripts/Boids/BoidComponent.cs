using Unity.Entities;

[GenerateAuthoringComponent]
public struct BoidComponent : IComponentData
{
    public uint GroupID;
    public Entity SettingsEntity;
}
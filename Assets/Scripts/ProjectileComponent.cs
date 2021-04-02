using Unity.Entities;
using Unity.Mathematics;

[GenerateAuthoringComponent]
public struct ProjectileComponent : IComponentData
{
    public float Speed;
    public float Damage;
    public float LifeTime;
    public float DespawnTime;
    public Entity OwnerEntity;
}

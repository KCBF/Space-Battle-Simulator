using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

[GenerateAuthoringComponent]
public struct ProjectileComponent : IComponentData
{
    public float Speed;
    public float Damage;
    public float LifeTime;
    public float DespawnTime;
    public Entity OwnerEntity;
}

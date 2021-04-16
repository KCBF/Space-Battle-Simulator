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
    public uint ParticleManagerIdx;

    public static uint TrailParticle1Idx = 0;
    public static uint TrailParticle2Idx = 1;
    public static uint TrailParticle3Idx = 2;
    public static uint DeathParticleIdx = 3;
}

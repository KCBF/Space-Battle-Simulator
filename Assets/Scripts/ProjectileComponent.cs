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
    public int ParticleManagerIdx;

    public static int TrailParticle1Idx = 0;
    public static int TrailParticle2Idx = 1;
    public static int TrailParticle3Idx = 2;
    public static int DeathParticleIdx = 3;
}

// Author: Peter Richards.
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
    public float HitTime;
    public Entity OwnerEntity;
    public int ParticleManagerIdx;

    public static readonly int TrailParticle1Idx = 0;
    public static readonly int TrailParticle2Idx = 1;
    public static readonly int TrailParticle3Idx = 2;
    public static readonly int DeathParticleIdx = 3;
}

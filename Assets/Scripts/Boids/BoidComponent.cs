// Author: Peter Richards.
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

[GenerateAuthoringComponent]
public struct BoidComponent : IComponentData
{
    public static uint MaxGroupID = 4;
    public uint GroupID;
    public float HP;
    public float DiedTime;
    public float HitTime;

    public Entity SettingsEntity;
    public float3 MoveForce;
    public float3 TargetUp;

    public float3 LineOfSightForce;

    public float NextAllowShootTime;

    public static readonly int TrailParticleIdx = 0;
    public static readonly int DeathParticleIdx = 1;
    public static readonly int MuzzleParticleIdx = 2;
}
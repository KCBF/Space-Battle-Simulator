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

    public Entity SettingsEntity;
    public float3 MoveForce;
    public float3 TargetUp;

    public float3 LineOfSightForce;

    public float NextAllowShootTime;

    public static int TrailParticleIdx = 0;
    public static int DeathParticleIdx = 1;
    public static int MuzzleParticleIdx = 2;
}
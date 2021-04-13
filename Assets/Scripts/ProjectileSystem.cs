using System.Collections.Generic;
using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Extensions;
using Unity.Collections;
using Unity.Jobs;
using Unity.Physics.Authoring;
using UnityEngine;

[UpdateBefore(typeof(Unity.Physics.Systems.BuildPhysicsWorld))]
public class ProjectileSystem : ComponentSystem
{
    protected override void OnUpdate()
    {
        Entities
            .ForEach((Entity entity,
                ref Translation translation, ref Rotation rotation,
                ref ProjectileComponent projectile, ref PhysicsVelocity velocity) =>
            {
                velocity.Linear = math.forward(rotation.Value) * projectile.Speed;
                
                if (projectile.DespawnTime == 0.0f)
                    projectile.DespawnTime = (float)Time.ElapsedTime + projectile.LifeTime;
                
                else if (projectile.DespawnTime <= Time.ElapsedTime)
                    PostUpdateCommands.DestroyEntity(entity);
            });
    }
}
using System.Collections.Generic;
using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Extensions;
using Unity.Collections;
using UnityEngine;
using Unity.Jobs;
using Unity.Physics.Authoring;

[UpdateAfter(typeof(Unity.Physics.Systems.EndFramePhysicsSystem))]
public class ProjectileSystem : ComponentSystem
{
    protected override void OnUpdate()
    {
        //CapsulecastCommand.ScheduleBatch();

        Entities
            .ForEach((Entity entity,
                ref Translation translation, ref Rotation rot,
                ref ProjectileComponent projectile, ref PhysicsVelocity velocity) =>
            {
                velocity.Linear = math.forward(rot.Value) * projectile.Speed;
            });
    }
}
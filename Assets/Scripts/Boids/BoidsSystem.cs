using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Extensions;
using Unity.Collections;
using UnityEngine;
using Unity.Jobs;

//[UpdateBefore(typeof(Unity.Physics.Systems.EndFramePhysicsSystem))]
[UpdateAfter(typeof(Unity.Physics.Systems.EndFramePhysicsSystem))]
public class BoidsSystem : ComponentSystem
{
    Unity.Physics.Systems.BuildPhysicsWorld physicsWorldSystem;
    CollisionWorld collisionWorld;

    Unity.Mathematics.Random random;

    protected override void OnCreate()
    {
        physicsWorldSystem = World.GetOrCreateSystem<Unity.Physics.Systems.BuildPhysicsWorld>();
        random =  new Unity.Mathematics.Random(1);
    }

    protected override void OnUpdate()
    {
        collisionWorld = physicsWorldSystem.PhysicsWorld.CollisionWorld;
        float viewDst = 10.0f;

        Entities.ForEach((Entity entity, 
            ref Translation translation, ref Rotation rot, 
            ref PhysicsVelocity velocity, ref PhysicsMass mass,
            ref BoidComponent boid) =>
        {
            float3 forwardForce = math.forward(rot.Value) * 0.05f;
            ComponentExtensions.ApplyLinearImpulse(ref velocity, mass, forwardForce);

            NativeList<int> neighbours = new NativeList<int>(Allocator.Temp);
            GetNeighbours(collisionWorld, translation.Value, out neighbours, viewDst);

            int neighbourCount = 0;

            float3 averagePos = float3.zero;
            float3 averageVelocity = float3.zero;
            float3 averageSeparation = float3.zero;

            foreach (int neighbourIdx in neighbours)
            {
                Entity neighbourEntity = collisionWorld.Bodies[neighbourIdx].Entity;
                if (neighbourEntity == entity)
                    continue;

                RigidTransform neighbourTransform = collisionWorld.Bodies[neighbourIdx].WorldFromBody;
                PhysicsVelocity neighbourVelocity = EntityManager.GetComponentData<PhysicsVelocity>(neighbourEntity);

                if (!CanSeeNeighbour(translation.Value, neighbourTransform.pos, viewDst))
                    continue;

                ++neighbourCount;

                averagePos += neighbourTransform.pos;
                averageVelocity += neighbourVelocity.Linear;
                averageSeparation += math.normalize(translation.Value - neighbourTransform.pos);
            }

            if (neighbourCount == 0)
                return;

            averagePos /= neighbourCount;
            averagePos -= translation.Value;

            averageVelocity /= neighbourCount;
            averageVelocity -= velocity.Linear;
            averageVelocity *= 10.0f;

            averageSeparation /= neighbourCount;
            averageSeparation *= 10.0f;

            float3 separation = averageSeparation;
            float3 alignment = averageVelocity;
            float3 cohesion = averagePos;

            float3 moveForce = separation + alignment + cohesion;
            velocity.Linear += moveForce;

            velocity.Linear.y = 0.0f;
            //ComponentExtensions.ApplyLinearImpulse(ref velocity, mass, moveForce);
        });        
    }

    bool GetNeighbours(CollisionWorld collisionWorld, float3 entityPos, out NativeList<int> neighbours, float viewDst)
    {
        OverlapAabbInput aabbQuery = new OverlapAabbInput
        {
            Aabb = new Aabb { Min = entityPos - viewDst, Max = entityPos + viewDst },
            Filter = new CollisionFilter
            {
                BelongsTo = ~0u, // all 1s, so all layers, collide with everything 
                CollidesWith = ~0u,
                GroupIndex = 0
            }
        };

        neighbours = new NativeList<int>(Allocator.Temp);
        return collisionWorld.OverlapAabb(aabbQuery, ref neighbours);
    }

    bool CanSeeNeighbour(float3 entityPos, float3 neighbourPos, float viewDst)
    {
        float3 deltaPos = entityPos - neighbourPos;
        return math.lengthsq(deltaPos) <= viewDst * viewDst;
    }
}

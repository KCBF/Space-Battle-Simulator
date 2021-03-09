using System.Collections.Generic;
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
        float deltaTime = Time.DeltaTime;

        Entities
            .ForEach((Entity entity,
                ref Translation translation, ref Rotation rot,
                ref PhysicsVelocity velocity, ref PhysicsMass mass,
                ref BoidComponent boid) =>
            {
                if (boid.SettingsEntity == Entity.Null)
                    return;

                BoidSettingsComponent settings = EntityManager.GetComponentData<BoidSettingsComponent>(boid.SettingsEntity);
                float3 forward = math.forward(rot.Value);
                float3 up = math.rotate(rot.Value, math.up());

                float3 sumNeighbourPos;
                float3 sumNeighbourVelocity;
                float3 sumNeighbourNormalDelta;

                int neighbourCount = GetNeighbourSumData(
                    entity, translation.Value, forward, settings,
                    out sumNeighbourPos, out sumNeighbourVelocity, out sumNeighbourNormalDelta
                );

                float invsNeighbourCount = (neighbourCount > 1) ? 1.0f / neighbourCount : 1.0f;

                float3 averagePos = sumNeighbourPos * invsNeighbourCount;
                float3 cohesionForce = averagePos - translation.Value;

                //cohesionForce = SmoothDamp(forward * settings.MoveSpeed * Time.DeltaTime, cohesionForce, ref vel, 10.5f, math.INFINITY, deltaTime);
                cohesionForce *= settings.CohesionScalar;

                float3 averageVelocity = sumNeighbourVelocity * invsNeighbourCount;
                float3 alignmentForce = math.normalizesafe(averageVelocity) * settings.MoveSpeed;
                alignmentForce -= velocity.Linear;
                alignmentForce *= settings.AlignmentScalar;

                float3 averageSeparation = sumNeighbourNormalDelta * invsNeighbourCount;
                float3 separationForce = averageSeparation;
                separationForce *= settings.SeparationScalar;

                float3 moveForce = cohesionForce + alignmentForce + separationForce;
                
                velocity.Linear += moveForce;

                float3 lookDir = math.normalizesafe(velocity.Linear);
                quaternion lookRot = quaternion.LookRotationSafe(lookDir, up);
                rot.Value = math.slerp(rot.Value, lookRot, Time.DeltaTime * settings.LookSpeed);

                /*float3 forwardForce = forward * settings.MoveSpeed * Time.DeltaTime;
                ComponentExtensions.ApplyLinearImpulse(ref velocity, mass, forwardForce);*/
                velocity.Linear.y = 0.0f;
            });
    }

    int GetNeighbourSumData(Entity entity, float3 entityPos, float3 entityForward, in BoidSettingsComponent boidSetttings, 
        out float3 sumNeighbourPos, out float3 sumNeighbourVelocity, out float3 sumNeighbourNormalDelta)
    {
        NativeList<int> neighbours = new NativeList<int>(Allocator.Temp);
        GetBroadNeighbours(collisionWorld, entityPos, out neighbours, boidSetttings.ViewDst);

        int neighbourCount = 0;
        sumNeighbourPos = float3.zero;
        sumNeighbourVelocity = float3.zero;
        sumNeighbourNormalDelta = float3.zero;

        foreach (int neighbourIdx in neighbours)
        {
            RigidBody rigidBody = collisionWorld.Bodies[neighbourIdx];

            Entity neighbourEntity = rigidBody.Entity;
            if (neighbourEntity == entity)
                continue;

            // filter....

            RigidTransform neighbourTransform = rigidBody.WorldFromBody;

            if (!CanSeeNeighbour(entityPos, entityForward, neighbourTransform.pos, boidSetttings.ViewDst, boidSetttings.ViewAngle))
                continue;

            PhysicsVelocity neighbourVelocity = EntityManager.GetComponentData<PhysicsVelocity>(neighbourEntity);

            bool isBoid = EntityManager.HasComponent<BoidComponent>(entity);
            ++neighbourCount;

            if (isBoid)
            {
                sumNeighbourPos += neighbourTransform.pos;
                sumNeighbourVelocity += neighbourVelocity.Linear;
                sumNeighbourNormalDelta += math.normalize(entityPos - neighbourTransform.pos);
            }
            else
            {

            }
        }

        return neighbourCount;
    }

    bool GetBroadNeighbours(CollisionWorld collisionWorld, float3 entityPos, out NativeList<int> neighbours, float viewDst)
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

    bool CanSeeNeighbour(float3 entityPos, float3 entityForward, float3 neighbourPos, float viewDst, float viewAngle)
    {
        float3 deltaPos = entityPos - neighbourPos;
        bool canSee = math.lengthsq(deltaPos) <= viewDst * viewDst;

        Vector3 dirToNeighbour = Vector3.Normalize(deltaPos);
        float deltaAngle = Vector3.Angle(entityForward, dirToNeighbour);

        canSee &= deltaAngle <= viewAngle * 0.5f;
        return canSee;
    }

    public static Vector3 SmoothDamp(float3 current, float3 target, ref float3 currentVelocity, float smoothTime, float maxSpeed, float deltaTime)
    {
        Vector3 currentVelocityF3 = currentVelocity;
        Vector3 value = Vector3.SmoothDamp(current, target, ref currentVelocityF3, smoothTime, maxSpeed, deltaTime);
        
        currentVelocity = currentVelocityF3;
        return value;
    }
}

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

        Entities
            .ForEach((Entity entity,
                ref Translation translation, ref Rotation rot,
                ref PhysicsVelocity velocity, ref BoidComponent boid) =>
            {
                if (boid.SettingsEntity == Entity.Null)
                    return;
                BoidSettingsComponent settings = EntityManager.GetComponentData<BoidSettingsComponent>(boid.SettingsEntity);

                float3 forward = math.forward(rot.Value);
                float3 up = math.rotate(rot.Value, math.up());

                float3 moveForce = float3.zero;
                
                moveForce += GetMapConstraintForce(translation.Value, settings);
                moveForce += GetObstacleMoveForce(entity, translation.Value, forward, boid, settings);

                if (math.all(moveForce == float3.zero))
                {
                    moveForce = GetBoidMoveForce(entity, translation.Value, forward, ref up, velocity.Linear, boid, settings);
                    moveForce += GetBoidLineOfSightForce(entity, translation.Value, forward, up, boid, settings);
                }

                float3 forwardForce = forward * settings.MoveSpeed;

                boid.moveForce = moveForce + forwardForce;
                boid.targetUp = up;
            });

        Entities
            .ForEach((Entity entity,
                ref Translation translation, ref Rotation rot,
                ref PhysicsVelocity velocity, ref BoidComponent boid) =>
            {
                if (boid.SettingsEntity == Entity.Null)
                    return;
                BoidSettingsComponent settings = EntityManager.GetComponentData<BoidSettingsComponent>(boid.SettingsEntity);

                velocity.Linear += boid.moveForce * Time.DeltaTime;

                float clampedSpeed = math.min(math.length(velocity.Linear), settings.MaxMoveSpeed);
                velocity.Linear = math.normalize(velocity.Linear) * clampedSpeed;

                float3 lookDir = math.normalizesafe(velocity.Linear);
                quaternion lookRot = quaternion.LookRotationSafe(lookDir, boid.targetUp);
                rot.Value = math.slerp(rot.Value, lookRot, settings.LookSpeed * Time.DeltaTime);
            });
    }

    float3 GetMapConstraintForce(float3 entityPos, BoidSettingsComponent settings)
    {
        float3 deltaPos = settings.MapCentre - entityPos;
        float deltaLen = math.length(deltaPos);

        if (deltaLen < settings.MapRadius)
            return float3.zero;

        float3 moveDir = math.normalize(deltaPos);
        return moveDir * (deltaLen - settings.MapRadius) * settings.MapRadiusWeight;
    }

    float3 GetObstacleMoveForce(Entity entity, float3 boidPos, float3 boidForward, BoidComponent boid, in BoidSettingsComponent settings)
    {
        PhysicsCategoryTags belongsTo = new PhysicsCategoryTags { Category01 = true };
        RaycastInput rayCommand = new RaycastInput()
        {
            Start = boidPos,
            End = boidPos + boidForward * settings.ObstacleViewDst,
            Filter = new CollisionFilter()
            {
                BelongsTo = belongsTo.Value,
                CollidesWith = ~0u,
                GroupIndex = 0
            }
        };

        NativeList<Unity.Physics.RaycastHit> raycastHits = new NativeList<Unity.Physics.RaycastHit>(Allocator.Temp);
        if (!collisionWorld.CastRay(rayCommand, ref raycastHits))
            return float3.zero;

        Unity.Physics.RaycastHit raycastResult = new Unity.Physics.RaycastHit();

        foreach (Unity.Physics.RaycastHit raycastHit in raycastHits)
        {
            if (raycastHit.Entity == entity)
                continue;

            if (EntityManager.HasComponent<BoidComponent>(raycastHit.Entity))
                continue;

            raycastResult = raycastHit;
            break;
        }

        if (raycastResult.Entity == entity || raycastResult.Entity == Entity.Null)
            return float3.zero;
        
        float deltaLen = math.length(boidPos - raycastResult.Position);
        float overlapLen = settings.ObstacleViewDst - deltaLen;

        float3 avoidanceForce = raycastResult.SurfaceNormal * overlapLen * settings.ObstacleAvoidWeight;
        return avoidanceForce;
    }

    float3 GetBoidMoveForce(Entity entity, float3 boidPos, float3 boidForward, ref float3 boidUp, float3 boidLinearVelocity, BoidComponent boid, in BoidSettingsComponent settings)
    {
        PhysicsCategoryTags belongsTo = new PhysicsCategoryTags { Category00 = true };
        NativeList<int> broadNeighbours;
        GetBroadNeighbours(
            collisionWorld, boidPos, settings.BoidDetectRadius, out broadNeighbours,
            new CollisionFilter
            {
                BelongsTo = belongsTo.Value,
                CollidesWith = ~0u,
                GroupIndex = 0
            }
        );

        float3 sumNeighbourPos;
        float3 sumNeighbourVelocity;
        float3 sumNeighbourNormalDelta;
        float3 sumUpDir;
        int neighbourCount = GetBoidNeighbourSumData(broadNeighbours, 
            entity, boidPos, boidForward, boid, settings,
            out sumNeighbourPos, out sumNeighbourVelocity, out sumNeighbourNormalDelta, out sumUpDir
        );

        if (neighbourCount == 0)
            return float3.zero;

        float invsNeighbourCount = 1.0f / neighbourCount;

        float3 averagePos = sumNeighbourPos * invsNeighbourCount;
        float3 cohesionForce = averagePos - boidPos;
        cohesionForce *= settings.CohesionWeight;

        float3 averageVelocity = sumNeighbourVelocity * invsNeighbourCount;
        float3 alignmentForce = averageVelocity - boidLinearVelocity;
        alignmentForce *= settings.AlignmentWeight;

        float3 averageSeparation = sumNeighbourNormalDelta * invsNeighbourCount;
        float3 separationForce = averageSeparation;
        separationForce *= settings.SeparationWeight;

        boidUp = sumUpDir * invsNeighbourCount;

        return cohesionForce + alignmentForce + separationForce;
    }

    bool IfBoidIsFriendlyNeighbour(Entity entity, float3 boidPos, float3 boidForward, BoidComponent boid,
        RigidBody neighbourRigid, float viewDst, float viewAngle)
    {
        if (neighbourRigid.Entity == entity)
            return false;

        if (EntityManager.HasComponent<BoidComponent>(neighbourRigid.Entity))
        {
            BoidComponent neighbourBoid = EntityManager.GetComponentData<BoidComponent>(neighbourRigid.Entity);
            if (neighbourBoid.GroupID != boid.GroupID)
                return false;
        }

        RigidTransform neighbourTransform = neighbourRigid.WorldFromBody;
        if (!CanSeeNeighbour(boidPos, boidForward, neighbourTransform.pos, viewDst, viewAngle))
            return false;

        return true;
    }

    int GetBoidNeighbourSumData(in NativeList<int> broadNeighbours,
        Entity entity, float3 boidPos, float3 boidForward, BoidComponent boid, in BoidSettingsComponent settings, 
        out float3 sumNeighbourPos, out float3 sumNeighbourVelocity, out float3 sumNeighbourNormalDelta, out float3 sumUpDir)
    {
        int neighbourCount = 0;
        sumNeighbourPos = float3.zero;
        sumNeighbourVelocity = float3.zero;
        sumNeighbourNormalDelta = float3.zero;
        sumUpDir = float3.zero;

        foreach (int neighbourIdx in broadNeighbours)
        {
            RigidBody neighbourRigid = collisionWorld.Bodies[neighbourIdx];

            if (!IfBoidIsFriendlyNeighbour(entity, boidPos, boidForward, boid, neighbourRigid, settings.BoidDetectRadius, settings.BoidDetectFOV))
                continue;

            RigidTransform neighbourTransform = neighbourRigid.WorldFromBody;
            PhysicsVelocity neighbourVelocity = EntityManager.GetComponentData<PhysicsVelocity>(neighbourRigid.Entity);
            ++neighbourCount;

            sumNeighbourPos += neighbourTransform.pos;
            sumNeighbourVelocity += neighbourVelocity.Linear;
            sumNeighbourNormalDelta += math.normalize(boidPos - neighbourTransform.pos);
            sumUpDir += math.rotate(neighbourTransform.rot, math.up());
        }

        return neighbourCount;
    }

    float3 GetBoidLineOfSightForce(Entity entity, float3 boidPos, float3 boidForward, float3 boidUp, BoidComponent boid, in BoidSettingsComponent settings)
    {
        PhysicsCategoryTags belongsTo = new PhysicsCategoryTags { Category00 = true };
        NativeList<int> broadNeighbours;
        GetBroadNeighbours(
            collisionWorld, boidPos, settings.FiringViewDst, out broadNeighbours,
            new CollisionFilter
            {
                BelongsTo = belongsTo.Value,
                CollidesWith = ~0u,
                GroupIndex = 0
            }
        );

        float3 boidRight = math.cross(boidForward, boidUp);
        float3 sumMove = float3.zero;

        foreach (int neighbourIdx in broadNeighbours)
        {
            RigidBody neighbourRigid = collisionWorld.Bodies[neighbourIdx];

            if (!IfBoidIsFriendlyNeighbour(entity, boidPos, boidForward, boid, neighbourRigid, settings.FiringViewDst, settings.FiringFOV))
                continue;

            RigidTransform neighbourTransform = neighbourRigid.WorldFromBody;

            float3 delta = math.normalize(neighbourTransform.pos - boidPos);
            float3 steerDir = math.cross(delta, boidRight);

            float scalar = math.sign(math.dot(boidUp, delta));
            scalar = (scalar == 0.0f) ? 1.0f : scalar;

            sumMove += steerDir * scalar;
        }

        return sumMove * settings.LineOfSightWeight;
    }

    bool GetBroadNeighbours(CollisionWorld collisionWorld, float3 centre, float radius, out NativeList<int> neighbours, in CollisionFilter collisionFilter)
    {
        OverlapAabbInput aabbQuery = new OverlapAabbInput
        {
            Aabb = new Aabb { Min = centre - radius, Max = centre + radius },
            Filter = collisionFilter
        };

        neighbours = new NativeList<int>(Allocator.Temp);
        return collisionWorld.OverlapAabb(aabbQuery, ref neighbours);
    }

    bool CanSeeNeighbour(float3 entityPos, float3 entityForward, float3 neighbourPos, float viewDst, float viewAngle)
    {
        float3 deltaPos = entityPos - neighbourPos;
        if (math.lengthsq(deltaPos) > viewDst * viewDst)
            return false;

        Vector3 dirToNeighbour = Vector3.Normalize(deltaPos);
        float deltaAngle = Vector3.Angle(entityForward, dirToNeighbour);

        return deltaAngle <= viewAngle * 0.5f;
    }
}

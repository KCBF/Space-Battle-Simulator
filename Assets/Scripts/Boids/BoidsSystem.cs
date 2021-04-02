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
        random = new Unity.Mathematics.Random(1);
    }

    protected override void OnUpdate()
    {
        collisionWorld = physicsWorldSystem.PhysicsWorld.CollisionWorld;
        
        Entities
            .ForEach((Entity entity,
                ref Translation translation, ref Rotation rot,
                ref PhysicsVelocity velocity, ref BoidComponent boid) =>
            {
                if (boid.HP <= 0.0f || boid.SettingsEntity == Entity.Null)
                    return;
                BoidSettingsComponent settings = EntityManager.GetComponentData<BoidSettingsComponent>(boid.SettingsEntity);

                float3 forward = math.forward(rot.Value);
                float3 up = math.rotate(rot.Value, math.up());

                float3 moveForce = GetObstacleMoveForce(entity, translation.Value, forward, boid, settings);
                float3 lineOfSightForce = float3.zero;

                if (math.all(moveForce == float3.zero))
                {
                    moveForce = GetBoidMoveForce(entity, translation.Value, forward, ref up, velocity.Linear, boid, settings);
                    moveForce += GetMapConstraintForce(translation.Value, settings);

                    lineOfSightForce = GetBoidLineOfSightForce(entity, translation.Value, forward, up, boid, settings);
                }

                float3 forwardForce = forward * settings.MoveSpeed;

                boid.MoveForce = moveForce + forwardForce;
                boid.TargetUp = up;
                boid.LineOfSightForce = lineOfSightForce;
                boid.pos = translation.Value;
                Debug.DrawLine(translation.Value, translation.Value + forward * 10.0f, Color.yellow);
            });

        Entities
            .ForEach((Entity entity,
                ref Translation translation, ref Rotation rot,
                ref PhysicsVelocity velocity, ref BoidComponent boid) =>
            {
                if (boid.HP <= 0.0f || boid.SettingsEntity == Entity.Null)
                    return;
                BoidSettingsComponent settings = EntityManager.GetComponentData<BoidSettingsComponent>(boid.SettingsEntity);

                velocity.Linear += boid.MoveForce * Time.DeltaTime;
                velocity.Linear += boid.LineOfSightForce * Time.DeltaTime;

                float clampedSpeed = math.min(math.length(velocity.Linear), settings.MaxMoveSpeed);
                velocity.Linear = math.normalize(velocity.Linear) * clampedSpeed;

                float3 lookDir = math.normalizesafe(velocity.Linear);
                quaternion lookRot = quaternion.LookRotationSafe(lookDir, boid.TargetUp);
                rot.Value = math.slerp(rot.Value, lookRot, settings.LookSpeed * Time.DeltaTime);

                ShootEnemy(entity, translation.Value, rot.Value, ref boid, settings);
            });
    }

    public static float3 GetTargetLeadPos(float3 origin, float3 targetPos, float3 targetVelocity, float projectileSpeed, float corrector)
    {
        if (projectileSpeed == 0.0f || math.lengthsq(targetVelocity) <= 0.0f)
            return targetPos;

        // The longer the distance the more the lead, the faster the intermediarySpeed the less the lead is.
        float deltaLen = math.distance(targetPos, origin);
        float scalar = deltaLen / projectileSpeed;

        float3 lead = targetVelocity * scalar * corrector;
        return targetPos + lead;
    }

    void ShootEnemy(Entity entity, float3 boidPos, quaternion boidRot, ref BoidComponent boid, in BoidSettingsComponent settings)
    {
        if (boid.NextAllowShootTime >= Time.ElapsedTime)
            return;

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

        float3 boidForward = math.forward(boidRot);
        Entity targetEntity = Entity.Null;
        float3 targetPos = float3.zero;
        float closestDst = float.MaxValue;

        foreach (int neighbourIdx in broadNeighbours)
        {
            RigidBody neighbourRigid = collisionWorld.Bodies[neighbourIdx];

            if (!CanSeeBoidNeighbour(entity, boidPos, boidForward, boid, neighbourRigid, settings.FiringViewDst, settings.FiringFOV, false))
                continue;

            RigidTransform neighbourTransform = neighbourRigid.WorldFromBody;
            float deltaLen = math.lengthsq(targetPos - boidPos);

            if (deltaLen < closestDst)
            {
                targetEntity = neighbourRigid.Entity;
                targetPos = neighbourTransform.pos;
                closestDst = deltaLen;
            }
        }
        
        if (targetEntity == Entity.Null)
            return;
        Debug.DrawLine(boidPos, boidPos + boidForward * 10.0f, Color.cyan);

        Entity projectileEntity = EntityManager.Instantiate(settings.BulletEntity);
        ProjectileComponent projectile = EntityManager.GetComponentData<ProjectileComponent>(projectileEntity);
        projectile.OwnerEntity = entity;
        EntityManager.SetComponentData(projectileEntity, projectile);

        PhysicsVelocity projectileVelocity = EntityManager.GetComponentData<PhysicsVelocity>(projectileEntity);
        float3 leadPos = GetTargetLeadPos(boidPos, targetPos, projectileVelocity.Linear, projectile.Speed, 1.0f);

        float3 lookDir = math.normalize(leadPos - boidPos);
        quaternion lookRot = quaternion.LookRotation(lookDir, math.rotate(boidRot, math.up()));
        EntityManager.SetComponentData(projectileEntity, new Rotation { Value = lookRot });
        
        float3 spawnPos = boidPos + math.rotate(boidRot, settings.ShootOffSet);
        EntityManager.SetComponentData(projectileEntity, new Translation { Value = spawnPos });

        boid.NextAllowShootTime = (float)Time.ElapsedTime + settings.ShootRate;

        Debug.DrawLine(spawnPos, spawnPos + lookDir * 10.0f, Color.red);
        
        Debug.DrawLine(boidPos, targetPos, Color.white);
        Debug.DrawLine(boidPos, boidPos + lookDir * 10.0f, Color.magenta);

        // TODO target leading...
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
        RaycastInput rayCommand = new RaycastInput()
        {
            Start = boidPos,
            End = boidPos + boidForward * settings.ObstacleViewDst,
            Filter = new CollisionFilter()
            {
                BelongsTo = ~0u,
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

            if (EntityManager.HasComponent<BoidComponent>(raycastHit.Entity) && 
                math.length(raycastHit.Position - boidPos) <= settings.BoidDetectRadius)
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
        float3 alignmentForce = math.normalize(averageVelocity - boidLinearVelocity);
        alignmentForce *= settings.AlignmentWeight;

        float3 averageSeparation = sumNeighbourNormalDelta * invsNeighbourCount;
        float3 separationForce = averageSeparation;
        separationForce *= settings.SeparationWeight;

        boidUp = sumUpDir * invsNeighbourCount;

        return cohesionForce + alignmentForce + separationForce;
    }

    bool CanSeeBoidNeighbour(Entity entity, float3 boidPos, float3 boidForward, BoidComponent boid,
        RigidBody neighbourRigid, float viewDst, float viewAngle, bool seeFriendly)
    {
        if (neighbourRigid.Entity == entity)
            return false;

        if (!EntityManager.HasComponent<BoidComponent>(neighbourRigid.Entity))
            return false;
        
        BoidComponent neighbourBoid = EntityManager.GetComponentData<BoidComponent>(neighbourRigid.Entity);

        if (neighbourBoid.HP <= 0.0f)
            return false;

        if (seeFriendly && neighbourBoid.GroupID != boid.GroupID)
            return false;

        else if (!seeFriendly && neighbourBoid.GroupID == boid.GroupID)
            return false;

        RigidTransform neighbourTransform = neighbourRigid.WorldFromBody;
        return CanSeeNeighbour(boidPos, boidForward, neighbourTransform.pos, viewDst, viewAngle);
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

            if (!CanSeeBoidNeighbour(entity, boidPos, boidForward, boid, neighbourRigid, settings.BoidDetectRadius, settings.BoidDetectFOV, true))
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
            
            if (!CanSeeBoidNeighbour(entity, boidPos, boidForward, boid, neighbourRigid, settings.FiringViewDst, settings.FiringFOV, true))
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
        float3 deltaPos = neighbourPos - entityPos;
        if (math.lengthsq(deltaPos) > viewDst * viewDst)
            return false;

        float3 dirToNeighbour = math.normalize(deltaPos);
        float deltaAngle = Vector3.Angle(entityForward, dirToNeighbour);
        
        return deltaAngle <= viewAngle * 0.5f;
    }
}

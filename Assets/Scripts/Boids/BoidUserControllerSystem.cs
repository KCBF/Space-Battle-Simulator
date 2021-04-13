using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;
using Unity.Collections;
using Unity.Physics;

public class BoidUserControllerSystem : ComponentSystem
{
    EntityQuery boidQuery;

    Unity.Physics.Systems.BuildPhysicsWorld physicsWorldSystem;
    CollisionWorld collisionWorld;

    protected override void OnCreate()
    {
        boidQuery = EntityManager.CreateEntityQuery(typeof(BoidComponent));
        physicsWorldSystem = World.GetOrCreateSystem<Unity.Physics.Systems.BuildPhysicsWorld>();
    }

    protected override void OnUpdate()
    {
        collisionWorld = physicsWorldSystem.PhysicsWorld.CollisionWorld;
        BoidControllerComponent boidControllerComponent = GetSingleton<BoidControllerComponent>();

        if (Input.GetKeyDown(KeyCode.Tab))
            boidControllerComponent.Manual = !boidControllerComponent.Manual;

        if (boidControllerComponent.Manual)
        {
            // MOVE
            Shoot(boidControllerComponent);
        }

        uint oldSelectedGroup = boidControllerComponent.SelectedGroup;
        SelectBoidGroup(ref boidControllerComponent);
        SetSingleton(boidControllerComponent);

        bool nextBoid = Input.GetKeyDown(KeyCode.X);
        bool prevBoid = Input.GetKeyDown(KeyCode.Z);
        if (!nextBoid && !prevBoid && oldSelectedGroup == boidControllerComponent.SelectedGroup)
            return;

        int boidCount = boidQuery.CalculateEntityCount();
        NativeArray<BoidComponent> boids = new NativeArray<BoidComponent>(boidCount, Allocator.Temp);
        NativeArray<Entity> boidEntities = new NativeArray<Entity>(boidCount, Allocator.Temp);

        int boidIdx = 0;
        int selectedBoidIdx = 0;

        Entities
            .ForEach((Entity entity, ref BoidComponent boid) =>
            {
                boids[boidIdx] = boid;
                boidEntities[boidIdx] = entity;

                if (entity == boidControllerComponent.BoidEntity)
                    selectedBoidIdx = boidIdx;
                ++boidIdx;
            });

        SelectNewBoid(boids, boidEntities, selectedBoidIdx, ref boidControllerComponent);
        SetSingleton(boidControllerComponent);
    }
    
    void SelectBoidGroup(ref BoidControllerComponent boidControllerComponent)
    {
        if (Input.GetKeyDown(KeyCode.Alpha1))
            boidControllerComponent.SelectedGroup = 1;
        else if (Input.GetKeyDown(KeyCode.Alpha2))
            boidControllerComponent.SelectedGroup = 2;
    }

    void SelectNewBoid(NativeArray<BoidComponent> boids, NativeArray<Entity> boidEntities, int selectedBoidIdx,
        ref BoidControllerComponent boidControllerComponent)
    {
        bool nextBoid = Input.GetKeyDown(KeyCode.X);
        int boidCount = boidQuery.CalculateEntityCount();

        int i = (nextBoid) ? selectedBoidIdx + 1 : selectedBoidIdx - 1;
        if (i >= boidCount)
            i = 0;
        else if (i < 0)
            i = boidCount - 1;

        while (i != selectedBoidIdx)
        {
            if (boids[i].GroupID == boidControllerComponent.SelectedGroup)
            {
                boidControllerComponent.BoidEntity = boidEntities[i];
                break;
            }

            if (nextBoid)
                ++i;
            else
                --i;

            if (i >= boidCount)
                i = 0;
            else if (i < 0)
                i = boidCount - 1;
        }
    }

    void Shoot(BoidControllerComponent boidControllerComponent)
    {
        if (boidControllerComponent.BoidEntity == Entity.Null || !Input.GetKeyDown(KeyCode.Space))
            return;

        BoidComponent boid = EntityManager.GetComponentData<BoidComponent>(boidControllerComponent.BoidEntity);
        if (boid.NextAllowShootTime > Time.ElapsedTime)
            return;

        BoidSettingsComponent settings = EntityManager.GetComponentData<BoidSettingsComponent>(boid.SettingsEntity);
        Translation translation = EntityManager.GetComponentData<Translation>(boidControllerComponent.BoidEntity);
        Rotation rot = EntityManager.GetComponentData<Rotation>(boidControllerComponent.BoidEntity);

        Entity projectileEntity = EntityManager.Instantiate(settings.MissleEntity);

        ProjectileComponent projectile = EntityManager.GetComponentData<ProjectileComponent>(projectileEntity);
        projectile.OwnerEntity = boidControllerComponent.BoidEntity;
        EntityManager.SetComponentData(projectileEntity, projectile);

        float3 spawnPos = translation.Value + math.rotate(rot.Value, settings.ShootOffSet);
        EntityManager.SetComponentData(projectileEntity, new Translation { Value = spawnPos });

        boid.NextAllowShootTime = (float)Time.ElapsedTime + settings.ShootRate;
        EntityManager.SetComponentData(boidControllerComponent.BoidEntity, boid);

        float3 lookDir = GetShootDir(boidControllerComponent.BoidEntity, spawnPos);
        quaternion lookRot = quaternion.LookRotation(lookDir, math.rotate(rot.Value, math.up()));
        EntityManager.SetComponentData(projectileEntity, new Rotation { Value = lookRot });
    }

    float3 GetShootDir(Entity entity, float3 spawnPos)
    {
        UnityEngine.Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        const float shootDst = 100.0f;

        RaycastInput raycastInput = new RaycastInput { Start = ray.origin, End = ray.origin + ray.direction * shootDst };
        NativeList<Unity.Physics.RaycastHit> raycastHits = new NativeList<Unity.Physics.RaycastHit>(Allocator.Temp);

        collisionWorld.CastRay(raycastInput, ref raycastHits);

        Entity targetEntity = Entity.Null;
        float3 targetPos = float3.zero;
        float closestDst = float.MaxValue;

        foreach (Unity.Physics.RaycastHit raycastHit in raycastHits)
        {
            RigidBody neighbourRigid = collisionWorld.Bodies[raycastHit.RigidBodyIndex];

            if (neighbourRigid.Entity == entity)
                continue;

            RigidTransform neighbourTransform = neighbourRigid.WorldFromBody;
            float deltaLen = math.lengthsq(raycastHit.Position - spawnPos);

            if (deltaLen < closestDst)
            {
                targetEntity = neighbourRigid.Entity;
                targetPos = neighbourTransform.pos;
                closestDst = deltaLen;
            }
        }

        if (targetEntity == Entity.Null)
            targetPos = raycastInput.End;

        return math.normalize(targetPos - spawnPos);
    }
}

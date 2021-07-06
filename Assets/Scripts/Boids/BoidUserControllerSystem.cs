// Author: Peter Richards.
using UnityEngine;
using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;
using Unity.Collections;
using Unity.Physics;

[UpdateBefore(typeof(BoidsSystem))]
public class BoidUserControllerSystem : ComponentSystem
{
    EntityQuery boidQuery;

    Unity.Physics.Systems.BuildPhysicsWorld physicsWorldSystem;
    CollisionWorld collisionWorld;

    protected override void OnCreate()
    {
        boidQuery = EntityManager.CreateEntityQuery(typeof(BoidComponent));
        physicsWorldSystem = World.GetOrCreateSystem<Unity.Physics.Systems.BuildPhysicsWorld>();
        RequireSingletonForUpdate<BoidUserControllerComponent>();
    }

    protected override void OnUpdate()
    {
        collisionWorld = physicsWorldSystem.PhysicsWorld.CollisionWorld;
        BoidUserControllerComponent boidControllerComponent = GetSingleton<BoidUserControllerComponent>();

        if (Input.GetKeyDown(KeyCode.Tab))
            boidControllerComponent.Manual = !boidControllerComponent.Manual;

        if (boidControllerComponent.Manual)
        {
            Move(boidControllerComponent);
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

        // Get all boids as an array.
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
    
    void SelectBoidGroup(ref BoidUserControllerComponent boidControllerComponent)
    {
        if (Input.GetKeyDown(KeyCode.Alpha1))
            boidControllerComponent.SelectedGroup = 1;
        else if (Input.GetKeyDown(KeyCode.Alpha2))
            boidControllerComponent.SelectedGroup = 2;
        else if (Input.GetKeyDown(KeyCode.Alpha3))
            boidControllerComponent.SelectedGroup = 3;
    }

    void SelectNewBoid(NativeArray<BoidComponent> boids, NativeArray<Entity> boidEntities, int selectedBoidIdx,
        ref BoidUserControllerComponent boidControllerComponent)
    {
        bool nextBoid = Input.GetKeyDown(KeyCode.X);
        int boidCount = boidQuery.CalculateEntityCount();

        // Iterate through boids arrays forwards or backwards to get the next boid in the selected boid group.
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

    void Shoot(BoidUserControllerComponent boidControllerComponent)
    {
        if (boidControllerComponent.BoidEntity == Entity.Null || !Input.GetKeyDown(KeyCode.Mouse0))
            return;

        BoidComponent boid = EntityManager.GetComponentData<BoidComponent>(boidControllerComponent.BoidEntity);
        if (boid.NextAllowShootTime > Time.ElapsedTime)
            return;

        BoidSettingsComponent settings = EntityManager.GetComponentData<BoidSettingsComponent>(boid.SettingsEntity);
        Translation translation = EntityManager.GetComponentData<Translation>(boidControllerComponent.BoidEntity);
        Rotation rot = EntityManager.GetComponentData<Rotation>(boidControllerComponent.BoidEntity);

        BoidsSystem boidsSystem = World.DefaultGameObjectInjectionWorld.GetExistingSystem<BoidsSystem>();
        ProjectileComponent projectile;
        float3 spawnPos;

        Entity projectileEntity = boidsSystem.ShootMissle(
            boidControllerComponent.BoidEntity, translation.Value, rot.Value, 
            ref boid, settings, out projectile, out spawnPos);

        float3 lookDir = GetShootDir(boidControllerComponent.BoidEntity, spawnPos);
        quaternion lookRot = quaternion.LookRotation(lookDir, math.rotate(rot.Value, math.up()));
        EntityManager.SetComponentData(projectileEntity, new Rotation { Value = lookRot });

        EntityManager.SetComponentData(boidControllerComponent.BoidEntity, boid);
    }

    float3 GetShootDir(Entity entity, float3 spawnPos)
    {
        UnityEngine.Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        const float shootDst = 1000.0f;

        RaycastInput raycastInput = new RaycastInput { 
            Start = ray.origin, End = ray.origin + ray.direction * shootDst, 
            Filter = CollisionFilter.Default 
        };
        
        NativeList<Unity.Physics.RaycastHit> raycastHits = new NativeList<Unity.Physics.RaycastHit>(Allocator.Temp);
        collisionWorld.CastRay(raycastInput, ref raycastHits);

        Entity targetEntity = Entity.Null;
        float3 targetPos = float3.zero;
        float closestDst = float.MaxValue;

        // Get closest object hit by ScreenPointToRay.
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
                targetPos = raycastHit.Position;
                closestDst = deltaLen;
            }
        }

        if (targetEntity == Entity.Null)
            targetPos = raycastInput.End;
        
        return math.normalize(targetPos - spawnPos);
    }

    void Move(BoidUserControllerComponent boidControllerComponent)
    {
        if (boidControllerComponent.BoidEntity == Entity.Null)
            return;

        BoidComponent boid = EntityManager.GetComponentData<BoidComponent>(boidControllerComponent.BoidEntity);
        BoidSettingsComponent settings = EntityManager.GetComponentData<BoidSettingsComponent>(boid.SettingsEntity);
        LocalToWorld localToWorld = EntityManager.GetComponentData<LocalToWorld>(boidControllerComponent.BoidEntity);

        boid.MoveForce = float3.zero;

        boid.MoveForce += GetMove("Horizontal", settings.MoveSpeed, settings.MaxMoveSpeed, localToWorld.Right);
        boid.MoveForce += GetMove("Hover", settings.MoveSpeed, settings.MaxMoveSpeed, localToWorld.Up);
        boid.MoveForce += GetMove("Vertical", settings.MoveSpeed, settings.MaxMoveSpeed, localToWorld.Forward);

        float rollAxis = Input.GetAxis("Roll");
        if (rollAxis != 0.0f)
        {
            float rollPercentage = math.abs(rollAxis);
            float speed = settings.LookSpeed * 0.5f * rollPercentage;
            boid.TargetUp = math.rotate(quaternion.AxisAngle(-localToWorld.Forward, speed * rollAxis), localToWorld.Up);
        }

        EntityManager.SetComponentData(boidControllerComponent.BoidEntity, boid);
    }

    float3 GetMove(string axisName, float minSpeed, float maxSpeed, float3 dir)
    {
        float axis = Input.GetAxis(axisName);

        if (axis == 0.0f)
            return float3.zero;

        float speedPercentage = math.abs(axis);
        float speed = math.lerp(minSpeed, maxSpeed, speedPercentage);
        return speed * dir * Input.GetAxis(axisName);
    }
}

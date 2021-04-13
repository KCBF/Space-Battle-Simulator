using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Physics;
using Unity.Physics.Systems;
using UnityEngine;

[UpdateBefore(typeof(BoidsSystem))]
public class ProjectileCollisionSystem : JobComponentSystem
{
    BuildPhysicsWorld buildPhysicsWorld;
    StepPhysicsWorld stepPhysicsWorld;

    protected override void OnCreate()
    {
        base.OnCreate();
        buildPhysicsWorld = World.GetOrCreateSystem<BuildPhysicsWorld>();
        stepPhysicsWorld = World.GetOrCreateSystem<StepPhysicsWorld>();
    }

    struct ProjectileCollisionSystemJob : ICollisionEventsJob
    {
        public ComponentDataFromEntity<ProjectileComponent> projectileGroup;
        public ComponentDataFromEntity<BoidComponent> boidGroup;

        public float ElapsedTime;

        void OnProjectileBoidCollisionEvent(Entity boidEntity, Entity projectileEntity)
        {
            ProjectileComponent projectile = projectileGroup[projectileEntity];
            BoidComponent boid = boidGroup[boidEntity];

            if (projectile.OwnerEntity == boidEntity)
                return;
            
            boid.HP -= projectile.Damage;
            boidGroup[boidEntity] = boid;

            projectile.DespawnTime = ElapsedTime;
            projectileGroup[projectileEntity] = projectile;
        }

        public void Execute(CollisionEvent collisionEvent)
        {
            Entity entityA = collisionEvent.EntityA;
            Entity entityB = collisionEvent.EntityB;

            bool entityAIsProjectile = projectileGroup.Exists(entityA);
            bool entityBIsProjectile = projectileGroup.Exists(entityB);
            
            bool entityAIsBoid = boidGroup.Exists(entityA);
            bool entityBIsBoid = boidGroup.Exists(entityB);

            if (entityAIsProjectile && entityBIsBoid)
                OnProjectileBoidCollisionEvent(entityB, entityA);
            else if (entityAIsBoid && entityBIsProjectile)
                OnProjectileBoidCollisionEvent(entityA, entityB);
        }
    }

    protected override JobHandle OnUpdate(JobHandle inputDependencies)
    {
        ProjectileCollisionSystemJob job = new ProjectileCollisionSystemJob();
        job.projectileGroup = GetComponentDataFromEntity<ProjectileComponent>(false);
        job.boidGroup = GetComponentDataFromEntity<BoidComponent>(false);
        job.ElapsedTime = (float)Time.ElapsedTime;
        
        JobHandle jobHandle = job.Schedule(stepPhysicsWorld.Simulation, ref buildPhysicsWorld.PhysicsWorld, inputDependencies);
        jobHandle.Complete();
        
        return jobHandle;
    }
}

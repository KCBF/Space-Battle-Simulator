// Author: Peter Richards.
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Physics;
using Unity.Physics.Systems;

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
        public ComponentDataFromEntity<BoidStationComponent> boidStationGroup;

        public float ElapsedTime;

        void OnProjectileBoidCollisionEvent(Entity boidEntity, Entity projectileEntity)
        {
            ProjectileComponent projectile = projectileGroup[projectileEntity];
            BoidComponent boid = boidGroup[boidEntity];

            if (projectile.OwnerEntity == boidEntity)
                return;
            
            boid.HP -= projectile.Damage;
            boid.HitTime = ElapsedTime + projectile.HitTime;

            if (boid.HP <= 0.0f && boid.DiedTime < ElapsedTime)
                boid.DiedTime = ElapsedTime;

            boidGroup[boidEntity] = boid;
        }

        void OnProjectileBoidStationCollisionEvent(Entity boidStationEntity, Entity projectileEntity)
        {
            ProjectileComponent projectile = projectileGroup[projectileEntity];
            BoidStationComponent boidStation = boidStationGroup[boidStationEntity];

            boidStation.HP -= projectile.Damage;
            boidStationGroup[boidStationEntity] = boidStation;
        }

        void OnDeinitProjectile(Entity projectileEntity)
        {
            ProjectileComponent projectile = projectileGroup[projectileEntity];
            projectile.DespawnTime = ElapsedTime;
            projectileGroup[projectileEntity] = projectile;
        }

        public void Execute(CollisionEvent collisionEvent)
        {
            Entity entityA = collisionEvent.EntityA;
            Entity entityB = collisionEvent.EntityB;

            bool entityAIsProjectile = projectileGroup.Exists(entityA);
            bool entityBIsProjectile = projectileGroup.Exists(entityB);

            SolveBoidVsProjectileCondition(entityA, entityB, entityAIsProjectile, entityBIsProjectile);
            SolveBoidStationVsProjectileCondition(entityA, entityB, entityAIsProjectile, entityBIsProjectile);

            if (entityAIsProjectile)
                OnDeinitProjectile(entityA);
            else if (entityBIsProjectile)
                OnDeinitProjectile(entityB);
        }

        void SolveBoidStationVsProjectileCondition(Entity entityA, Entity entityB, bool entityAIsProjectile, bool entityBIsProjectile)
        {
            bool entityAIsBoidStation = boidStationGroup.Exists(entityA);
            bool entityBIsBoidStation = boidStationGroup.Exists(entityB);

            if (entityAIsProjectile && entityBIsBoidStation)
                OnProjectileBoidStationCollisionEvent(entityB, entityA);
            else if (entityAIsBoidStation && entityBIsProjectile)
                OnProjectileBoidStationCollisionEvent(entityA, entityB);
        }

        void SolveBoidVsProjectileCondition(Entity entityA, Entity entityB, bool entityAIsProjectile, bool entityBIsProjectile)
        {
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
        job.boidStationGroup = GetComponentDataFromEntity<BoidStationComponent>(false);
        job.ElapsedTime = (float)Time.ElapsedTime;
        
        JobHandle jobHandle = job.Schedule(stepPhysicsWorld.Simulation, ref buildPhysicsWorld.PhysicsWorld, inputDependencies);
        jobHandle.Complete();
        
        return jobHandle;
    }
}

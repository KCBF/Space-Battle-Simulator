// Author: Peter Richards.
using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;
using Unity.Physics;
using UnityEngine;

[UpdateAfter(typeof(BoidsSystem))]
public class BoidStationWalkerSystem : ComponentSystem
{
    Unity.Physics.Systems.BuildPhysicsWorld physicsWorldSystem;
    CollisionWorld collisionWorld;

    public EntityParticleManager[] ParticleManagers;

    Unity.Mathematics.Random random;
    public AStarGrid grid;
    public float updateGridRate = 2.0f;
    public float nextUpdateTime = 0.0f;

    protected override void OnCreate()
    {
        physicsWorldSystem = World.GetOrCreateSystem<Unity.Physics.Systems.BuildPhysicsWorld>();
        random = new Unity.Mathematics.Random(1);
    }

    protected override void OnStartRunning()
    {
        Entities.ForEach((Entity entity, ref BoidStationComponent boidStation,
            DynamicBuffer <AStarPathElement> pathBuffer) =>
        {
            pathBuffer.Clear();
            EntityParticleManager particleManager = MonoBehaviour.Instantiate(ParticleManagers[boidStation.ParticleManagerIdx]);
            EntityManager.AddComponentObject(entity, particleManager);
        });
    }

    protected override void OnStopRunning()
    {
        nextUpdateTime = 0.0f;
    }

    protected override void OnUpdate()
    {
        collisionWorld = physicsWorldSystem.PhysicsWorld.CollisionWorld;

        bool gridUpdated = nextUpdateTime <= Time.ElapsedTime;
        if (gridUpdated)
        {
            grid.SetGrid(EntityManager, collisionWorld);
            nextUpdateTime = (float)Time.ElapsedTime + updateGridRate;
        }

        Entities.ForEach((Entity entity,
            DynamicBuffer<AStarPathElement> pathBuffer, ref AStarWalkerComponent walker, ref BoidStationComponent boidStation,
            ref Translation translation, ref Rotation rot, ref PhysicsVelocity velocity) =>
        {
            velocity.Linear = float3.zero;
            EntityParticleManager entityParticleManager = EntityManager.GetComponentObject<EntityParticleManager>(entity);
            if (boidStation.HP <= 0.0f)
            {
                ParticleSystem deathParticleSystem = entityParticleManager.childParticleSystems[BoidStationComponent.DeathParticleIdx];

                if (!boidStation.deaded)
                {
                    deathParticleSystem.Play(true);
                    boidStation.deaded = true;
                }
                velocity.Linear = float3.zero;
                return;
            }
            entityParticleManager.transform.position = translation.Value;
            entityParticleManager.transform.rotation = rot.Value;

            DynamicBuffer<int3> path = pathBuffer.Reinterpret<int3>();
            
            if (gridUpdated || walker.CurrentNodeIdx <= -1 || walker.CurrentNodeIdx >= path.Length)
            {
                float3 startPos = translation.Value;
                if (!gridUpdated)
                {
                    walker.TargetPos = random.NextFloat3Direction() * random.NextFloat(boidStation.PatrolRadius);
                    boidStation.TargetUp = random.NextFloat3Direction();
                }

                int3 lastNod = int3.zero;
                if (gridUpdated && walker.CurrentNodeIdx >= 0 && walker.CurrentNodeIdx < path.Length)
                    lastNod = path[walker.CurrentNodeIdx];

                path.Clear();
                grid.FindPath(translation.Value, walker.TargetPos, ref path);
                walker.CurrentNodeIdx = path.Length - 1;

                if (gridUpdated && walker.CurrentNodeIdx >= 0 && walker.CurrentNodeIdx < path.Length)
                    path[walker.CurrentNodeIdx] = lastNod;
            }

            if (walker.CurrentNodeIdx <= -1 || walker.CurrentNodeIdx >= path.Length)
                return;
            
            int3 pathNodeXYZ = path[walker.CurrentNodeIdx];
            float3 pathPos = grid.GetWorldPos(pathNodeXYZ);
            float3 pathDelta = pathPos - translation.Value;
            
            if (math.lengthsq(pathDelta) <= walker.NextNodeThreshold * walker.NextNodeThreshold)
                --walker.CurrentNodeIdx;

            float3 lookDir = math.normalizesafe(pathDelta);
            quaternion lookRot = quaternion.LookRotationSafe(lookDir, boidStation.TargetUp);
            rot.Value = math.slerp(rot.Value, lookRot, walker.LookSpeed * Time.DeltaTime);
            
            velocity.Linear = lookDir * walker.MoveSpeed;
            
            for (int c = 0; c < path.Length - 1; ++c)
            {
                int3 nxyz = path[c];
                float3 pp = grid.GetWorldPos(nxyz);

                int3 onxyz = path[c + 1];
                float3 opp = grid.GetWorldPos(onxyz);

                Debug.DrawLine(pp, opp);
            }
        });
    }
}

// Author: Peter Richards.
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;
using Unity.Collections;

[UpdateAfter(typeof(BoidTeamStatTrackerSystem))]
public class BoidRespawnerSystem : ComponentSystem
{
    protected override void OnUpdate()
    {
        NativeArray<Entity> nextDeadBoidInGroup = new NativeArray<Entity>((int)BoidComponent.MaxGroupID, Allocator.Temp);
        NativeArray<float> minDeadTimeInGroup = new NativeArray<float>((int)BoidComponent.MaxGroupID, Allocator.Temp);

        for (int i = 0; i < minDeadTimeInGroup.Length; ++i)
            minDeadTimeInGroup[i] = float.MaxValue;

        // Find dead boids to respawn.
        Entities.ForEach((Entity entity, ref BoidComponent boid) =>
        {
            if (boid.HP <= 0.0f &&
                boid.DiedTime < minDeadTimeInGroup[(int)boid.GroupID])
            {
                nextDeadBoidInGroup[(int)boid.GroupID] = entity;
                minDeadTimeInGroup[(int)boid.GroupID] = boid.NextAllowShootTime;
            }
        });

        Entities.ForEach((ref Translation translation, ref Rotation rot,
            ref BoidStationComponent boidStation, ref BoidSpawnerComponent boidSpawner) =>
        {
            boidSpawner.Enabled = boidStation.HP > 0.0f;

            if (!boidSpawner.Enabled ||
                boidSpawner.NextSpawnTime > Time.ElapsedTime)
                return;
            
            Entity boidEntity = nextDeadBoidInGroup[(int)boidStation.GroupID];
            if (boidEntity == Entity.Null)
                return;
            
            boidSpawner.NextSpawnTime = (float)Time.ElapsedTime + boidSpawner.SpawnRate;

            float3 spawnPos = /*translation.Value + */math.rotate(rot.Value, boidSpawner.SpawnOffSet);
            EntityManager.SetComponentData(boidEntity, new Translation { Value = spawnPos });
            EntityManager.SetComponentData(boidEntity, new Rotation { Value = math.inverse(rot.Value) });
            
            BoidComponent boid = EntityManager.GetComponentData<BoidComponent>(boidEntity);
            boid.HP = 1.0f;
            EntityManager.SetComponentData(boidEntity, boid);
        });
    }
}

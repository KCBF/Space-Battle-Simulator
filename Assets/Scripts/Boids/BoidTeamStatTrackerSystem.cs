// Author: Peter Richards.
using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Collections;
using Unity.Transforms;

[UpdateAfter(typeof(BoidsSystem))]
public class BoidTeamStatTrackerSystem : ComponentSystem
{
    public int[] BoidTeamTotalCounts;
    public int[] BoidTeamAliveCounts;
    public float[] BoidBasesHPs;
    public float[] BoidNextSpawnTimes;

    protected override void OnCreate()
    {
        BoidTeamTotalCounts = new int[BoidComponent.MaxGroupID];
        BoidTeamAliveCounts = new int[BoidComponent.MaxGroupID];
        BoidBasesHPs = new float[BoidComponent.MaxGroupID];
        BoidNextSpawnTimes = new float[BoidComponent.MaxGroupID];
    }

    protected override void OnUpdate()
    {
        for (int i = 0; i < BoidTeamTotalCounts.Length; ++i)
        {
            BoidTeamTotalCounts[i] = 0;
            BoidTeamAliveCounts[i] = 0;
            BoidBasesHPs[i] = 0;
            BoidNextSpawnTimes[i] = 0;
        }

        Entities.ForEach((ref BoidComponent boid) =>
        {
            ++BoidTeamTotalCounts[boid.GroupID];

            if (boid.HP > 0.0f)
                ++BoidTeamAliveCounts[boid.GroupID];
        });

        Entities.ForEach((ref BoidStationComponent boidStation) =>
        {
            BoidBasesHPs[boidStation.GroupID] += boidStation.HP;
        });

        Entities.ForEach((ref BoidStationComponent boidStation, ref BoidSpawnerComponent boidSpawner) =>
        {
            boidSpawner.AliveBoidCount = BoidTeamAliveCounts[boidStation.GroupID];
            boidSpawner.TotalBoidCount = BoidTeamTotalCounts[boidStation.GroupID];
            BoidNextSpawnTimes[boidStation.GroupID] = boidSpawner.NextSpawnTime;
        });
    }
}

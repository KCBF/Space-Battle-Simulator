using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Collections;

public class BoidTeamStatTrackerSystem : ComponentSystem
{
    public uint[] BoidTeamTotalCounts;
    public uint[] BoidTeamAliveCounts;
    public float[] BoidBasesHPs;

    protected override void OnCreate()
    {
        BoidTeamTotalCounts = new uint[4];
        BoidTeamAliveCounts = new uint[4];
        BoidBasesHPs = new float[4];
    }

    protected override void OnUpdate()
    {
        for (int i = 0; i < BoidTeamTotalCounts.Length; ++i)
        {
            BoidTeamTotalCounts[i] = 0;
            BoidTeamAliveCounts[i] = 0;
            BoidBasesHPs[i] = 0;
        }

        Entities.ForEach((ref BoidComponent boid) =>
        {
            ++BoidTeamTotalCounts[(int)boid.GroupID];

            if (boid.HP > 0.0f)
                ++BoidTeamAliveCounts[(int)boid.GroupID];
        });

        Entities.ForEach((ref BoidStationComponent spaceShipBase) =>
        {
            BoidBasesHPs[(int)spaceShipBase.GroupID] += spaceShipBase.HP;
        });
    }
}

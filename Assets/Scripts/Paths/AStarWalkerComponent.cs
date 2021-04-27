// Author: Peter Richards.
using Unity.Entities;
using Unity.Mathematics;

[GenerateAuthoringComponent]
public struct AStarWalkerComponent : IComponentData
{
    public int CurrentNodeIdx;
    public float NextNodeThreshold;
    public float3 TargetPos;
    public float MoveSpeed;
    public float LookSpeed;
}

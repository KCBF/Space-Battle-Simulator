// Author: Peter Richards.
using Unity.Entities;
using Unity.Mathematics;

[System.Serializable]
[GenerateAuthoringComponent]
[InternalBufferCapacity(15)]
public struct AStarPathElement : IBufferElementData
{
    public int3 Value;
}

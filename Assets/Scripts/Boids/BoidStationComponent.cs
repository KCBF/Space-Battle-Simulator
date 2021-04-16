using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;

[GenerateAuthoringComponent]
public struct BoidStationComponent : IComponentData
{
    public float HP;
    public uint GroupID;
    public float AttractRadius;
}

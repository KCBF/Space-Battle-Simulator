using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;

[GenerateAuthoringComponent]
public struct BoidStationComponent : IComponentData
{
    public float HP;
    public float GroupID;
}

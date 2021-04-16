using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;

[GenerateAuthoringComponent]
public struct BoidUserControllerComponent : IComponentData
{
    public uint SelectedGroup;
    public Entity BoidEntity;
    public bool Manual;
}

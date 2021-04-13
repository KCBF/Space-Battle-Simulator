using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;

[GenerateAuthoringComponent]
public struct BoidControllerComponent : IComponentData
{
    public uint SelectedGroup;
    public Entity BoidEntity;
    public bool Manual;
}

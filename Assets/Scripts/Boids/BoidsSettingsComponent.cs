using Unity.Entities;

[System.Serializable]
[GenerateAuthoringComponent]
public struct BoidSettingsComponent : IComponentData
{
    public float MoveSpeed;
    public float LookSpeed;

    public float ViewDst;
    public float ViewAngle;

    public float CohesionScalar;
    public float AlignmentScalar;
    public float SeparationScalar;
}
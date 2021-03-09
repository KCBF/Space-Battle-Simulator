using UnityEngine;
using Unity.Mathematics;
using Unity.Entities;
using Unity.Transforms;

public class CameraEntityTarget : MonoBehaviour
{
    public Entity targetEntity;
    public float3 offset;

    EntityManager entityManager;

    void Awake()
    {
        entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
    }

    private void LateUpdate()
    {
        if (targetEntity == Entity.Null)
            return;

        Translation entityPos = entityManager.GetComponentData<Translation>(targetEntity);
        transform.position = entityPos.Value + offset;

        Rotation entityRot = entityManager.GetComponentData<Rotation>(targetEntity);
        transform.rotation = entityRot.Value;
    }
}

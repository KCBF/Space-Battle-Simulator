using UnityEngine;
using Unity.Mathematics;
using Unity.Entities;
using Unity.Transforms;

public class CameraEntityTarget : MonoBehaviour
{
    public Entity targetEntity;

    EntityManager entityManager;

    void Awake()
    {
        entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
    }

    private void LateUpdate()
    {
        if (targetEntity == Entity.Null)
            return;

        
    }
}

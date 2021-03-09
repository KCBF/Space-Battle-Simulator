using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;
using Unity.Transforms;
using Unity.Rendering;
using Unity.Mathematics;
using Unity.Physics;

[System.Serializable]
public struct BoidGroup
{
    public GameObject spaceShip;
    public Entity spaceShipEntity;
    public BlobAssetStore blobAssetStore;

    public Entity settingsEntity;
    public BoidSettingsComponent settings;

    public void Init(EntityManager entityManager)
    {
        settingsEntity = entityManager.CreateEntity(typeof(BoidSettingsComponent));
        SetSettingsComponentData(entityManager);

        InitSpaceShipEntity();
    }

    public void ClampMinSeparation()
    {
        if (spaceShip == null)
            return;

        Vector3 boundsSize = spaceShip.GetComponent<UnityEngine.Collider>().bounds.size;
        int minAxisI = Math.IndexOfMinComponent(boundsSize);
        float minAxis = boundsSize[minAxisI];

        settings.SeparationScalar = math.max(settings.SeparationScalar, minAxis);
    }

    public void SetSettingsComponentData(EntityManager entityManager)
    {
        ClampMinSeparation();
        entityManager.SetComponentData(settingsEntity, settings);
    }

    void InitSpaceShipEntity()
    {
        if (spaceShip == null)
            return;

        blobAssetStore = new BlobAssetStore();
        var spaceShipEntitySettings = GameObjectConversionSettings.FromWorld(World.DefaultGameObjectInjectionWorld, blobAssetStore);
        spaceShipEntity = GameObjectConversionUtility.ConvertGameObjectHierarchy(spaceShip, spaceShipEntitySettings);
    }
}

public class BoidsSim : MonoBehaviour
{
    public BoidGroup boidGroup;

    public CameraEntityTarget cameraEntityTarget;

    EntityManager entityManager;

    void Start()
    {
        entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
        boidGroup.Init(entityManager);

        SpawnBoids(10);
    }

    public void SpawnBoids(uint num)
    {
        Unity.Mathematics.Random random = new Unity.Mathematics.Random(2);

        for (uint i = 0; i < num; ++i)
        {
            Entity entity = entityManager.Instantiate(boidGroup.spaceShipEntity);
            
            float3 spawnPos = random.NextFloat3Direction() * 100.0f;
            spawnPos.y = 0.0f;
            entityManager.SetComponentData(entity, new Translation { Value = spawnPos });
            entityManager.SetComponentData(entity, new PhysicsVelocity { Linear = random.NextFloat3Direction() * 20.0f });

            entityManager.SetComponentData(entity, new BoidComponent { SettingsEntity = boidGroup.settingsEntity });

            if (i == 0)
                cameraEntityTarget.targetEntity = entity;
        }
    }

    private void Update()
    {
        boidGroup.SetSettingsComponentData(entityManager);
    }

    private void OnDestroy()
    {
        boidGroup.blobAssetStore.Dispose();
    }

    private void OnValidate()
    {
        boidGroup.ClampMinSeparation();
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, boidGroup.settings.ViewDst);

        float halfFOV = boidGroup.settings.ViewAngle * 0.5f;
        Vector3 leftRayDirection = Quaternion.AngleAxis(-halfFOV, Vector3.up) * transform.forward;
        Vector3 rightRayDirection = Quaternion.AngleAxis(halfFOV, Vector3.up) * transform.forward;

        Gizmos.color = Color.red;
        Gizmos.DrawRay(transform.position, transform.forward * boidGroup.settings.ViewDst);
        Gizmos.DrawRay(transform.position, leftRayDirection * boidGroup.settings.ViewDst);
        Gizmos.DrawRay(transform.position, rightRayDirection * boidGroup.settings.ViewDst);
    }
}

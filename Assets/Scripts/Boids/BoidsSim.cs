using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;
using Unity.Transforms;
using Unity.Rendering;
using Unity.Mathematics;
using Unity.Physics;

[System.Serializable]
public class BoidGroup
{
    public uint numToSpawn = 10;
    public float spawnRadius = 100.0f;

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

    public void SetSettingsComponentData(EntityManager entityManager)
    {
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
    public BoidGroup[] boidGroups;
    public int previewGroupI;

    public CameraEntityTarget cameraEntityTarget;

    EntityManager entityManager;
    Unity.Mathematics.Random random;

    void Start()
    {
        entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
        random = new Unity.Mathematics.Random(5);

        foreach (BoidGroup boidGroup in boidGroups)
        {
            boidGroup.Init(entityManager);
            SpawnBoids(boidGroup.numToSpawn, boidGroup);
        }
    }

    public void SpawnBoids(uint num, BoidGroup boidGroup)
    {
        for (uint i = 0; i < num; ++i)
        {
            Entity entity = entityManager.Instantiate(boidGroup.spaceShipEntity);

            float3 spawnPos = random.NextFloat3Direction() * boidGroup.spawnRadius;
            spawnPos += boidGroup.settings.MapCentre;
            spawnPos.y = 0.0f;

            float3 vel = random.NextFloat3Direction() * boidGroup.settings.MoveSpeed;
            vel.y = 0.0f;

            entityManager.SetComponentData(entity, new Translation { Value = spawnPos });
            entityManager.SetComponentData(entity, new PhysicsVelocity { Linear = vel });

            BoidComponent boid = entityManager.GetComponentData<BoidComponent>(entity);
            boid.SettingsEntity = boidGroup.settingsEntity;
            entityManager.SetComponentData(entity, boid);

            if (i == 0)
                cameraEntityTarget.targetEntity = entity;
        }
    }

    private void Update()
    {
        foreach (BoidGroup boidGroup in boidGroups)
            boidGroup.SetSettingsComponentData(entityManager);
    }

    private void OnDestroy()
    {
        foreach (BoidGroup boidGroup in boidGroups)
            boidGroup.blobAssetStore.Dispose();
    }

    private void OnDrawGizmos()
    {
        previewGroupI = Mathf.Clamp(previewGroupI, 0, boidGroups.Length - 1);
        if (boidGroups.Length == 0)
            return;

        GizmoBoidGroupSettings(boidGroups[previewGroupI]);        
    }

    void GizmoBoidGroupSettings(BoidGroup previewGroup)
    {
        Vector3 previewPos = (cameraEntityTarget.targetEntity == Entity.Null) ? transform.position :
            (Vector3)entityManager.GetComponentData<Translation>(cameraEntityTarget.targetEntity).Value;

        Vector3 previewForward = (cameraEntityTarget.targetEntity == Entity.Null) ? transform.forward :
            (Vector3)math.forward(entityManager.GetComponentData<Rotation>(cameraEntityTarget.targetEntity).Value);

        Vector3 previewUp = (cameraEntityTarget.targetEntity == Entity.Null) ? transform.up :
            (Vector3)math.rotate(entityManager.GetComponentData<Rotation>(cameraEntityTarget.targetEntity).Value, math.up());

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(previewPos, previewGroup.settings.BoidDetectRadius);

        float halfFOV = previewGroup.settings.BoidDetectFOV * 0.5f;
        Vector3 leftRayDirection = Quaternion.AngleAxis(-halfFOV, previewUp) * previewForward;
        Vector3 rightRayDirection = Quaternion.AngleAxis(halfFOV, previewUp) * previewForward;

        Gizmos.DrawRay(previewPos, previewForward * previewGroup.settings.BoidDetectRadius);
        Gizmos.DrawRay(previewPos, leftRayDirection * previewGroup.settings.BoidDetectRadius);
        Gizmos.DrawRay(previewPos, rightRayDirection * previewGroup.settings.BoidDetectRadius);

        halfFOV = previewGroup.settings.FiringFOV * 0.5f;
        leftRayDirection = Quaternion.AngleAxis(-halfFOV, previewUp) * previewForward;
        rightRayDirection = Quaternion.AngleAxis(halfFOV, previewUp) * previewForward;

        Gizmos.color = Color.green;
        Gizmos.DrawRay(previewPos, leftRayDirection * previewGroup.settings.ObstacleViewDst);
        Gizmos.DrawRay(previewPos, rightRayDirection * previewGroup.settings.ObstacleViewDst);
    }
}
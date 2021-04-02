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
    public bool gizmo = true;
    public uint numToSpawn = 10;
    public float spawnRadius = 100.0f;

    public GameObject spaceShip;
    public Entity spaceShipEntity;
    public BlobAssetStore spaceShipBlobAssetStore;

    public GameObject bullet;
    public Entity bulletEntity;
    public BlobAssetStore bulletBlobAssetStore;

    public Entity settingsEntity;
    public BoidSettingsComponent settings;

    public void Init(EntityManager entityManager)
    {
        settingsEntity = entityManager.CreateEntity(typeof(BoidSettingsComponent));
        SetSettingsComponentData(entityManager);

        InitSpaceShipEntity();
        InitBulletEntity();
    }

    public void SetSettingsComponentData(EntityManager entityManager)
    {
        settings.BulletEntity = bulletEntity;
        entityManager.SetComponentData(settingsEntity, settings);
    }

    void InitSpaceShipEntity()
    {
        if (spaceShip == null)
            return;

        spaceShipBlobAssetStore = new BlobAssetStore();
        var spaceShipEntitySettings = GameObjectConversionSettings.FromWorld(World.DefaultGameObjectInjectionWorld, spaceShipBlobAssetStore);
        spaceShipEntity = GameObjectConversionUtility.ConvertGameObjectHierarchy(spaceShip, spaceShipEntitySettings);
    }

    void InitBulletEntity()
    {
        if (bullet == null)
            return;

        bulletBlobAssetStore = new BlobAssetStore();
        var bulletEntitySettings = GameObjectConversionSettings.FromWorld(World.DefaultGameObjectInjectionWorld, bulletBlobAssetStore);
        bulletEntity = GameObjectConversionUtility.ConvertGameObjectHierarchy(bullet, bulletEntitySettings);
    }
}

public class BoidsSim : MonoBehaviour
{
    public BoidGroup[] boidGroups;

    public CameraEntityTarget cameraEntityTarget;

    public GameObject[] asteroidEntities;
    BlobAssetStore[] asteroidBlobAssetStores;
    public float asteroidInnerSpaceRadius;
    public float asteroidOuterSpaceRadius;
    public int numAsteroidsToSpawn;
    
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

        SpawnAsteroids();
    }

    public void SpawnAsteroids()
    {
        asteroidBlobAssetStores = new BlobAssetStore[asteroidEntities.Length];
        for (int j = 0; j < asteroidEntities.Length; ++j)
        {
            asteroidBlobAssetStores[j] = new BlobAssetStore();
            var bulletEntitySettings = GameObjectConversionSettings.FromWorld(World.DefaultGameObjectInjectionWorld, asteroidBlobAssetStores[j]);
            Entity asteroidEntity = GameObjectConversionUtility.ConvertGameObjectHierarchy(asteroidEntities[j], bulletEntitySettings);

            for (int i = 0; i < numAsteroidsToSpawn; ++i)
            {
                float3 spawnDir = random.NextFloat3Direction();
                
                float3 spawnPos = spawnDir * random.NextFloat(asteroidInnerSpaceRadius, asteroidOuterSpaceRadius);
                spawnPos += (float3)transform.position;
                
                Entity newAsteroidEntity = entityManager.Instantiate(asteroidEntity);
                entityManager.SetComponentData(newAsteroidEntity, new Translation { Value = spawnPos });
                entityManager.SetComponentData(newAsteroidEntity, new Rotation { Value = random.NextQuaternionRotation() });
            }
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
        {
            if (boidGroup.spaceShipBlobAssetStore != null)
                boidGroup.spaceShipBlobAssetStore.Dispose();

            if (boidGroup.bulletBlobAssetStore != null)
                boidGroup.bulletBlobAssetStore.Dispose();
        }

        foreach (BlobAssetStore asteroidBlobAssetStore in asteroidBlobAssetStores)
        {
            if (asteroidBlobAssetStore != null)
                asteroidBlobAssetStore.Dispose();
        }
    }

    private void OnDrawGizmos()
    {
        for (int i = 0; i < boidGroups.Length; ++i)
            GizmoBoidGroupSettings(i);        
    }

    void GizmoBoidGroupSettings(int i)
    {
        BoidGroup boidGroup = boidGroups[i];
        if (!boidGroup.gizmo)
            return;

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(boidGroup.settings.MapCentre, new Vector3(boidGroup.settings.MapRadius, boidGroup.settings.MapRadius, boidGroup.settings.MapRadius) * 2.0f);

        Vector3 previewPos = (cameraEntityTarget.targetEntity == Entity.Null) ? (Vector3)boidGroup.settings.MapCentre :
            (Vector3)entityManager.GetComponentData<Translation>(cameraEntityTarget.targetEntity).Value;

        Vector3 previewForward = (cameraEntityTarget.targetEntity == Entity.Null) ? transform.forward :
            (Vector3)math.forward(entityManager.GetComponentData<Rotation>(cameraEntityTarget.targetEntity).Value);

        Vector3 previewUp = (cameraEntityTarget.targetEntity == Entity.Null) ? transform.up :
            (Vector3)math.rotate(entityManager.GetComponentData<Rotation>(cameraEntityTarget.targetEntity).Value, math.up());

        Gizmos.color = Color.red;

        // Draw boid detection properties.
        Gizmos.DrawWireSphere(previewPos, boidGroup.settings.BoidDetectRadius);

        float halfFOV = boidGroup.settings.BoidDetectFOV * 0.5f;
        Vector3 leftRayDirection = Quaternion.AngleAxis(-halfFOV, previewUp) * previewForward;
        Vector3 rightRayDirection = Quaternion.AngleAxis(halfFOV, previewUp) * previewForward;

        Gizmos.DrawRay(previewPos, leftRayDirection * boidGroup.settings.BoidDetectRadius);
        Gizmos.DrawRay(previewPos, rightRayDirection * boidGroup.settings.BoidDetectRadius);

        // Draw obstacle detection properties.
        Gizmos.color = Color.magenta;
        Gizmos.DrawRay(previewPos, previewForward * boidGroup.settings.ObstacleViewDst);

        // Draw obstacle detection properties.
        halfFOV = boidGroup.settings.FiringFOV * 0.5f;
        leftRayDirection = Quaternion.AngleAxis(-halfFOV, previewUp) * previewForward;
        rightRayDirection = Quaternion.AngleAxis(halfFOV, previewUp) * previewForward;

        Gizmos.color = Color.green;
        Gizmos.DrawRay(previewPos, leftRayDirection * boidGroup.settings.FiringViewDst);
        Gizmos.DrawRay(previewPos, rightRayDirection * boidGroup.settings.FiringViewDst);
    }
}
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;
using Unity.Transforms;
using Unity.Rendering;
using Unity.Mathematics;

[System.Serializable]
public struct BoidSettings
{
    public GameObject spaceShip;
    public Entity spaceShipEntity;
    public BlobAssetStore blobAssetStore;

    public void InitSpaceShipEntity()
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
    public BoidSettings boidSettings;

    EntityManager entityManager;

    void Start()
    {
        entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
        boidSettings.InitSpaceShipEntity();

        SpawnBoids(10);
    }

    public void SpawnBoids(uint num)
    {
        Unity.Mathematics.Random random = new Unity.Mathematics.Random(1);

        for (uint i = 0; i < num; ++i)
        {
            Entity entity = entityManager.Instantiate(boidSettings.spaceShipEntity);
                       
            float3 spawnPos = random.NextFloat3Direction() * 50.0f;
            spawnPos.y = 0.0f;
            entityManager.SetComponentData(entity, new Translation { Value = spawnPos });
        }
    }

    private void OnDestroy()
    {
        boidSettings.blobAssetStore.Dispose();
    }
}

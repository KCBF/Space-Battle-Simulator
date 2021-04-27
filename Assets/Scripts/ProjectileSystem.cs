// Author: Peter Richards.
using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;
using Unity.Physics;
using UnityEngine;

[UpdateBefore(typeof(Unity.Physics.Systems.BuildPhysicsWorld))]
public class ProjectileSystem : ComponentSystem
{
    public EntityParticleManager[] ParticleManagers;

    protected override void OnUpdate()
    {
        Entities.ForEach((Entity entity,
            ref Translation translation, ref Rotation rot,
            ref ProjectileComponent projectile, ref PhysicsVelocity velocity) =>
        {
            if (EntityManager.HasComponent<EntityParticleManager>(entity))
                return;

            EntityParticleManager particleManager = MonoBehaviour.Instantiate(ParticleManagers[projectile.ParticleManagerIdx]);
            EntityManager.AddComponentObject(entity, particleManager);
        });

        Entities.ForEach((Entity entity,
            ref Translation translation, ref Rotation rot,
            ref ProjectileComponent projectile, ref PhysicsVelocity velocity) =>
        {
            velocity.Linear = math.forward(rot.Value) * projectile.Speed;

            if (projectile.DespawnTime == 0.0f)
                projectile.DespawnTime = (float)Time.ElapsedTime + projectile.LifeTime;

            else if (projectile.DespawnTime <= Time.ElapsedTime)
                PostUpdateCommands.DestroyEntity(entity);
                
            DoBoidParticleEffects(entity, translation, rot, projectile);
        });
    }

    void DoBoidParticleEffects(Entity entity, Translation translation, Rotation rot, ProjectileComponent projectile)
    {
        EntityParticleManager particleManager = EntityManager.GetComponentObject<EntityParticleManager>(entity);
        if (particleManager == null)
            return;

        particleManager.transform.position = translation.Value;
        particleManager.transform.rotation = rot.Value;
        ParticleSystem[] particleSystems = particleManager.childParticleSystems;
        
        if (projectile.DespawnTime <= Time.ElapsedTime)
        {
            particleSystems[ProjectileComponent.TrailParticle1Idx].Stop(true);
            particleSystems[ProjectileComponent.TrailParticle2Idx].Stop(true);
            particleSystems[ProjectileComponent.TrailParticle3Idx].Stop(true);

            if (!particleSystems[ProjectileComponent.DeathParticleIdx].isPlaying)
            {
                particleSystems[ProjectileComponent.DeathParticleIdx].Play(true);
                MonoBehaviour.Destroy(particleManager.gameObject, particleSystems[BoidComponent.DeathParticleIdx].main.duration);
            }
        }

        else
        {
            if (!particleSystems[ProjectileComponent.TrailParticle1Idx].isPlaying)
                particleSystems[ProjectileComponent.TrailParticle1Idx].Play(true);

            if (!particleSystems[ProjectileComponent.TrailParticle2Idx].isPlaying)
                particleSystems[ProjectileComponent.TrailParticle2Idx].Play(true);

            if (!particleSystems[ProjectileComponent.TrailParticle3Idx].isPlaying)
                particleSystems[ProjectileComponent.TrailParticle3Idx].Play(true);
        }
    }
}
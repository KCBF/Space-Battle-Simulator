using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EntityParticleManager : MonoBehaviour
{
    public ParticleSystem[] childParticleSystems;

    private void OnDestroy()
    {
        foreach (ParticleSystem particleSystem in childParticleSystems)
        {
            if (particleSystem != null)
                Destroy(particleSystem);
        }
    }
}

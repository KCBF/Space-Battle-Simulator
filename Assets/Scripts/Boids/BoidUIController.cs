using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Unity.Entities;

public class BoidUIController : MonoBehaviour
{
    public Image healthBar;
    public Text boidTeamText;
    public Text seedText;
    public Text maunalText;
    public Text missleCooldown;
    public Image crosshair;

    EntityManager entityManager;

    void Awake()
    {
        entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
    }

    void LateUpdate()
    {
        var boidUserControllerSystem = World.DefaultGameObjectInjectionWorld.GetExistingSystem<BoidUserControllerSystem>();
        if (!boidUserControllerSystem.HasSingleton<BoidUserControllerComponent>())
            return;

        BoidUserControllerComponent boidControllerComponent = boidUserControllerSystem.GetSingleton<BoidUserControllerComponent>();

        boidTeamText.text = "Team: " + boidControllerComponent.SelectedGroup.ToString();
        seedText.text = "Seed: " + BoidsSim.Seed.ToString();
        maunalText.text = boidControllerComponent.Manual ? "Manual Control" : "AI Control";

        crosshair.gameObject.SetActive(boidControllerComponent.Manual);

        if (boidControllerComponent.Manual)
            crosshair.transform.position = Input.mousePosition;

        if (boidControllerComponent.BoidEntity == Entity.Null)
            return;

        BoidComponent boidComponent = entityManager.GetComponentData<BoidComponent>(boidControllerComponent.BoidEntity);

        healthBar.fillAmount = boidComponent.HP;
        missleCooldown.text = "Missle Cooldown: " + Mathf.Max(boidComponent.NextAllowShootTime - Time.time, 0.0f).ToString();
    }
}

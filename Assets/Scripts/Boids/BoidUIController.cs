// Author: Peter Richards.
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
    public Text teamStatsText;
    public Text controlsText;

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

        DrawTeamStatsText();
        DrawBoidControllerBoidEntityData(boidControllerComponent);
    }

    void DrawTeamStatsText()
    {
        teamStatsText.enabled = Input.GetKey(KeyCode.R);
        controlsText.enabled = Input.GetKey(KeyCode.R);
        if (!teamStatsText.enabled)
            return;

        teamStatsText.text = "";
        var boidTeamStatTrackerSystem = World.DefaultGameObjectInjectionWorld.GetExistingSystem<BoidTeamStatTrackerSystem>();

        for (int i = 1; i < boidTeamStatTrackerSystem.BoidTeamTotalCounts.Length; ++i)
        {
            teamStatsText.text += $"Team {i}: " +
                $"{boidTeamStatTrackerSystem.BoidTeamAliveCounts[i]} / {boidTeamStatTrackerSystem.BoidTeamTotalCounts[i]}\n";

            teamStatsText.text += $"Team {i} Base HP: " +
                $"{boidTeamStatTrackerSystem.BoidBasesHPs[i]}\n";
            
            teamStatsText.text += $"Team {i} Respawn Time: " +
                Mathf.Max(0.0f, boidTeamStatTrackerSystem.BoidNextSpawnTimes[i] - Time.time).ToString("f2") + "\n";
        }
    }

    void DrawBoidControllerBoidEntityData(in BoidUserControllerComponent boidControllerComponent)
    {
        if (boidControllerComponent.Manual)
            crosshair.transform.position = Input.mousePosition;

        if (boidControllerComponent.BoidEntity == Entity.Null)
            return;

        BoidComponent boidComponent = entityManager.GetComponentData<BoidComponent>(boidControllerComponent.BoidEntity);

        healthBar.fillAmount = boidComponent.HP;
        missleCooldown.text = "Missle Cooldown: " + Mathf.Max(0.0f, boidComponent.NextAllowShootTime - Time.time).ToString("f2");
    }
}

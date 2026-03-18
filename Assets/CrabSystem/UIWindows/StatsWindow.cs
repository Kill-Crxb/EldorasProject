using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class StatsWindow : UIWindow
{
    [Header("Stats Specific")]
    [SerializeField] private TextMeshProUGUI windowTitle;
    [SerializeField] private Button closeButton;

    [Header("Stat Display Components")]
    [SerializeField] private CoreStatsUI coreStatsUI;
    [SerializeField] private SecondaryStatsUI secondaryStatsUI;
    [SerializeField] private StatAllocationUI statAllocationUI;

    private RPGSystem rpgSystem;

    protected override void SetupWindow()
    {
        if (windowTitle != null)
            windowTitle.text = "Character Stats";

        if (closeButton != null)
            closeButton.onClick.AddListener(CloseThisWindow);

        if (playerBrain != null)
        {
            rpgSystem = playerBrain.GetModule<RPGSystem>();

            // Initialize UI components if they need brain reference
            if (coreStatsUI != null)
                ConnectUIComponent(coreStatsUI);
            if (secondaryStatsUI != null)
                ConnectUIComponent(secondaryStatsUI);
            if (statAllocationUI != null)
                ConnectUIComponent(statAllocationUI);
        }

        if (debugMode)
            Debug.Log("[StatsWindow] Setup complete");
    }

    private void ConnectUIComponent(MonoBehaviour component)
    {
        // These components auto-find their references on Start()
        // We just need to make sure they're enabled
        if (component != null)
            component.gameObject.SetActive(true);
    }

    public override void OnClose()
    {
        base.OnClose();

        if (closeButton != null)
            closeButton.onClick.RemoveListener(CloseThisWindow);
    }
}
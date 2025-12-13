using UnityEngine;
using TMPro;

public class PlayerInfoPanel : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private TextMeshProUGUI playerNameText;
    [SerializeField] private TextMeshProUGUI factionNameText;
    [SerializeField] private TextMeshProUGUI playerLvlText;

    [Header("Player Reference")]
    [SerializeField] private ControllerBrain playerBrain;

    private PlayerInfoModule playerInfo;
    private IdentityHandler identityHandler;
    private PlayerFactionHandler factionHandler;
    private StatAllocationSystem statAllocation;

    private void Start()
    {
        if (playerBrain == null)
        {
            Debug.LogError("PlayerInfoPanel: Player Brain not assigned!");
            return;
        }

        playerInfo = playerBrain.GetModule<PlayerInfoModule>();
        if (playerInfo == null)
        {
            Debug.LogError("PlayerInfoPanel: Could not find PlayerInfoModule on player");
            return;
        }

        identityHandler = playerInfo.IdentityHandler;
        factionHandler = playerInfo.FactionHandler;

        if (identityHandler == null)
        {
            Debug.LogError("PlayerInfoPanel: IdentityHandler not found");
        }

        if (factionHandler == null)
        {
            Debug.LogError("PlayerInfoPanel: PlayerFactionHandler not found");
        }

        // Get StatAllocationSystem to subscribe to level changes
        statAllocation = playerBrain.Stats?.Allocation;
        if (statAllocation != null)
        {
            statAllocation.OnLevelChanged += OnLevelChanged;
        }

        UpdateDisplay();
    }

    private void OnDestroy()
    {
        // Unsubscribe from events
        if (statAllocation != null)
        {
            statAllocation.OnLevelChanged -= OnLevelChanged;
        }
    }

    private void OnLevelChanged(int oldLevel, int newLevel)
    {
        UpdateDisplay();
    }

    private void UpdateDisplay()
    {
        if (playerNameText != null && identityHandler != null)
        {
            playerNameText.text = identityHandler.CharacterName;
        }

        if (factionNameText != null && factionHandler != null)
        {
            factionNameText.text = factionHandler.GetFactionName();
        }

        if (playerLvlText != null && identityHandler != null)
        {
            playerLvlText.text = $"Lvl {identityHandler.Level}";
        }
    }

    public void RefreshDisplay()
    {
        UpdateDisplay();
    }
}
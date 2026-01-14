using UnityEngine;
using TMPro;

/// <summary>
/// Displays player info (name, faction, level) in the UI.
/// Phase 1.6 Days 7-8: Migrated to use direct module access instead of backward compatibility wrappers
/// </summary>
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
    private RPGSystem RPGSystem;  

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

        // MIGRATED: Direct module access instead of backward compatibility wrapper
        RPGSystem = playerBrain.GetModule<RPGSystem>();
        if (RPGSystem != null)
        {
            RPGSystem.OnLevelChanged += OnLevelChanged;
        }

        UpdateDisplay();
    }

    private void OnDestroy()
    {
        // Unsubscribe from events
        if (RPGSystem != null)
        {
            RPGSystem.OnLevelChanged -= OnLevelChanged;
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
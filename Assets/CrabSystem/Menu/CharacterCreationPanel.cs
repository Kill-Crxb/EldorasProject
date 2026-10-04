using System;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// CharacterCreationPanel
///
/// Attach to: CharacterCreationPanel root GameObject (child of MenuUI)
///
/// Name entry plus a rolled statline. There is no manual allocation — the six core
/// stats are rolled from a fixed pool with a guaranteed minimum each, and the player
/// rerolls until they like what they see.
///
/// Randomize is the seam for the rest of character generation. Starting passives,
/// class and family each become another Roll* call inside Randomize(), so the button
/// keeps meaning "give me a whole new character" as those systems land.
///
/// Inspector wiring:
///
///   [Input Fields]
///   nameInputField        — TMP_InputField
///
///   [Stat Display]
///   mindText … insightText — TextMeshProUGUI, one per core stat
///   pointsText            — TextMeshProUGUI, shows the rolled total
///
///   [Navigation]
///   randomizeButton       — Button (reroll)
///   finaliseButton        — Button (write saves + return to select)
///   cancelButton          — Button (discard + return to select)
///
///   [Feedback]
///   feedbackText          — TextMeshProUGUI
///
///   [Config]
///   minPerStat            — Every stat is guaranteed at least this (default 3)
///   statPointPool         — Total points across all six stats (default 30)
///   minNameLength / maxNameLength
///   defaultModelId        — Placeholder until the appearance system is built
/// </summary>
public class CharacterCreationPanel : MonoBehaviour
{
    // ── Inspector ─────────────────────────────────────────────────────────

    [Header("Input Fields")]
    [SerializeField] private TMP_InputField nameInputField;

    [Header("Stat Display")]
    [SerializeField] private TextMeshProUGUI mindText;
    [SerializeField] private TextMeshProUGUI bodyText;
    [SerializeField] private TextMeshProUGUI spiritText;
    [SerializeField] private TextMeshProUGUI resilienceText;
    [SerializeField] private TextMeshProUGUI enduranceText;
    [SerializeField] private TextMeshProUGUI insightText;

    [Tooltip("Shows the rolled total. Always equals the pool — it is there to make the budget visible.")]
    [SerializeField] private TextMeshProUGUI pointsText;

    [Header("Navigation")]
    [SerializeField] private Button randomizeButton;
    [SerializeField] private Button finaliseButton;
    [SerializeField] private Button cancelButton;

    [Header("Feedback")]
    [SerializeField] private TextMeshProUGUI feedbackText;

    [Header("Config")]
    [Tooltip("Every stat is guaranteed at least this much before anything is rolled.")]
    [Min(1)]
    [SerializeField] private int minPerStat = 3;

    [Tooltip("Total points shared across all six stats, minimums included.")]
    [Min(1)]
    [SerializeField] private int statPointPool = 30;

    [Tooltip("Minimum character name length.")]
    [SerializeField] private int minNameLength = 2;

    [Tooltip("Maximum character name length.")]
    [SerializeField] private int maxNameLength = 20;

    [Tooltip("Model ID written to metadata. Placeholder until appearance system is built.")]
    [IdRef(IdKind.Model)] [SerializeField] private string defaultModelId = "female_base_v1";

    // ── Stats ─────────────────────────────────────────────────────────────

    // Index order is the display order, and the only place stat ids are written down.
    private static readonly string[] StatIds =
    {
        "core.mind", "core.body", "core.spirit", "core.resilience", "core.endurance", "core.insight"
    };

    private const int StatCount = 6;

    private readonly int[] stats = new int[StatCount];
    private TextMeshProUGUI[] statTexts;

    // ── State ─────────────────────────────────────────────────────────────

    private AccountManager accountManager;
    private SaveManager saveManager;
    private bool isBusy;
    private Action onComplete;

    // ── Lifecycle ─────────────────────────────────────────────────────────

    private void Awake()
    {
        // Cached here rather than in OnEnable so Randomize() is safe to call from an
        // inspector-wired onClick before the panel has ever been opened.
        statTexts = new[] { mindText, bodyText, spiritText, resilienceText, enduranceText, insightText };
    }

    private void OnEnable()
    {
        accountManager = ManagerBrain.Instance?.GetManager<AccountManager>();
        saveManager = ManagerBrain.Instance?.GetManager<SaveManager>();

        if (accountManager == null || saveManager == null)
        {
            SetFeedback("Error: Managers not found.", isError: true);
            SetInteractable(false);
            return;
        }

        WireButtons();
        ResetToDefaults();
    }

    private void OnDisable()
    {
        UnwireButtons();
    }

    // ── Public API ────────────────────────────────────────────────────────

    /// <summary>
    /// Activate the panel and register the callback fired on finalise or cancel.
    /// Called by CharacterSelectScreen.
    /// </summary>
    public void Open(Action onCompleteCallback)
    {
        onComplete = onCompleteCallback;
        gameObject.SetActive(true);
    }

    /// <summary>
    /// Rolls a fresh character. Every stat starts on the minimum and the leftover
    /// points are dealt out one at a time, so results cluster around the average
    /// instead of producing one maxed stat and five floors.
    ///
    /// Public so the Randomize button can call it directly, and so later generation
    /// steps (passives, class, family) have one obvious place to hang off.
    /// </summary>
    public void Randomize()
    {
        RollStats();
        RefreshStatDisplay();
    }

    // ── Setup ─────────────────────────────────────────────────────────────

    private void WireButtons()
    {
        // A missing reference here used to mean a button that looked fine and did nothing.
        if (randomizeButton == null)
            Debug.LogWarning("[CharacterCreationPanel] No randomize button assigned — rerolling is unavailable.", this);

        randomizeButton?.onClick.AddListener(Randomize);
        finaliseButton?.onClick.AddListener(OnFinaliseClicked);
        cancelButton?.onClick.AddListener(OnCancelClicked);
    }

    private void UnwireButtons()
    {
        randomizeButton?.onClick.RemoveListener(Randomize);
        finaliseButton?.onClick.RemoveListener(OnFinaliseClicked);
        cancelButton?.onClick.RemoveListener(OnCancelClicked);
    }

    private void ResetToDefaults()
    {
        if (nameInputField != null)
            nameInputField.text = "";

        ClearFeedback();
        SetInteractable(true);
        Randomize();
    }

    // ── Rolling ───────────────────────────────────────────────────────────

    private void RollStats()
    {
        int floorTotal = minPerStat * StatCount;

        if (statPointPool < floorTotal)
        {
            Debug.LogWarning($"[CharacterCreationPanel] A pool of {statPointPool} cannot cover " +
                             $"{StatCount} stats at {minPerStat} each. Rolling everything at the minimum.", this);
        }

        for (int i = 0; i < StatCount; i++)
            stats[i] = minPerStat;

        int spare = Mathf.Max(0, statPointPool - floorTotal);

        for (int i = 0; i < spare; i++)
            stats[UnityEngine.Random.Range(0, StatCount)] += 1;
    }

    private void RefreshStatDisplay()
    {
        for (int i = 0; i < StatCount; i++)
            if (statTexts[i] != null) statTexts[i].text = stats[i].ToString();

        if (pointsText != null)
            pointsText.text = Total().ToString();
    }

    private int Total()
    {
        int total = 0;

        for (int i = 0; i < StatCount; i++)
            total += stats[i];

        return total;
    }

    private StatBaseOverride[] BuildStatOverrides()
    {
        var overrides = new StatBaseOverride[StatCount];

        for (int i = 0; i < StatCount; i++)
            overrides[i] = new StatBaseOverride { statId = StatIds[i], baseValue = stats[i] };

        return overrides;
    }

    // ── Finalise ──────────────────────────────────────────────────────────

    private void OnFinaliseClicked()
    {
        if (isBusy) return;
        _ = HandleFinalise();
    }

    private async Task HandleFinalise()
    {
        ClearFeedback();

        string characterName = nameInputField != null ? nameInputField.text.Trim() : "";
        if (!ValidateName(characterName)) return;

        SetBusy(true);
        SetFeedback("Creating character...");

        var data = new CharacterCreationData
        {
            characterName = characterName,
            accountName = accountManager.ActiveAccountName,
            modelId = defaultModelId,
            baseStats = BuildStatOverrides()
        };

        string characterId = await saveManager.CreateCharacter(data);

        SetBusy(false);

        if (string.IsNullOrEmpty(characterId))
        {
            SetFeedback("Failed to create character. Please try again.", isError: true);
            return;
        }

        Close();
    }

    // ── Cancel ────────────────────────────────────────────────────────────

    private void OnCancelClicked()
    {
        if (isBusy) return;
        Close();
    }

    private void Close()
    {
        gameObject.SetActive(false);
        onComplete?.Invoke();
    }

    // ── Validation ────────────────────────────────────────────────────────

    private bool ValidateName(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            SetFeedback("Please enter a character name.", isError: true);
            return false;
        }

        if (name.Length < minNameLength)
        {
            SetFeedback($"Name must be at least {minNameLength} characters.", isError: true);
            return false;
        }

        if (name.Length > maxNameLength)
        {
            SetFeedback($"Name must be {maxNameLength} characters or fewer.", isError: true);
            return false;
        }

        foreach (char c in name)
        {
            if (!char.IsLetterOrDigit(c) && c != ' ' && c != '-' && c != '\'')
            {
                SetFeedback("Name contains invalid characters.", isError: true);
                return false;
            }
        }

        return true;
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private void SetBusy(bool busy)
    {
        isBusy = busy;
        SetInteractable(!busy);
    }

    private void SetInteractable(bool value)
    {
        if (nameInputField != null) nameInputField.interactable = value;
        if (randomizeButton != null) randomizeButton.interactable = value;
        if (finaliseButton != null) finaliseButton.interactable = value;
        if (cancelButton != null) cancelButton.interactable = value;
    }

    private void SetFeedback(string message, bool isError = false)
    {
        if (feedbackText == null) return;
        feedbackText.text = message;
        feedbackText.color = isError ? Color.red : Color.white;
    }

    private void ClearFeedback()
    {
        if (feedbackText != null) feedbackText.text = "";
    }
}

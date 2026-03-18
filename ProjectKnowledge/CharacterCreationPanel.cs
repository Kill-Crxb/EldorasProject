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
/// Drives the character creation flow:
///   - Name entry and validation
///   - Stat allocation against a configurable point pool (self-contained,
///     no RPGSystem dependency — menu scene has no player brain)
///   - Finalise: writes seed save files via SaveManager, returns to CharacterSelectScreen
///
/// Stat allocation rules:
///   - All stats start at statFloor (inspector field)
///   - Player has totalStatPoints to distribute freely across the six stats
///   - A stat can never be reduced below statFloor
///   - Plus buttons disable when the pool is exhausted
///   - Minus buttons disable when a stat is already at the floor
///
/// Inspector wiring:
///
///   [Input Fields]
///   nameInputField        — TMP_InputField
///
///   [Stat Display]
///   mindText              — TextMeshProUGUI
///   bodyText              — TextMeshProUGUI
///   spiritText            — TextMeshProUGUI
///   resilienceText        — TextMeshProUGUI
///   enduranceText         — TextMeshProUGUI
///   insightText           — TextMeshProUGUI
///   remainingPointsText   — TextMeshProUGUI  (pool remaining)
///
///   [Stat Buttons]
///   mindPlus / mindMinus  — Button (repeat for each stat)
///   bodyPlus / bodyMinus
///   spiritPlus / spiritMinus
///   resiliencePlus / resilienceMinus
///   endurancePlus / enduranceMinus
///   insightPlus / insightMinus
///
///   [Navigation]
///   finaliseButton        — Button (write saves + return to select)
///   cancelButton          — Button (discard + return to select)
///
///   [Feedback]
///   feedbackText          — TextMeshProUGUI
///
///   [Config]
///   statFloor             — Minimum value any stat can reach (default 8)
///   totalStatPoints       — Points the player distributes at creation (default 10)
///   minNameLength         — Minimum character name length (default 2)
///   maxNameLength         — Maximum character name length (default 20)
///   defaultModelId        — Placeholder until appearance system is built
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
    [SerializeField] private TextMeshProUGUI remainingPointsText;

    [Header("Stat Buttons")]
    [SerializeField] private Button mindPlus;
    [SerializeField] private Button mindMinus;
    [SerializeField] private Button bodyPlus;
    [SerializeField] private Button bodyMinus;
    [SerializeField] private Button spiritPlus;
    [SerializeField] private Button spiritMinus;
    [SerializeField] private Button resiliencePlus;
    [SerializeField] private Button resilienceMinus;
    [SerializeField] private Button endurancePlus;
    [SerializeField] private Button enduranceMinus;
    [SerializeField] private Button insightPlus;
    [SerializeField] private Button insightMinus;

    [Header("Navigation")]
    [SerializeField] private Button finaliseButton;
    [SerializeField] private Button cancelButton;

    [Header("Feedback")]
    [SerializeField] private TextMeshProUGUI feedbackText;

    [Header("Config")]
    [Tooltip("The value all stats start at and cannot be reduced below.")]
    [Min(1)]
    [SerializeField] private int statFloor = 8;

    [Tooltip("Total points the player can freely distribute across all six stats at creation.")]
    [Min(0)]
    [SerializeField] private int totalStatPoints = 10;

    [Tooltip("Minimum character name length.")]
    [SerializeField] private int minNameLength = 2;

    [Tooltip("Maximum character name length.")]
    [SerializeField] private int maxNameLength = 20;

    [Tooltip("Model ID written to metadata. Placeholder until appearance system is built.")]
    [SerializeField] private string defaultModelId = "base_body";

    [Header("Debug")]
    [SerializeField] private bool debugLogging = false;

    // ── Stat Indices ──────────────────────────────────────────────────────

    private const int MIND = 0;
    private const int BODY = 1;
    private const int SPIRIT = 2;
    private const int RESILIENCE = 3;
    private const int ENDURANCE = 4;
    private const int INSIGHT = 5;
    private const int STAT_COUNT = 6;

    // ── State ─────────────────────────────────────────────────────────────

    private AccountManager accountManager;
    private SaveManager saveManager;

    private int[] stats = new int[STAT_COUNT];
    private int remainingPoints;
    private bool isBusy;
    private Action onComplete;

    // ── Lifecycle ─────────────────────────────────────────────────────────

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

    // ── Setup ─────────────────────────────────────────────────────────────

    private void WireButtons()
    {
        mindPlus?.onClick.AddListener(() => AdjustStat(MIND, +1));
        mindMinus?.onClick.AddListener(() => AdjustStat(MIND, -1));
        bodyPlus?.onClick.AddListener(() => AdjustStat(BODY, +1));
        bodyMinus?.onClick.AddListener(() => AdjustStat(BODY, -1));
        spiritPlus?.onClick.AddListener(() => AdjustStat(SPIRIT, +1));
        spiritMinus?.onClick.AddListener(() => AdjustStat(SPIRIT, -1));
        resiliencePlus?.onClick.AddListener(() => AdjustStat(RESILIENCE, +1));
        resilienceMinus?.onClick.AddListener(() => AdjustStat(RESILIENCE, -1));
        endurancePlus?.onClick.AddListener(() => AdjustStat(ENDURANCE, +1));
        enduranceMinus?.onClick.AddListener(() => AdjustStat(ENDURANCE, -1));
        insightPlus?.onClick.AddListener(() => AdjustStat(INSIGHT, +1));
        insightMinus?.onClick.AddListener(() => AdjustStat(INSIGHT, -1));

        finaliseButton?.onClick.AddListener(OnFinaliseClicked);
        cancelButton?.onClick.AddListener(OnCancelClicked);
    }

    private void UnwireButtons()
    {
        mindPlus?.onClick.RemoveAllListeners();
        mindMinus?.onClick.RemoveAllListeners();
        bodyPlus?.onClick.RemoveAllListeners();
        bodyMinus?.onClick.RemoveAllListeners();
        spiritPlus?.onClick.RemoveAllListeners();
        spiritMinus?.onClick.RemoveAllListeners();
        resiliencePlus?.onClick.RemoveAllListeners();
        resilienceMinus?.onClick.RemoveAllListeners();
        endurancePlus?.onClick.RemoveAllListeners();
        enduranceMinus?.onClick.RemoveAllListeners();
        insightPlus?.onClick.RemoveAllListeners();
        insightMinus?.onClick.RemoveAllListeners();

        finaliseButton?.onClick.RemoveListener(OnFinaliseClicked);
        cancelButton?.onClick.RemoveListener(OnCancelClicked);
    }

    private void ResetToDefaults()
    {
        for (int i = 0; i < STAT_COUNT; i++)
            stats[i] = statFloor;

        remainingPoints = totalStatPoints;

        if (nameInputField != null)
            nameInputField.text = "";

        ClearFeedback();
        SetInteractable(true);
        RefreshStatDisplay();
    }

    // ── Stat Allocation ───────────────────────────────────────────────────

    private void AdjustStat(int statIndex, int delta)
    {
        if (delta > 0)
        {
            if (remainingPoints <= 0) return;

            stats[statIndex] += 1;
            remainingPoints -= 1;
        }
        else
        {
            if (stats[statIndex] <= statFloor) return;

            stats[statIndex] -= 1;
            remainingPoints += 1;
        }

        RefreshStatDisplay();
    }

    private void RefreshStatDisplay()
    {
        if (mindText != null) mindText.text = stats[MIND].ToString();
        if (bodyText != null) bodyText.text = stats[BODY].ToString();
        if (spiritText != null) spiritText.text = stats[SPIRIT].ToString();
        if (resilienceText != null) resilienceText.text = stats[RESILIENCE].ToString();
        if (enduranceText != null) enduranceText.text = stats[ENDURANCE].ToString();
        if (insightText != null) insightText.text = stats[INSIGHT].ToString();

        if (remainingPointsText != null)
            remainingPointsText.text = remainingPoints.ToString();

        RefreshButtonStates();
    }

    private void RefreshButtonStates()
    {
        // Plus buttons — disabled when the pool is exhausted
        bool canAdd = remainingPoints > 0;
        if (mindPlus != null) mindPlus.interactable = canAdd;
        if (bodyPlus != null) bodyPlus.interactable = canAdd;
        if (spiritPlus != null) spiritPlus.interactable = canAdd;
        if (resiliencePlus != null) resiliencePlus.interactable = canAdd;
        if (endurancePlus != null) endurancePlus.interactable = canAdd;
        if (insightPlus != null) insightPlus.interactable = canAdd;

        // Minus buttons — disabled when stat is at the floor
        if (mindMinus != null) mindMinus.interactable = stats[MIND] > statFloor;
        if (bodyMinus != null) bodyMinus.interactable = stats[BODY] > statFloor;
        if (spiritMinus != null) spiritMinus.interactable = stats[SPIRIT] > statFloor;
        if (resilienceMinus != null) resilienceMinus.interactable = stats[RESILIENCE] > statFloor;
        if (enduranceMinus != null) enduranceMinus.interactable = stats[ENDURANCE] > statFloor;
        if (insightMinus != null) insightMinus.interactable = stats[INSIGHT] > statFloor;
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
            originId = string.Empty,
            mind = stats[MIND],
            body = stats[BODY],
            spirit = stats[SPIRIT],
            resilience = stats[RESILIENCE],
            endurance = stats[ENDURANCE],
            insight = stats[INSIGHT],
            unspentPoints = remainingPoints
        };

        if (debugLogging)
            Debug.Log($"[CharacterCreationPanel] Finalising: {data.characterName} | " +
                      $"Stats: M{data.mind} B{data.body} Sp{data.spirit} " +
                      $"R{data.resilience} E{data.endurance} I{data.insight} | " +
                      $"Unspent: {data.unspentPoints}");

        string characterId = await saveManager.CreateCharacter(data);

        SetBusy(false);

        if (string.IsNullOrEmpty(characterId))
        {
            SetFeedback("Failed to create character. Please try again.", isError: true);
            return;
        }

        if (debugLogging)
            Debug.Log($"[CharacterCreationPanel] Character created: {characterId}");

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
        if (finaliseButton != null) finaliseButton.interactable = value;
        if (cancelButton != null) cancelButton.interactable = value;

        if (value)
            RefreshButtonStates(); // Restore pool/floor-aware states
        else
            SetAllStatButtonsInteractable(false);
    }

    private void SetAllStatButtonsInteractable(bool value)
    {
        Button[] buttons =
        {
            mindPlus, mindMinus, bodyPlus, bodyMinus,
            spiritPlus, spiritMinus, resiliencePlus, resilienceMinus,
            endurancePlus, enduranceMinus, insightPlus, insightMinus
        };

        foreach (var b in buttons)
            if (b != null) b.interactable = value;
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
using System.Collections.Generic;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// CharacterSelectScreen
///
/// Attach to: CharacterList GameObject
///
/// Inspector wiring:
///
///   [Character List]
///   Character List Container  — Transform cards spawn into (vertical layout group)
///   Character Slot Prefab     — CharacterSaveCard prefab
///
///   [Character Menu]
///   New Character Button      — New button (loads new character creation panel)
///   Delete Button             — Del button (deletes currently selected character)
///
///   [Main Buttons]
///   Play Button               — JoinGameButton
///
///   [Feedback]
///   Feedback Text             — TextMeshProUGUI
/// </summary>
public class CharacterSelectScreen : MonoBehaviour
{
    [Header("Character List")]
    [SerializeField] private Transform characterListContainer;
    [SerializeField] private GameObject characterSlotPrefab;

    [Header("Character Menu")]
    [SerializeField] private Button newCharacterButton;
    [SerializeField] private Button deleteButton;

    [Header("Main Buttons")]
    [SerializeField] private Button playButton;

    [Header("Feedback")]
    [SerializeField] private TextMeshProUGUI feedbackText;

    [Header("Character Creation")]
    [Tooltip("The CharacterCreationPanel — activated when New is clicked, deactivated on confirm/cancel.")]
    [SerializeField] private CharacterCreationPanel characterCreationPanel;

    [Header("Debug")]
    [SerializeField] private bool debugLogging = false;

    private AccountManager accountManager;
    private SaveManager saveManager;
    private List<CharacterMetadata> characters = new();
    private CharacterSlotUI selectedSlot;
    private CharacterMetadata selectedCharacter;
    private bool isBusy = false;
    private bool suppressNextRefresh = false;

    // ── Lifecycle ─────────────────────────────────────────────────────────

    void OnEnable()
    {
        accountManager = ManagerBrain.Instance?.GetManager<AccountManager>();
        saveManager = ManagerBrain.Instance?.GetManager<SaveManager>();

        if (accountManager == null || saveManager == null)
        {
            SetFeedback("Error: Managers not found.", isError: true);
            return;
        }

        newCharacterButton?.onClick.AddListener(OnNewClicked);
        deleteButton?.onClick.AddListener(OnDeleteClicked);
        playButton?.onClick.AddListener(OnPlayClicked);

        SetPlayInteractable(false);
        SetDeleteInteractable(false);

        if (suppressNextRefresh)
        {
            suppressNextRefresh = false;
            return;
        }

        _ = RefreshCharacterList();
    }

    void OnDisable()
    {
        newCharacterButton?.onClick.RemoveListener(OnNewClicked);
        deleteButton?.onClick.RemoveListener(OnDeleteClicked);
        playButton?.onClick.RemoveListener(OnPlayClicked);
    }

    // ── Character List ────────────────────────────────────────────────────

    private async Task RefreshCharacterList()
    {
        SetBusy(true);
        ClearList();

        characters = await saveManager.GetAllCharacters();

        foreach (var metadata in characters)
            SpawnSlot(metadata);

        // Force layout rebuild — ContentSizeFitter and VerticalLayoutGroup don't
        // recalculate automatically after runtime instantiation.
        if (characterListContainer != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(
                characterListContainer as RectTransform);
        }

        SetBusy(false);
        ClearFeedback();

        if (debugLogging)
            Debug.Log($"[CharacterSelectScreen] Loaded {characters.Count} character(s)");
    }

    private void SpawnSlot(CharacterMetadata metadata)
    {
        if (characterSlotPrefab == null || characterListContainer == null) return;

        GameObject go = Instantiate(characterSlotPrefab, characterListContainer);
        var slot = go.GetComponent<CharacterSlotUI>();

        if (slot == null)
        {
            Debug.LogError("[CharacterSelectScreen] CharacterSaveCard prefab is missing CharacterSlotUI!");
            Destroy(go);
            return;
        }

        slot.Setup(metadata, OnSlotSelected);
    }

    private void ClearList()
    {
        if (characterListContainer == null) return;

        foreach (Transform child in characterListContainer)
            Destroy(child.gameObject);

        selectedSlot = null;
        selectedCharacter = null;
        SetPlayInteractable(false);
        SetDeleteInteractable(false);
    }

    // ── Selection ─────────────────────────────────────────────────────────

    private void OnSlotSelected(CharacterSlotUI slot, CharacterMetadata metadata)
    {
        selectedSlot?.SetSelected(false);

        selectedSlot = slot;
        selectedCharacter = metadata;

        slot.SetSelected(true);
        SetPlayInteractable(true);
        SetDeleteInteractable(true);
        ClearFeedback();

        if (debugLogging)
            Debug.Log($"[CharacterSelectScreen] Selected: {metadata.characterName}");
    }

    // ── Play ──────────────────────────────────────────────────────────────

    private void OnPlayClicked()
    {
        if (isBusy || selectedCharacter == null) return;

        if (!accountManager.IsLoggedIn)
        {
            SetFeedback("You must be logged in to play.", isError: true);
            return;
        }

        if (debugLogging)
            Debug.Log($"[CharacterSelectScreen] Playing: {selectedCharacter.characterName}");

        SceneLoader.Instance?.LoadGameScene(selectedCharacter.characterId);
    }

    // ── New ───────────────────────────────────────────────────────────────

    private void OnNewClicked()
    {
        if (isBusy) return;

        if (characterCreationPanel == null)
        {
            Debug.LogError("[CharacterSelectScreen] CharacterCreationPanel not assigned!");
            return;
        }

        if (debugLogging)
            Debug.Log("[CharacterSelectScreen] Opening character creation panel");

        // Suppress the OnEnable refresh that fires when this panel reactivates —
        // OnCreationComplete handles the refresh explicitly after creation.
        suppressNextRefresh = true;
        gameObject.SetActive(false);

        characterCreationPanel.Open(OnCreationComplete);
    }

    private void OnCreationComplete()
    {
        gameObject.SetActive(true);
        _ = RefreshCharacterList();

        if (debugLogging)
            Debug.Log("[CharacterSelectScreen] Returned from character creation");
    }

    // ── Delete ────────────────────────────────────────────────────────────

    private void OnDeleteClicked()
    {
        if (isBusy || selectedCharacter == null) return;
        _ = HandleDelete(selectedCharacter);
    }

    private async Task HandleDelete(CharacterMetadata metadata)
    {
        SetBusy(true);
        SetFeedback($"Deleting {metadata.characterName}...");

        bool success = await accountManager.SaveProvider.DeleteCharacter(metadata.characterId);

        SetBusy(false);

        if (success)
        {
            if (debugLogging)
                Debug.Log($"[CharacterSelectScreen] Deleted: {metadata.characterName}");

            await RefreshCharacterList();
        }
        else
        {
            SetFeedback($"Failed to delete {metadata.characterName}.", isError: true);
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private void SetBusy(bool busy)
    {
        isBusy = busy;
        if (newCharacterButton != null) newCharacterButton.interactable = !busy;
        SetPlayInteractable(!busy && selectedCharacter != null);
        SetDeleteInteractable(!busy && selectedCharacter != null);
    }

    private void SetPlayInteractable(bool value)
    {
        if (playButton != null) playButton.interactable = value;
    }

    private void SetDeleteInteractable(bool value)
    {
        if (deleteButton != null) deleteButton.interactable = value;
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
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// CharacterSlotUI
///
/// Attach to: CharacterSaveCard prefab root (which also has a Button component)
///
/// Inspector wiring:
///   nameText          — TextMeshProUGUI  (character name)
///   levelText         — TextMeshProUGUI  (e.g. "Lvl 1")
///   creatorText       — TextMeshProUGUI  (account name)
///   selectButton      — Button on this same GameObject
///   selectedIndicator — GameObject (highlight, starts inactive)
/// </summary>
public class CharacterSlotUI : MonoBehaviour
{
    [Header("Display")]
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI levelText;
    [SerializeField] private TextMeshProUGUI creatorText;

    [Header("Interaction")]
    [SerializeField] private Button selectButton;

    [Header("Visual")]
    [SerializeField] private GameObject selectedIndicator;

    private CharacterMetadata metadata;
    private Action<CharacterSlotUI, CharacterMetadata> onSelected;

    public CharacterMetadata Metadata => metadata;

    public void Setup(
        CharacterMetadata data,
        Action<CharacterSlotUI, CharacterMetadata> selectCallback)
    {
        metadata = data;
        onSelected = selectCallback;

        if (nameText != null) nameText.text = data.characterName;
        if (levelText != null) levelText.text = $"Lvl {data.level}";
        if (creatorText != null) creatorText.text = data.accountName;

        SetSelected(false);

        selectButton?.onClick.AddListener(OnSelectClicked);
    }

    void OnDestroy()
    {
        selectButton?.onClick.RemoveListener(OnSelectClicked);
    }

    public void SetSelected(bool selected)
    {
        if (selectedIndicator != null)
            selectedIndicator.SetActive(selected);
    }

    private void OnSelectClicked()
    {
        onSelected?.Invoke(this, metadata);
    }
}
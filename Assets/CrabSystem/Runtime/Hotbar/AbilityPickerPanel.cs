using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// Toggle panel listing available AbilitySlotData SOs for drag-to-hotbar assignment.
/// Also surfaces raw AbilityDefinitions from RuntimeAbilityManager for abilities not
/// yet wrapped in an AbilitySlotData asset.
///
/// No transient ScriptableObjects are created — AbilityPickerEntry carries
/// SlotData (nullable) or AbilityDef directly.
/// </summary>
public class AbilityPickerPanel : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private RectTransform entryContainer;
    [SerializeField] private GameObject entryPrefab;
    [SerializeField] private ScrollRect scrollRect;

    [Header("Static Slot List")]
    [Tooltip("Drag AbilitySlotData SOs here. Supplemented at runtime by RuntimeAbilityManager.")]
    [SerializeField] private List<AbilitySlotData> staticSlotList = new List<AbilitySlotData>();

    private HotbarSystem hotbarSystem;
    private string targetBarId;
    private bool isOpen;

    private readonly List<AbilityPickerEntry> spawnedEntries = new List<AbilityPickerEntry>();

    void Awake() => gameObject.SetActive(false);

    // ── API ──────────────────────────────────────────────────────────────

    public void Toggle(HotbarSystem hotbar, string barId)
    {
        if (isOpen) Close(); else Open(hotbar, barId);
    }

    public void Open(HotbarSystem hotbar, string barId)
    {
        hotbarSystem = hotbar;
        targetBarId = barId;
        isOpen = true;
        gameObject.SetActive(true);
        Refresh();
    }

    public void Close()
    {
        isOpen = false;
        gameObject.SetActive(false);
    }

    // ── Build ─────────────────────────────────────────────────────────────

    private void Refresh()
    {
        ClearEntries();

        // SO-backed slots from the static list
        foreach (var slot in staticSlotList)
        {
            if (slot == null) continue;
            SpawnEntry(slot, null);
        }

        // Raw abilities from RuntimeAbilityManager not covered by any static slot
        var playerBrain = ManagerBrain.Instance?.GetManager<SaveManager>()?.PlayerBrain;
        var ram = playerBrain?.GetModule<RuntimeAbilityManager>();

        if (ram != null)
        {
            foreach (var instance in ram.GetAllInstances())
            {
                if (instance?.definition == null) continue;

                bool alreadyCovered = staticSlotList.Exists(s =>
                    s != null &&
                    s.abilityChain != null &&
                    s.abilityChain.Length == 1 &&
                    s.abilityChain[0] == instance.definition);

                if (!alreadyCovered)
                    SpawnEntry(null, instance.definition);
            }
        }
    }

    private void SpawnEntry(AbilitySlotData slotData, AbilityDefinition abilityDef)
    {
        if (entryContainer == null) return;

        GameObject go;
        if (entryPrefab != null)
        {
            go = Instantiate(entryPrefab, entryContainer);
        }
        else
        {
            go = new GameObject("PickerEntry");
            go.transform.SetParent(entryContainer, false);
            var rt = go.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(60, 60);
            go.AddComponent<Image>().color = new Color(0.2f, 0.2f, 0.2f, 0.9f);
        }

        var entry = go.GetComponent<AbilityPickerEntry>() ?? go.AddComponent<AbilityPickerEntry>();
        entry.Initialize(slotData, abilityDef);
        spawnedEntries.Add(entry);
    }

    private void ClearEntries()
    {
        foreach (var e in spawnedEntries)
            if (e != null) Destroy(e.gameObject);
        spawnedEntries.Clear();
    }
}

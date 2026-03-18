using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class EquipmentWindow : UIWindow
{
    [Header("UI Containers")]
    [SerializeField] private Transform leftEquipContainer;
    [SerializeField] private Transform rightEquipContainer;
    [SerializeField] private Transform leftWeaponContainer;
    [SerializeField] private Transform rightWeaponContainer;

    [Header("Info Panel")]
    [SerializeField] private TextMeshProUGUI playerNameText;
    [SerializeField] private TextMeshProUGUI factionNameText;
    [SerializeField] private TextMeshProUGUI levelGoldText;

    [Header("Prefabs")]
    [SerializeField] private GameObject equipmentSlotPrefab;

    [Header("Slot Configuration")]
    [SerializeField] private EquipmentSlotConfig[] leftPanelSlots = new EquipmentSlotConfig[4];
    [SerializeField] private EquipmentSlotConfig[] rightPanelSlots = new EquipmentSlotConfig[4];
    [SerializeField] private EquipmentSlotConfig[] weaponSlots = new EquipmentSlotConfig[2];

    private EquipmentSystem equipmentSystem;
    private IdentitySystem identitySystem;
    private PlayerInfoModule playerInfo;
    private readonly Dictionary<string, EquipmentSlotVisual_Simple> slotVisuals = new Dictionary<string, EquipmentSlotVisual_Simple>();

    protected override void SetupWindow()
    {
        if (playerBrain == null) { Debug.LogError("[EquipmentWindow] No player brain!"); return; }

        equipmentSystem = playerBrain.GetModule<EquipmentSystem>();
        identitySystem = playerBrain.GetModule<IdentitySystem>();
        playerInfo = playerBrain.GetModule<PlayerInfoModule>();

        if (equipmentSystem == null) { Debug.LogError("[EquipmentWindow] No EquipmentSystem on player!"); return; }

        ValidateSlotConfigurations();
        CreateSlots();
        UpdateInfoPanel();
    }

    public override void OnClose() => base.OnClose();

    private void ValidateSlotConfigurations()
    {
        var usedIds = new HashSet<string>();
        ValidateSlotArray(leftPanelSlots, "Left Panel", usedIds);
        ValidateSlotArray(rightPanelSlots, "Right Panel", usedIds);
        ValidateSlotArray(weaponSlots, "Weapon", usedIds);
    }

    private void ValidateSlotArray(EquipmentSlotConfig[] slots, string panelName, HashSet<string> usedIds)
    {
        if (slots == null) return;

        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] == null || !slots[i].IsValid()) continue;

            string id = slots[i].SlotId;
            if (!usedIds.Add(id))
                Debug.LogError($"[EquipmentWindow] Duplicate slot ID '{id}' in {panelName}!");
        }
    }

    private void CreateSlots()
    {
        CreateSlotsFromArray(leftPanelSlots, leftEquipContainer, "Left Panel");
        CreateSlotsFromArray(rightPanelSlots, rightEquipContainer, "Right Panel");
        CreateWeaponSlot(0, leftWeaponContainer);
        CreateWeaponSlot(1, rightWeaponContainer);
    }

    private void CreateSlotsFromArray(EquipmentSlotConfig[] slots, Transform container, string panelName)
    {
        if (container == null) { Debug.LogWarning($"[EquipmentWindow] {panelName} container not assigned!"); return; }
        if (slots == null || slots.Length == 0) { Debug.LogWarning($"[EquipmentWindow] {panelName} has no slot configs!"); return; }

        foreach (var config in slots)
        {
            if (config != null && config.IsValid())
                CreateSlot(container, config);
        }
    }

    private void CreateWeaponSlot(int index, Transform container)
    {
        if (container == null) { Debug.LogWarning($"[EquipmentWindow] Weapon container {index} not assigned!"); return; }
        if (weaponSlots == null || index >= weaponSlots.Length || weaponSlots[index] == null) { Debug.LogWarning($"[EquipmentWindow] Weapon slot {index} not configured!"); return; }

        CreateSlot(container, weaponSlots[index]);
    }

    private void CreateSlot(Transform container, EquipmentSlotConfig config)
    {
        var slotObj = Instantiate(equipmentSlotPrefab, container);
        slotObj.name = $"Slot_{config.SlotId}";

        var slotVisual = slotObj.GetComponent<EquipmentSlotVisual_Simple>();
        if (slotVisual == null)
        {
            Debug.LogError("[EquipmentWindow] Slot prefab missing EquipmentSlotVisual_Simple!");
            Destroy(slotObj);
            return;
        }

        slotVisual.Initialize(config, equipmentSystem);
        slotVisuals[config.SlotId] = slotVisual;
    }

    private void UpdateInfoPanel()
    {
        if (playerNameText != null && identitySystem != null)
            playerNameText.text = identitySystem.GetEntityName();

        if (factionNameText != null && playerInfo != null)
            factionNameText.text = playerInfo.GetPlayerFactionName();

        if (levelGoldText != null)
            levelGoldText.text = playerInfo != null ? $"LVL {playerInfo.GetCharacterLevel()}  0G" : "LVL 1  0G";
    }

    public int SlotCount => slotVisuals.Count;

    [ContextMenu("Debug: Print Slot Configuration")]
    private void DebugPrintSlotConfiguration()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("[EquipmentWindow] Slot Configuration:");
        AppendSlotArray(sb, "Left", leftPanelSlots);
        AppendSlotArray(sb, "Right", rightPanelSlots);
        AppendSlotArray(sb, "Weapons", weaponSlots);
        Debug.Log(sb.ToString());
    }

    private void AppendSlotArray(System.Text.StringBuilder sb, string label, EquipmentSlotConfig[] slots)
    {
        sb.Append($"  {label}: ");
        if (slots == null || slots.Length == 0) { sb.AppendLine("(none)"); return; }
        sb.AppendLine(string.Join(", ", System.Array.ConvertAll(slots, s => s != null ? s.SlotId : "null")));
    }
}
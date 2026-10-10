using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// HotbarSystem — owns the entity's action bars, one set of contents per page.
///
/// Bars are ActionBarDefinition assets. Nothing in this class names a bar: "centre", "mouse" and
/// "modifier" are ids on assets, and adding a bar is creating one asset and dropping it in
/// availableBars.
///
/// Two independent axes, deliberately not merged:
///   availableBars  — which bars this entity CAN have.            Authored on the prefab.
///   activeBarIds   — which of them the player HAS switched on.   Saved per character.
///   pages          — which CONTENTS a bar shows right now.       Claimed by a blackboard fact.
///
/// A page swaps what is in a bar. Activation decides whether the bar exists at all. Implementing
/// one with the other is how they end up fighting.
/// </summary>
public class HotbarSystem : MonoBehaviour, IBrainModule, ISaveable
{
    public int InitOrder => 170;

    [Header("Bars")]
    [Tooltip("Every bar this entity can have. Which of them are switched on is per-character " +
             "save data, not an authored flag — see ActionBarDefinition.defaultActive.")]
    [SerializeField] private List<ActionBarDefinition> availableBars = new List<ActionBarDefinition>();

    [Header("Pages")]
    [Tooltip("Every page this entity can use. One of them should have an empty activationFact " +
             "to act as the default. Leave the list empty and a default page is synthesised.")]
    [SerializeField] private List<HotbarPageDefinition> pageDefinitions = new List<HotbarPageDefinition>();

    [Tooltip("Seconds between activation-fact checks. Matches the SemanticBridgeSystem pulse.")]
    [SerializeField] private float pageCheckInterval = 0.1f;

    [Header("Moveset")]
    [Tooltip("Bar and slot the weapon moveset owns. While the entity has a MovesetModule the slot " +
             "can't be assigned, and pressing it fires the moveset (Moveset_Build.md).")]
    [SerializeField] private string movesetBarId = "mouse";
    [SerializeField] private int movesetSlotIndex = 0;
    [SerializeField] private int blockSlotIndex = 1;

    [Header("Debug")]
    [SerializeField] private bool debugLogging = false;

    private ControllerBrain brain;
    private AbilitySystem abilitySystem;

    // A slot pressed while a hit state holds the player, fired on the first free frame instead of dropped. One is
    // held; the latest press wins.
    private AbilityDefinition heldPress;
    private SlotTransformationSystem transformSystem;
    private MovesetModule movesetModule;
    private Blackboard blackboard;

    private readonly List<HotbarPageState> pages = new List<HotbarPageState>();
    private readonly List<string> activeBarIds = new List<string>();
    private bool hasChosenBars;

    private HotbarPageDefinition defaultPage;
    private HotbarPageDefinition activePage;
    private float nextPageCheck;

    public event Action<string, int> OnSlotChanged;

    /// <summary>Fires when the active page changes. Views must rebuild — slot counts can differ.</summary>
    public event Action<string> OnPageChanged;

    /// <summary>Fires when a bar is switched on or off. HotbarHud rebuilds the screen.</summary>
    public event Action OnActiveBarsChanged;

    public bool IsEnabled { get; set; } = true;

    public ControllerBrain Brain => brain;
    public string ActivePageId => activePage != null ? activePage.pageId : "";
    public HotbarPageDefinition ActivePage => activePage;

    #region IBrainModule

    public void Initialize(ControllerBrain controllerBrain)
    {
        brain = controllerBrain;
        abilitySystem = brain.GetModule<AbilitySystem>();

        WarnOnBadBars();
        ResolveDefaultPage();
        BuildMissingPageStates();
        ApplyDefaultActiveBars();

        activePage = defaultPage;
    }

    public void LateInitialize()
    {
        if (abilitySystem == null) abilitySystem = brain.GetModule<AbilitySystem>();

        transformSystem = brain.GetModule<SlotTransformationSystem>();
        movesetModule = brain.GetModule<MovesetModule>();
        if (movesetModule != null) movesetModule.OnPreviewChanged += HandleMovesetPreviewChanged;
        blackboard = brain.Blackboard;

        nextPageCheck = Time.time + pageCheckInterval;
    }

    public void UpdateModule()
    {
        if (!IsEnabled) return;

        FireHeldPress();
        if (Time.time < nextPageCheck) return;

        nextPageCheck = Time.time + pageCheckInterval;
        RefreshActivePage();
    }

    #endregion

    #region Bars

    /// <summary>
    /// A bar cannot be synthesised — an ActionBarDefinition is an asset — so an empty or broken
    /// list is reported rather than papered over. An entity with no bars is legitimate for an NPC.
    /// </summary>
    private void WarnOnBadBars()
    {
        if (availableBars == null) availableBars = new List<ActionBarDefinition>();

        for (int i = 0; i < availableBars.Count; i++)
        {
            var def = availableBars[i];

            if (def == null)
            {
                Debug.LogError($"[HotbarSystem] {name} availableBars[{i}] is empty. That slot will " +
                               $"never produce a bar.");
                continue;
            }

            if (!def.IsValid())
                Debug.LogError($"[HotbarSystem] {name}: bar asset '{def.name}' has no barId. Its " +
                               $"contents cannot be saved or loaded.");
        }
    }

    public List<ActionBarDefinition> GetAvailableBars() => availableBars;

    public ActionBarDefinition FindBar(string barId)
    {
        if (string.IsNullOrEmpty(barId)) return null;

        foreach (var def in availableBars)
        {
            if (def != null && def.barId == barId) return def;
        }

        return null;
    }

    public bool HasBar(string barId) => FindBar(barId) != null;

    #endregion

    #region Activation

    public bool IsBarActive(string barId)
        => !string.IsNullOrEmpty(barId) && activeBarIds.Contains(barId);

    public IReadOnlyList<string> GetActiveBarIds() => activeBarIds;

    /// <summary>
    /// Switches a bar on or off. Contents are untouched either way — turning a bar back on
    /// restores whatever the player had arranged on it, which is why this never clears a page.
    /// </summary>
    public void SetBarActive(string barId, bool active)
    {
        var def = FindBar(barId);
        if (def == null) return;

        if (!active && def.alwaysOn)
        {
            if (debugLogging)
                Debug.Log($"[HotbarSystem] '{barId}' is alwaysOn and cannot be switched off.");
            return;
        }

        bool changed = active ? AddActive(barId) : activeBarIds.Remove(barId);
        if (!changed) return;

        hasChosenBars = true;
        OnActiveBarsChanged?.Invoke();

        if (debugLogging)
            Debug.Log($"[HotbarSystem] Bar '{barId}' {(active ? "on" : "off")}.");
    }

    private bool AddActive(string barId)
    {
        if (activeBarIds.Contains(barId)) return false;

        activeBarIds.Add(barId);
        return true;
    }

    /// <summary>
    /// Fills the active list from each bar's defaultActive, for a fresh character or a save
    /// written before the player could choose. Also forces alwaysOn bars in every time, so a
    /// bar that became mandatory after a save was written still appears.
    /// </summary>
    private void ApplyDefaultActiveBars()
    {
        if (!hasChosenBars)
        {
            activeBarIds.Clear();

            foreach (var def in availableBars)
            {
                if (def == null || !def.IsValid()) continue;
                if (!def.defaultActive) continue;

                AddActive(def.barId);
            }
        }

        foreach (var def in availableBars)
        {
            if (def == null || !def.IsValid() || !def.alwaysOn) continue;
            AddActive(def.barId);
        }
    }

    #endregion

    #region Pages

    /// <summary>
    /// Re-evaluates which page should be active and switches if it changed.
    /// Called on the pulse; call it directly after writing a fact for an immediate swap.
    /// </summary>
    public void RefreshActivePage()
    {
        var winner = PickPage();
        if (winner == activePage) return;

        activePage = winner;

        // Overrides are keyed barId:index with no page in the key, so a hit proc earned on the
        // armed page would otherwise land on whatever now occupies that slot.
        transformSystem?.ClearAllOverrides();

        NotifyAllBarsChanged();
        OnPageChanged?.Invoke(ActivePageId);

        if (debugLogging)
            Debug.Log($"[HotbarSystem] Page → {ActivePageId}");
    }

    private HotbarPageDefinition PickPage()
    {
        if (blackboard == null) return defaultPage;

        HotbarPageDefinition best = defaultPage;
        int bestPriority = int.MinValue;

        foreach (var def in pageDefinitions)
        {
            if (def == null) continue;
            if (def.IsDefault) continue;
            if (def.priority <= bestPriority) continue;
            if (!blackboard.GetBool(def.activationFact.GetHashCode())) continue;

            best = def;
            bestPriority = def.priority;
        }

        return best;
    }

    private void ResolveDefaultPage()
    {
        foreach (var def in pageDefinitions)
        {
            if (def == null || !def.IsDefault) continue;

            defaultPage = def;
            return;
        }

        defaultPage = ScriptableObject.CreateInstance<HotbarPageDefinition>();
        defaultPage.pageId = "default";
        defaultPage.displayName = "Default";
    }

    private void BuildMissingPageStates()
    {
        EnsurePageState(defaultPage);

        foreach (var def in pageDefinitions)
        {
            if (def == null || string.IsNullOrEmpty(def.pageId)) continue;
            EnsurePageState(def);
        }
    }

    private HotbarPageState EnsurePageState(HotbarPageDefinition def)
    {
        if (def == null) return null;

        var existing = FindPageState(def.pageId);
        if (existing != null)
        {
            EnsureBarsIn(existing);
            return existing;
        }

        var state = new HotbarPageState { pageId = def.pageId };
        EnsureBarsIn(state);

        pages.Add(state);
        return state;
    }

    /// <summary>
    /// Adds a config for any bar the page is missing. This is what lets a bar added after a save
    /// was written appear empty rather than not at all.
    /// </summary>
    private void EnsureBarsIn(HotbarPageState state)
    {
        if (state.bars == null) state.bars = new List<ActionBarConfig>();

        foreach (var def in availableBars)
        {
            if (def == null || !def.IsValid()) continue;
            if (state.Find(def.barId) != null) continue;

            state.bars.Add(new ActionBarConfig(def.barId, def.slots, def.rows));
        }
    }

    private HotbarPageState FindPageState(string pageId)
    {
        if (string.IsNullOrEmpty(pageId)) return null;

        foreach (var state in pages)
        {
            if (state != null && state.pageId == pageId) return state;
        }

        return null;
    }

    public List<HotbarPageDefinition> GetPageDefinitions() => pageDefinitions;

    #endregion

    #region Slot Access

    public void AssignSlot(string barId, int index, string abilitySlotId)
    {
        if (IsMovesetSlot(barId, index)) return;
        var config = ResolveBar(barId);
        if (config == null || !IsValidIndex(index, config)) return;

        config.slots[index].abilitySlotId = abilitySlotId ?? "";
        OnSlotChanged?.Invoke(barId, index);
    }

    public void AssignSlot(string barId, int index, AbilityDefinition ability)
    {
        AssignSlot(barId, index, ability != null ? $"ability:{ability.abilityId}" : "");
    }

    public void AssignItemSlot(string barId, int index, string itemInstanceId)
    {
        if (IsMovesetSlot(barId, index)) return;
        var config = ResolveBar(barId);
        if (config == null || !IsValidIndex(index, config)) return;

        config.slots[index].abilitySlotId = "";
        config.slots[index].itemInstanceId = itemInstanceId ?? "";
        OnSlotChanged?.Invoke(barId, index);
    }

    public void AssignItemSlot(string barId, int index, ItemInstance item)
    {
        AssignItemSlot(barId, index, item?.instanceId ?? "");
    }

    public void ClearSlot(string barId, int index)
    {
        if (IsMovesetSlot(barId, index)) return;
        var config = ResolveBar(barId);
        if (config == null || !IsValidIndex(index, config)) return;

        config.slots[index].abilitySlotId = "";
        config.slots[index].itemInstanceId = "";
        OnSlotChanged?.Invoke(barId, index);
    }

    public ActionBarSlotData GetSlot(string barId, int index)
        => ResolveBar(barId)?.GetSlot(index);

    public ActionBarConfig GetConfig(string barId)
        => ResolveBar(barId);

    private void HandleMovesetPreviewChanged()
    {
        OnSlotChanged?.Invoke(movesetBarId, movesetSlotIndex);
        OnSlotChanged?.Invoke(movesetBarId, blockSlotIndex);
    }

    private void OnDestroy()
    {
        if (movesetModule != null) movesetModule.OnPreviewChanged -= HandleMovesetPreviewChanged;
    }

    public bool IsMovesetSlot(string barId, int index)
        => movesetModule != null && barId == movesetBarId && (index == movesetSlotIndex || index == blockSlotIndex);

    public AbilityDefinition ResolveSlotAbility(ActionBarSlotData slot)
    {
        // Compared by reference: a slot loaded from an older save can carry a stale barId / index.
        if (movesetModule != null && slot != null && slot == GetSlot(movesetBarId, movesetSlotIndex)) return movesetModule.PeekNext();
        if (movesetModule != null && slot != null && slot == GetSlot(movesetBarId, blockSlotIndex)) return movesetModule.PeekBlock();
        if (slot == null || !slot.IsAssigned) return null;
        if (!slot.abilitySlotId.StartsWith("ability:")) return null;

        string abilityId = slot.abilitySlotId.Substring("ability:".Length);
        return abilitySystem?.GetAbility(abilityId);
    }

    /// <summary>Searches the bars as currently resolved — active page plus default fallthrough.</summary>
    public (string barId, int index) FindSlotForAbility(string abilityId)
    {
        foreach (var (id, config) in AllBars())
        {
            if (config == null) continue;

            for (int i = 0; i < config.slots.Count; i++)
            {
                if (DoesSlotContainAbility(config.slots[i], abilityId))
                    return (id, i);
            }
        }

        return (null, -1);
    }

    public void ResizeBar(string barId, int slotCount, int rowCount)
    {
        ResolveBar(barId)?.Resize(slotCount, rowCount);
    }

    public void TriggerSlot(string barId, int index)
    {
        if (abilitySystem == null) return;

        if (IsMovesetSlot(barId, index))
        {
            if (transformSystem != null && transformSystem.HasOverride(barId, index))
                TriggerOverride(barId, index);
            else if (index == blockSlotIndex)
                movesetModule.PressBlock();
            else
                movesetModule.Press();
            return;
        }

        var slot = GetSlot(barId, index);
        if (slot == null || !slot.IsAssigned) return;

        if (slot.HasItem)
        {
            Debug.LogWarning($"[HotbarSystem] Item use not yet implemented: {slot.itemInstanceId}");
            return;
        }

        if (transformSystem != null && transformSystem.HasOverride(barId, index))
        {
            TriggerOverride(barId, index);
            return;
        }

        var ability = ResolveSlotAbility(slot);
        if (ability == null) return;

        if (Stunned())
        {
            heldPress = ability;
            return;
        }

        abilitySystem.UseAbility(ability.abilityId);
    }

    private void FireHeldPress()
    {
        if (heldPress == null || Stunned()) return;

        AbilityDefinition press = heldPress;
        heldPress = null;
        if (brain.Damage != null && brain.Damage.IsDead) return;

        abilitySystem.UseAbility(press.abilityId);
    }

    private bool Stunned() => blackboard != null && blackboard.GetBool(BlackboardKey.CannotAct);

    #endregion

    #region ISaveable

    public string GetSaveId() => "hotbar";
    public int GetSaveVersion() => HotbarSaveData.CurrentVersion;
    public int LoadOrder => 60;

    public string GetSaveData()
    {
        var data = new HotbarSaveData
        {
            version = HotbarSaveData.CurrentVersion,
            pages = pages,
            activeBarIds = new List<string>(activeBarIds),
            hasChosenBars = hasChosenBars,
        };
        return JsonUtility.ToJson(data);
    }

    public void LoadSaveData(string json)
    {
        if (string.IsNullOrEmpty(json)) return;

        var data = JsonUtility.FromJson<HotbarSaveData>(json);
        if (data == null) return;

        pages.Clear();

        if (data.pages != null && data.pages.Count > 0)
            pages.AddRange(data.pages);
        else
            MigrateLegacyBars(data);

        // v2 pages carry their bars as three named fields; fold them into the list.
        foreach (var state in pages)
            state?.MigrateNamedBars();

        // Pages and bars authored since this save was written have no stored state yet.
        BuildMissingPageStates();

        activeBarIds.Clear();
        hasChosenBars = data.hasChosenBars;

        if (data.activeBarIds != null)
        {
            // A bar removed from availableBars since the save is dropped rather than kept as a
            // ghost that nothing can render.
            foreach (string barId in data.activeBarIds)
            {
                if (HasBar(barId)) AddActive(barId);
            }
        }

        ApplyDefaultActiveBars();

        activePage = defaultPage;
        NotifyAllBarsChanged();
        OnPageChanged?.Invoke(ActivePageId);
        OnActiveBarsChanged?.Invoke();
    }

    /// <summary>
    /// v1 saves stored the three bars at the root. Fold them into the default page so the
    /// player's arrangement survives the format change.
    /// </summary>
    private void MigrateLegacyBars(HotbarSaveData data)
    {
        var state = new HotbarPageState
        {
            pageId = defaultPage != null ? defaultPage.pageId : "default",
            centreBar = data.centreBar,
            bottomLeftBar = data.bottomLeftBar,
            bottomRightBar = data.bottomRightBar,
        };

        state.MigrateNamedBars();
        if (state.bars.Count == 0) return;

        pages.Add(state);

        if (debugLogging)
            Debug.Log("[HotbarSystem] Migrated v1 bars into the default page.");
    }

    #endregion

    #region Resolution

    /// <summary>
    /// The config a bar currently reads and writes: the active page's copy when that page
    /// supplies the bar, otherwise the default page's.
    /// </summary>
    private ActionBarConfig ResolveBar(string barId)
    {
        bool supplied = activePage != null && activePage.Supplies(barId);
        var state = FindPageState(supplied ? activePage.pageId : defaultPage?.pageId);

        return state?.Find(barId);
    }

    private IEnumerable<(string id, ActionBarConfig config)> AllBars()
    {
        foreach (var def in availableBars)
        {
            if (def == null || !def.IsValid()) continue;

            yield return (def.barId, ResolveBar(def.barId));
        }
    }

    private void TriggerOverride(string barId, int index)
    {
        var overrideAbility = transformSystem.GetEffectiveAbility(barId, index);
        if (overrideAbility != null && abilitySystem.CanUseAbility(overrideAbility.abilityId))
        {
            abilitySystem.UseAbility(overrideAbility.abilityId);
            transformSystem.ClearOverride(barId, index);
        }
    }

    private bool DoesSlotContainAbility(ActionBarSlotData slot, string abilityId)
    {
        if (!slot.IsAssigned) return false;

        return slot.abilitySlotId == $"ability:{abilityId}";
    }

    private bool IsValidIndex(int index, ActionBarConfig config)
        => index >= 0 && index < config.slots.Count;

    private void NotifyAllBarsChanged()
    {
        foreach (var (id, config) in AllBars())
        {
            if (config == null) continue;

            for (int i = 0; i < config.slots.Count; i++)
                OnSlotChanged?.Invoke(id, i);
        }
    }

    #endregion
}

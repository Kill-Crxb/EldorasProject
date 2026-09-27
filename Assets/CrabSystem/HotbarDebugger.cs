// Editor-only. Three of these scan the scene with FindObjectsOfType every frame, and
// none of them belong in a player build.
#if UNITY_EDITOR
using UnityEngine;

public class HotbarDebugger : MonoBehaviour
{
    [SerializeField] private bool enabled = true;
    [SerializeField] private int logPaddingSize = 70;

    private ControllerBrain playerBrain;
    private HotbarSystem hotbarSystem;

    private void OnEnable()
    {
        if (!enabled) return;

        var player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
            playerBrain = player.GetComponent<ControllerBrain>();
    }

    private void Update()
    {
        if (!enabled || playerBrain == null) return;

        if (Input.GetKeyDown(KeyCode.H))
        {
            DumpHotbarState();
        }

        if (Input.GetKeyDown(KeyCode.J))
        {
            DumpActionBarViews();
        }
    }

    [ContextMenu("Dump Hotbar State")]
    public void DumpHotbarState()
    {
        if (hotbarSystem == null)
            hotbarSystem = playerBrain?.GetModule<HotbarSystem>();

        string pad = new string('═', logPaddingSize);

        Debug.Log($"\n[HotbarDebugger] ╔{pad}");
        Debug.Log($"[HotbarDebugger] ║ HOTBAR SYSTEM STATE (Press H anytime)");
        Debug.Log($"[HotbarDebugger] ╠{pad}");

        if (hotbarSystem == null)
        {
            Debug.LogError($"[HotbarDebugger] ║ HotbarSystem: ✗ NULL");
            Debug.Log($"[HotbarDebugger] ╚{pad}\n");
            return;
        }

        Debug.Log($"[HotbarDebugger] ║ HotbarSystem: ✓ Found");
        Debug.Log($"[HotbarDebugger] ║ IsEnabled: {hotbarSystem.IsEnabled}");

        Debug.Log($"[HotbarDebugger] ║ ActivePage: {hotbarSystem.ActivePageId}");

        foreach (var def in hotbarSystem.GetAvailableBars())
        {
            if (def == null)
            {
                Debug.LogWarning($"[HotbarDebugger] ║ (empty slot in availableBars)");
                continue;
            }

            bool active = hotbarSystem.IsBarActive(def.barId);
            string flags = active ? "ON " : "off";
            if (def.alwaysOn) flags += " alwaysOn";

            Debug.Log($"[HotbarDebugger] ║");
            Debug.Log($"[HotbarDebugger] ║ [{def.barId}] {flags} · {def.slots}x{def.rows} · " +
                      $"anchor {def.anchor}");
            DumpBar(hotbarSystem, def.barId);
        }

        Debug.Log($"[HotbarDebugger] ╚{pad}\n");
    }

    private void DumpBar(HotbarSystem hotbar, string barId)
    {
        var config = hotbar.GetConfig(barId);
        if (config == null)
        {
            Debug.LogWarning($"[HotbarDebugger] ║   Config: ✗ NULL");
            return;
        }

        Debug.Log($"[HotbarDebugger] ║   SlotCount: {config.slotCount}");

        for (int i = 0; i < config.slotCount; i++)
        {
            var slot = hotbar.GetSlot(barId, i);
            if (slot == null)
            {
                Debug.LogWarning($"[HotbarDebugger] ║   Slot[{i}]: ✗ NULL");
                continue;
            }

            Debug.Log($"[HotbarDebugger] ║   Slot[{i}]:");
            Debug.Log($"[HotbarDebugger] ║     IsAssigned: {slot.IsAssigned}");
            Debug.Log($"[HotbarDebugger] ║     abilitySlotId: '{slot.abilitySlotId}'");
            Debug.Log($"[HotbarDebugger] ║     HasItem: {slot.HasItem}");

            var ability = hotbar.ResolveSlotAbility(slot);
            if (ability != null)
            {
                Debug.Log($"[HotbarDebugger] ║     ✓ Ability: {ability.abilityName}");
            }
            else if (slot.HasAbility)
            {
                Debug.LogWarning($"[HotbarDebugger] ║     ✗ Ability ID not found");
            }
            else
            {
                Debug.Log($"[HotbarDebugger] ║     ⊘ Empty slot");
            }
        }
    }

    [ContextMenu("Dump ActionBarView State")]
    public void DumpActionBarViews()
    {
        string pad = new string('═', logPaddingSize);

        Debug.Log($"\n[HotbarDebugger] ╔{pad}");
        Debug.Log($"[HotbarDebugger] ║ ACTION BAR VIEWS (Press J anytime)");
        Debug.Log($"[HotbarDebugger] ╠{pad}");

        var actionBars = FindObjectsOfType<ActionBarView>();
        if (actionBars.Length == 0)
        {
            Debug.LogError($"[HotbarDebugger] ║ No ActionBarView components found in scene");
            Debug.Log($"[HotbarDebugger] ╚{pad}\n");
            return;
        }

        Debug.Log($"[HotbarDebugger] ║ Found {actionBars.Length} ActionBarView(s)");

        foreach (var bar in actionBars)
        {
            Debug.Log($"[HotbarDebugger] ║");
            Debug.Log($"[HotbarDebugger] ║ {bar.gameObject.name}");
            Debug.Log($"[HotbarDebugger] ║   Active: {bar.gameObject.activeSelf}");
            Debug.Log($"[HotbarDebugger] ║   Enabled: {bar.enabled}");

            var rectTransform = bar.GetComponent<RectTransform>();
            if (rectTransform != null)
            {
                Debug.Log($"[HotbarDebugger] ║   RectTransform.sizeDelta: {rectTransform.sizeDelta}");
                Debug.Log($"[HotbarDebugger] ║   RectTransform.anchoredPosition: {rectTransform.anchoredPosition}");
                Debug.Log($"[HotbarDebugger] ║   RectTransform.scale: {rectTransform.localScale}");
            }

            // Check for child slot views
            int slotCount = bar.transform.childCount;
            Debug.Log($"[HotbarDebugger] ║   Child GameObjects: {slotCount}");

            for (int i = 0; i < slotCount && i < 5; i++)
            {
                var child = bar.transform.GetChild(i);
                Debug.Log($"[HotbarDebugger] ║     [{i}] {child.name} (active: {child.gameObject.activeSelf})");

                var slotView = child.GetComponent<ActionBarSlotView>();
                if (slotView != null)
                {
                    Debug.Log($"[HotbarDebugger] ║         ✓ Has ActionBarSlotView");
                }
                else
                {
                    Debug.LogWarning($"[HotbarDebugger] ║         ✗ No ActionBarSlotView component");
                }
            }

            if (slotCount > 5)
            {
                Debug.Log($"[HotbarDebugger] ║     ... and {slotCount - 5} more");
            }
        }

        Debug.Log($"[HotbarDebugger] ╚{pad}\n");
    }
}

#endif

using System.Collections.Generic;
using UnityEngine;
using RPG.NPC.UI;

public class NameplateManager : MonoBehaviour
{
    private static NameplateManager instance;
    public static NameplateManager Instance
    {
        get
        {
            if (instance == null)
                instance = FindFirstObjectByType<NameplateManager>(FindObjectsInactive.Exclude);
            return instance;
        }
    }

    [Header("Nameplate Prefab")]
    [SerializeField] private GameObject nameplatePrefab;
    [SerializeField] private Transform worldSpaceParent;

    [Header("Debug")]
    [SerializeField] private bool debugLogging = false;

    private ControllerBrain playerBrain;
    private readonly List<NPCNameplate> registeredNameplates = new List<NPCNameplate>();

    public ControllerBrain PlayerBrain => playerBrain;

    void Awake()
    {
        if (instance != null && instance != this) { Destroy(gameObject); return; }
        instance = this;
    }

    void Start() => ResolvePlayerBrain();

    public void SpawnNameplate(ControllerBrain npcBrain)
    {
        if (!enabled) return;
        if (nameplatePrefab == null) { Debug.LogError("[NameplateManager] Nameplate prefab not assigned!"); return; }
        if (npcBrain == null) return;

        Transform parent = worldSpaceParent != null ? worldSpaceParent : transform;
        GameObject go = Instantiate(nameplatePrefab, parent);
        go.name = $"Nameplate_{npcBrain.name}";

        var nameplate = go.GetComponent<NPCNameplate>();
        if (nameplate == null)
        {
            Debug.LogError("[NameplateManager] Nameplate prefab missing NPCNameplate component!", nameplatePrefab);
            Destroy(go);
            return;
        }

        Register(nameplate);
        nameplate.Initialize(npcBrain, playerBrain);

        if (debugLogging)
            Debug.Log($"[NameplateManager] Spawned: {npcBrain.name}");
    }

    public void ResolvePlayerBrain()
    {
        foreach (var brain in FindObjectsByType<ControllerBrain>(FindObjectsSortMode.None))
        {
            if (brain.EntityType != EntityType.Player) continue;
            SetPlayerBrain(brain);
            return;
        }
        Debug.LogWarning("[NameplateManager] No player brain found.");
    }

    public void SetPlayerBrain(ControllerBrain brain)
    {
        playerBrain = brain;
        foreach (var nameplate in registeredNameplates)
        {
            if (nameplate != null) nameplate.SetTargetPlayer(playerBrain);
        }
    }

    public void Register(NPCNameplate nameplate)
    {
        if (nameplate == null || registeredNameplates.Contains(nameplate)) return;
        registeredNameplates.Add(nameplate);
        if (playerBrain != null) nameplate.SetTargetPlayer(playerBrain);
    }

    public void Unregister(NPCNameplate nameplate) => registeredNameplates.Remove(nameplate);

    public void BroadcastFactionRefresh()
    {
        foreach (var nameplate in registeredNameplates)
            if (nameplate != null) nameplate.UpdateDisplay();
    }

    [ContextMenu("Print Registered Nameplates")]
    private void PrintRegistered()
    {
        if (!Application.isPlaying) return;
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"[NameplateManager] {registeredNameplates.Count} nameplate(s) | Player: {(playerBrain != null ? playerBrain.name : "NONE")}");
        foreach (var n in registeredNameplates)
            sb.AppendLine(n != null ? $"  • {n.EntityName} (Lv.{n.EntityLevel})" : "  • [destroyed]");
        Debug.Log(sb.ToString());
    }
}
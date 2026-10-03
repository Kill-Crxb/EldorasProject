#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;

// Editor-only. Spawns itself when play starts, so it needs no scene setup. For every entity with a MovesetModule, logs
// one line whenever its moveset state changes: stance, the item in the weapon slot, the active
// moveset, the step LMB would fire, and what the hotbar's LMB slot resolves to.
//
//   [MovesetProbe] Base_PC(Clone)  armed  item steel_katana  moveset Moveset_Katana  next BasicAttack1  slot BasicAttack1
public class MovesetProbe : MonoBehaviour
{
    [SerializeField] private string movesetBarId = "mouse";
    [SerializeField] private int movesetSlotIndex = 0;

    private readonly Dictionary<MovesetModule, string> last = new();
    private bool reportedNone;

    // A copy placed in a scene loaded after the self-spawned one stands down, so lines aren't doubled.
    private void Awake()
    {
        if (FindObjectsByType<MovesetProbe>().Length > 1) Destroy(this);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Spawn()
    {
        if (FindAnyObjectByType<MovesetProbe>() != null) return;

        GameObject host = new GameObject("[MovesetProbe]");
        DontDestroyOnLoad(host);
        host.AddComponent<MovesetProbe>();
        Debug.Log("[MovesetProbe] running");
    }

    private void Update()
    {
        MovesetModule[] found = FindObjectsByType<MovesetModule>();
        if (found.Length == 0 && !reportedNone && Time.frameCount % 300 == 0)
        {
            reportedNone = true;
            Debug.Log("[MovesetProbe] no active MovesetModule anywhere");
        }

        foreach (MovesetModule moveset in found)
        {
            string state = Describe(moveset);
            if (last.TryGetValue(moveset, out string previous) && previous == state) continue;

            last[moveset] = state;
            Debug.Log($"[MovesetProbe] {moveset.transform.root.name}  {state}");
        }
    }

    private string Describe(MovesetModule moveset)
    {
        ControllerBrain brain = moveset.GetComponentInParent<ControllerBrain>();
        if (brain == null) return "no brain";

        CombatStanceModule stance = brain.GetModule<CombatStanceModule>();
        EquipmentSystem equipment = brain.GetModule<EquipmentSystem>();
        HotbarSystem hotbar = brain.GetModule<HotbarSystem>();
        Blackboard blackboard = brain.Blackboard;

        string stanceText = stance == null ? "no-stance" : stance.IsUnarmed ? "unarmed" : "armed";
        ItemInstance item = equipment != null ? equipment.GetEquippedItem("mainwep") : null;
        string itemText = item == null ? "none" : item.definitionId;
        WeaponMoveset active = moveset.ActiveMoveset;
        AbilityDefinition next = moveset.PeekNext();
        AbilityDefinition block = moveset.PeekBlock();

        bool grounded = brain.Movement != null && brain.Movement.IsGrounded;
        bool blocking = blackboard != null && blackboard.GetBool(BlackboardKey.IsBlocking);
        bool running = blackboard != null && (blackboard.GetBool(BlackboardKey.IsRunning) || blackboard.GetBool(BlackboardKey.IsSprinting));
        string context = $"{(grounded ? "ground" : "AIR")}{(blocking ? " blocking" : "")}{(running ? " running" : "")}";

        string slotText = "no-hotbar";
        if (hotbar != null)
        {
            bool owned = hotbar.IsMovesetSlot(movesetBarId, movesetSlotIndex);
            AbilityDefinition shown = hotbar.ResolveSlotAbility(hotbar.GetSlot(movesetBarId, movesetSlotIndex));
            slotText = $"{(owned ? "owned" : "NOT-owned")} shows {(shown != null ? shown.abilityId : "nothing")}";
        }

        return $"{stanceText}  {context}  item {itemText}  moveset {(active != null ? active.name : "none")}  " +
               $"next {(next != null ? next.abilityId : "none")}  block {(block != null ? block.abilityId : "none")}  slot {slotText}";
    }
}
#endif

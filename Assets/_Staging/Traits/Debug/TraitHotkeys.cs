#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.InputSystem;

// Editor-only. Drop one in the scene and fill the list: Ctrl+F5 toggles the first trait on the player, Ctrl+F6 the
// second, and so on (F5-F9, F11). Ctrl+F12 raises the player one level, to watch a dormant trait reveal.
// Ctrl+Delete extracts the organ in the chosen slot, to watch its trait go with it.
// Plain F-keys stay with StatusHotkeys.

namespace CrabSystem
{
    public class TraitHotkeys : MonoBehaviour
    {
        [SerializeField] private TraitDefinition[] traits;
        [SerializeField] private string extractSlot = "eyes";

        private static readonly Key[] Keys = { Key.F5, Key.F6, Key.F7, Key.F8, Key.F9, Key.F11 };

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || !keyboard.ctrlKey.isPressed) return;

            if (keyboard[Key.F12].wasPressedThisFrame) LevelUp();
            if (keyboard[Key.Delete].wasPressedThisFrame) Extract();

            int count = Mathf.Min(traits.Length, Keys.Length);
            for (int i = 0; i < count; i++)
            {
                if (keyboard[Keys[i]].wasPressedThisFrame && traits[i] != null) Toggle(traits[i]);
            }
        }

        private static void Toggle(TraitDefinition trait)
        {
            TraitModule module = PlayerModule<TraitModule>();
            if (module == null) return;

            bool removed = module.Remove(trait.traitId);
            if (!removed) module.Grant(trait.traitId);
            Debug.Log($"[TraitHotkeys] {trait.traitId}: {(removed ? "removed" : module.IsRevealed(trait.traitId) ? "granted" : "granted, dormant")}");
        }

        private void Extract()
        {
            OrganModule organs = PlayerModule<OrganModule>();
            if (organs == null) return;

            InstalledOrgan organ = organs.Extract(extractSlot);
            Debug.Log(organ != null ? $"[TraitHotkeys] Extracted {organ.organId} from {extractSlot}" : $"[TraitHotkeys] Nothing in {extractSlot}");
        }

        private static void LevelUp()
        {
            RPGSystem rpg = PlayerModule<RPGSystem>();
            if (rpg == null) return;

            rpg.SetLevel(rpg.CurrentLevel + 1);
            Debug.Log($"[TraitHotkeys] Level {rpg.CurrentLevel}");
        }

        private static T PlayerModule<T>() where T : Component
        {
            foreach (ControllerBrain brain in FindObjectsByType<ControllerBrain>())
            {
                if (brain.IsPlayer) return brain.GetComponentInChildren<T>();
            }

            Debug.LogWarning("[TraitHotkeys] No player brain in the scene.");
            return null;
        }
    }
}
#endif

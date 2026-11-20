#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.InputSystem;

// Editor-only. Drop one in the scene and fill the list: Shift+F5 toggles the first feat on the player, Shift+F6 the
// second, and so on (F5-F9, F11). Grants sit under debug:feat:{id}, so they never collide with a real granter.
// Ctrl+F-keys stay with TraitHotkeys, plain F-keys with StatusHotkeys.

namespace CrabSystem
{
    public class FeatHotkeys : MonoBehaviour
    {
        [SerializeField] private FeatDefinition[] feats;

        private static readonly Key[] Keys = { Key.F5, Key.F6, Key.F7, Key.F8, Key.F9, Key.F11 };

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || !keyboard.shiftKey.isPressed) return;

            int count = Mathf.Min(feats.Length, Keys.Length);
            for (int i = 0; i < count; i++)
            {
                if (keyboard[Keys[i]].wasPressedThisFrame && feats[i] != null) Toggle(feats[i]);
            }
        }

        private static void Toggle(FeatDefinition feat)
        {
            FeatModule module = PlayerFeats();
            if (module == null) return;

            string key = $"debug:feat:{feat.featId}";
            bool held = false;
            foreach (FeatGrant grant in module.Grants)
            {
                if (grant.sourceKey == key) held = true;
            }

            if (held) module.Withdraw(key);
            else module.Grant(feat, key);
            Debug.Log($"[FeatHotkeys] {feat.featId}: {(held ? "withdrawn" : "granted")}");
        }

        private static FeatModule PlayerFeats()
        {
            foreach (ControllerBrain brain in FindObjectsByType<ControllerBrain>())
            {
                if (brain.IsPlayer) return brain.GetComponentInChildren<FeatModule>();
            }

            Debug.LogWarning("[FeatHotkeys] No player brain in the scene.");
            return null;
        }
    }
}
#endif

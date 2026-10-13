using UnityEditor;
using UnityEngine;

// Tools → Crab → UI → Add Status Bar (Player / Target). Puts a StatusBarView under the selected UI object, or the
// first canvas in the open scene, at a starting spot to drag from: the player's top-left, the target's top-centre.

namespace CrabSystem
{
    public static class StatusBarMenu
    {
        [MenuItem("Tools/Crab/UI/Add Status Bar (Player)")]
        static void AddPlayer() => Add(StatusBarSubject.Player, new Vector2(0f, 1f), new Vector2(20f, -20f));

        [MenuItem("Tools/Crab/UI/Add Status Bar (Target)")]
        static void AddTarget() => Add(StatusBarSubject.Target, new Vector2(0.5f, 1f), new Vector2(-100f, -60f));

        static void Add(StatusBarSubject subject, Vector2 anchor, Vector2 offset)
        {
            Transform parent = Selection.activeTransform as RectTransform;
            if (parent == null)
            {
                Canvas canvas = Object.FindAnyObjectByType<Canvas>();
                parent = canvas != null ? canvas.transform : null;
            }
            if (parent == null)
            {
                Debug.LogWarning("[StatusBarMenu] No canvas in the open scene; select one and run this again.");
                return;
            }

            var go = new GameObject($"StatusBar_{subject}", typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(go, "Add Status Bar");
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = anchor;
            rect.anchoredPosition = offset;
            rect.sizeDelta = new Vector2(40f, 40f);

            var bar = go.AddComponent<StatusBarView>();
            var so = new SerializedObject(bar);
            so.FindProperty("subject").enumValueIndex = (int)subject;
            so.ApplyModifiedPropertiesWithoutUndo();

            Selection.activeGameObject = go;
            Debug.Log($"[StatusBarMenu] {go.name} added under {parent.name}. Move it, then set growth, rows and size on the component.");
        }
    }
}

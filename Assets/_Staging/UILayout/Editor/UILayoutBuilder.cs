using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// Sets up UI layout mode on the open scene's UI canvas (the one holding UIInputRouter):
// - a UIFrame on every hotbar anchor, resource / defence bar, status bar and globe that lacks one,
//   with its id taken from the object's name; bars are set to resize (width / height) instead of scale;
// - the full-screen overlay and toolbar, with a UIPanel (side Layout) and UILayoutMode;
// - a "UI" entry in the escape menu, after "Settings", wired to UILayoutMode.Enter.
// Running it again replaces the overlay and toolbar (undoable) and rewires the menu entry, so
// toolbar changes reach a scene that already has it. Colours and sizes are tuned on the built objects afterwards.

namespace CrabSystem
{
    public static class UILayoutBuilder
    {
        [MenuItem("Tools/CrabSystem/UI/Build UI Layout Mode")]
        public static void Build()
        {
            var router = Object.FindAnyObjectByType<UIInputRouter>();
            if (router == null)
            {
                Debug.LogError("[UILayoutBuilder] No UIInputRouter in the open scene");
                return;
            }

            var canvas = router.GetComponentInParent<Canvas>();
            if (canvas == null)
            {
                Debug.LogError("[UILayoutBuilder] UIInputRouter is not under a canvas", router);
                return;
            }

            var existing = canvas.GetComponentInChildren<UILayoutMode>(true);
            if (existing != null) Undo.DestroyObjectImmediate(existing.gameObject);

            int frames = AddFrames(canvas.transform);
            var mode = BuildMode(canvas.transform);
            bool wired = AddMenuEntry(canvas.transform, mode);

            EditorSceneManager.MarkSceneDirty(mode.gameObject.scene);
            Selection.activeObject = mode.gameObject;

            Debug.Log($"[UILayoutBuilder] Added {frames} UIFrames and UI layout mode." +
                      (wired ? " Escape menu has a \"UI\" entry." : " No escape menu found: wire a button to UILayoutMode.Enter."));
        }

        [MenuItem("Tools/CrabSystem/UI/Add UIFrames To HUD")]
        public static void AddFramesOnly()
        {
            var router = Object.FindAnyObjectByType<UIInputRouter>();
            var canvas = router != null ? router.GetComponentInParent<Canvas>() : null;
            if (canvas == null)
            {
                Debug.LogError("[UILayoutBuilder] No UIInputRouter under a canvas in the open scene");
                return;
            }

            int frames = AddFrames(canvas.transform);
            EditorSceneManager.MarkSceneDirty(canvas.gameObject.scene);
            Debug.Log($"[UILayoutBuilder] Added {frames} UIFrames.");
        }

        private static int AddFrames(Transform root)
        {
            int added = 0;

            foreach (var anchor in root.GetComponentsInChildren<HotbarAnchor>(true)) added += AddFrame(anchor.gameObject);
            foreach (var bar in root.GetComponentsInChildren<BarView>(true))
            {
                added += AddFrame(bar.gameObject);
                MakeResizable(bar.GetComponent<UIFrame>());
            }

            foreach (var status in root.GetComponentsInChildren<StatusBarView>(true)) added += AddFrame(status.gameObject);
            foreach (var globe in root.GetComponentsInChildren<ResourceGlobe>(true)) added += AddFrame(globe.gameObject);

            return added;
        }

        private static int AddFrame(GameObject go)
        {
            if (go.GetComponent<UIFrame>() != null) return 0;

            var frame = new SerializedObject(Undo.AddComponent<UIFrame>(go));
            frame.FindProperty("frameId").stringValue = go.name.Trim().ToLowerInvariant().Replace(' ', '_');
            frame.ApplyModifiedPropertiesWithoutUndo();
            return 1;
        }

        // Bars stretch rather than scale, so the fill and text stay sharp.
        private static void MakeResizable(UIFrame frame)
        {
            var serialized = new SerializedObject(frame);
            serialized.FindProperty("canScale").boolValue = false;
            serialized.FindProperty("canResizeWidth").boolValue = true;
            serialized.FindProperty("canResizeHeight").boolValue = true;
            serialized.ApplyModifiedProperties();
        }

        private static UILayoutMode BuildMode(Transform canvas)
        {
            var root = NewUI("UILayout", canvas);
            Stretch(root);
            root.SetAsLastSibling();

            var overlay = NewUI("Overlay", root);
            Stretch(overlay);
            // Clear but still catching clicks, so nothing under the overlay reacts while editing.
            overlay.gameObject.AddComponent<Image>().color = Color.clear;

            var handles = NewUI("Handles", overlay);
            Stretch(handles);
            var template = NewHandle(handles);

            var toolbar = NewToolbar(overlay);
            var selectedLabel = NewText("Selected", toolbar, 18f, TextAlignmentOptions.MidlineLeft);
            selectedLabel.text = "Click an element";
            selectedLabel.gameObject.AddComponent<LayoutElement>().preferredWidth = 220f;

            var scale = NewSliderRow(toolbar, "Scale");
            var width = NewSliderRow(toolbar, "Width");
            var height = NewSliderRow(toolbar, "Height");

            var grid = NewButton(toolbar, "Grid", "Grid: On", 120f);
            var reset = NewButton(toolbar, "Reset", "Reset", 100f);
            var cancel = NewButton(toolbar, "Cancel", "Cancel", 100f);
            var save = NewButton(toolbar, "Save", "Save", 100f);

            var panel = new SerializedObject(root.gameObject.AddComponent<UIPanel>());
            panel.FindProperty("side").enumValueIndex = (int)PanelSide.Layout;
            panel.FindProperty("content").objectReferenceValue = overlay.gameObject;
            panel.ApplyModifiedPropertiesWithoutUndo();

            var modeComponent = root.gameObject.AddComponent<UILayoutMode>();
            var mode = new SerializedObject(modeComponent);
            mode.FindProperty("handleContainer").objectReferenceValue = handles;
            mode.FindProperty("handleTemplate").objectReferenceValue = template;
            mode.FindProperty("selectedLabel").objectReferenceValue = selectedLabel;
            mode.FindProperty("scaleRow").objectReferenceValue = scale.transform.parent.gameObject;
            mode.FindProperty("scaleSlider").objectReferenceValue = scale;
            mode.FindProperty("widthRow").objectReferenceValue = width.transform.parent.gameObject;
            mode.FindProperty("widthSlider").objectReferenceValue = width;
            mode.FindProperty("heightRow").objectReferenceValue = height.transform.parent.gameObject;
            mode.FindProperty("heightSlider").objectReferenceValue = height;
            mode.FindProperty("gridButton").objectReferenceValue = grid;
            mode.FindProperty("gridLabel").objectReferenceValue = grid.GetComponentInChildren<TMP_Text>();
            mode.FindProperty("resetButton").objectReferenceValue = reset;
            mode.FindProperty("cancelButton").objectReferenceValue = cancel;
            mode.FindProperty("saveButton").objectReferenceValue = save;
            mode.ApplyModifiedPropertiesWithoutUndo();

            Undo.RegisterCreatedObjectUndo(root.gameObject, "Build UI Layout Mode");
            return modeComponent;
        }

        // Inserted after "Settings" so it sits with the other game options; appended if there's no Settings.
        private static bool AddMenuEntry(Transform canvas, UILayoutMode mode)
        {
            var view = canvas.GetComponentInChildren<EscapeMenuView>(true);
            if (view == null) return false;

            var menu = new SerializedObject(view);
            var entries = menu.FindProperty("entries");
            int at = entries.arraySize;

            int found = -1;

            for (int i = 0; i < entries.arraySize; i++)
            {
                string label = entries.GetArrayElementAtIndex(i).FindPropertyRelative("label").stringValue;
                if (label == "UI") found = i;
                if (label == "Settings") at = i + 1;
            }

            // An existing entry is rewired rather than duplicated, so a rebuild points it at the new mode.
            if (found < 0) entries.InsertArrayElementAtIndex(at);
            var entry = entries.GetArrayElementAtIndex(found < 0 ? at : found);
            entry.FindPropertyRelative("kind").enumValueIndex = (int)EscapeMenuEntryKind.Button;
            entry.FindPropertyRelative("label").stringValue = "UI";

            // The inserted element is a copy of its neighbour, listeners included, so set the one call outright.
            var calls = entry.FindPropertyRelative("onClick.m_PersistentCalls.m_Calls");
            calls.arraySize = 1;
            var call = calls.GetArrayElementAtIndex(0);
            call.FindPropertyRelative("m_Target").objectReferenceValue = mode;
            call.FindPropertyRelative("m_TargetAssemblyTypeName").stringValue =
                $"{typeof(UILayoutMode).FullName}, {typeof(UILayoutMode).Assembly.GetName().Name}";
            call.FindPropertyRelative("m_MethodName").stringValue = nameof(UILayoutMode.Enter);
            call.FindPropertyRelative("m_Mode").intValue = (int)UnityEngine.Events.PersistentListenerMode.Void;
            call.FindPropertyRelative("m_CallState").intValue = (int)UnityEngine.Events.UnityEventCallState.RuntimeOnly;

            menu.ApplyModifiedProperties();
            return true;
        }

        // The handle itself is invisible: it only catches clicks. Its backdrop, drawn behind the
        // element, is made at runtime. The name sits just above the box.
        private static UILayoutHandle NewHandle(RectTransform parent)
        {
            var rect = NewUI("HandleTemplate", parent);
            rect.gameObject.AddComponent<Image>().color = Color.clear;

            var label = NewText("Label", rect, 14f, TextAlignmentOptions.Bottom);
            label.text = "Frame";
            label.rectTransform.anchorMin = new Vector2(0f, 1f);
            label.rectTransform.anchorMax = new Vector2(1f, 1f);
            label.rectTransform.pivot = new Vector2(0.5f, 0f);
            label.rectTransform.sizeDelta = new Vector2(0f, 20f);
            label.rectTransform.anchoredPosition = new Vector2(0f, 2f);
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Overflow;

            var handle = rect.gameObject.AddComponent<UILayoutHandle>();
            var serialized = new SerializedObject(handle);
            serialized.FindProperty("label").objectReferenceValue = label;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            rect.gameObject.SetActive(false);
            return handle;
        }

        // A label and a slider in one row, so the row can be hidden when the selected frame lacks the option.
        private static Slider NewSliderRow(RectTransform parent, string name)
        {
            var row = NewUI(name + "Row", parent);
            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 6f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            var label = NewText("Label", row, 16f, TextAlignmentOptions.MidlineRight);
            label.text = name;
            label.gameObject.AddComponent<LayoutElement>().preferredWidth = 56f;

            var slider = NewSlider(row);
            slider.name = name;
            row.gameObject.SetActive(false);
            return slider;
        }

        private static RectTransform NewToolbar(RectTransform parent)
        {
            var bar = NewUI("Toolbar", parent);
            bar.anchorMin = new Vector2(0.5f, 1f);
            bar.anchorMax = new Vector2(0.5f, 1f);
            bar.pivot = new Vector2(0.5f, 1f);
            bar.anchoredPosition = new Vector2(0f, -24f);

            bar.gameObject.AddComponent<Image>().color = new Color(0.06f, 0.06f, 0.08f, 0.9f);

            var layout = bar.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(16, 16, 10, 10);
            layout.spacing = 10f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            var fitter = bar.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            return bar;
        }

        private static Slider NewSlider(RectTransform parent)
        {
            var go = DefaultControls.CreateSlider(new DefaultControls.Resources());
            go.layer = parent.gameObject.layer;
            go.transform.SetParent(parent, false);

            var layout = go.AddComponent<LayoutElement>();
            layout.preferredWidth = 160f;
            layout.preferredHeight = 20f;
            return go.GetComponent<Slider>();
        }

        private static Button NewButton(RectTransform parent, string name, string text, float width)
        {
            var rect = NewUI(name, parent);
            var layout = rect.gameObject.AddComponent<LayoutElement>();
            layout.preferredWidth = width;
            layout.preferredHeight = 40f;

            var image = rect.gameObject.AddComponent<Image>();
            image.color = new Color(1f, 1f, 1f, 0.08f);

            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;

            var label = NewText("Label", rect, 18f, TextAlignmentOptions.Center);
            label.text = text;
            Stretch(label.rectTransform);
            return button;
        }

        private static TextMeshProUGUI NewText(string name, Transform parent, float size, TextAlignmentOptions alignment)
        {
            var text = NewUI(name, parent).gameObject.AddComponent<TextMeshProUGUI>();
            text.fontSize = size;
            text.alignment = alignment;
            text.raycastTarget = false;
            return text;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static RectTransform NewUI(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }
    }
}

using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// Builds the escape menu under the open scene's UI canvas (the one holding UIInputRouter):
// a block on the left that sizes to its rows, a hidden button and header to clone, and the
// view with its starting entries. Colours and sizes are tuned on the built objects afterwards.

namespace CrabSystem
{
    public static class EscapeMenuBuilder
    {
        [MenuItem("Tools/CrabSystem/UI/Build Escape Menu")]
        public static void Build()
        {
            var router = Object.FindAnyObjectByType<UIInputRouter>();
            if (router == null)
            {
                Debug.LogError("[EscapeMenuBuilder] No UIInputRouter in the open scene");
                return;
            }

            var canvas = router.GetComponentInParent<Canvas>();
            if (canvas == null)
            {
                Debug.LogError("[EscapeMenuBuilder] UIInputRouter is not under a canvas", router);
                return;
            }

            var existing = canvas.GetComponentInChildren<EscapeMenuView>(true);
            if (existing != null)
            {
                Selection.activeObject = existing;
                Debug.LogError("[EscapeMenuBuilder] This canvas already has an escape menu", existing);
                return;
            }

            var menu = NewUI("EscapeMenu", canvas.transform);
            menu.anchorMin = new Vector2(0f, 0.5f);
            menu.anchorMax = new Vector2(0f, 0.5f);
            menu.pivot = new Vector2(0f, 0.5f);
            menu.anchoredPosition = new Vector2(48f, 0f);
            menu.sizeDelta = new Vector2(320f, 0f);

            menu.gameObject.AddComponent<Image>().color = new Color(0.06f, 0.06f, 0.08f, 0.85f);

            var layout = menu.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(16, 16, 16, 16);
            layout.spacing = 6f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            menu.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var button = NewButton(menu);
            var header = NewHeader(menu);

            var panel = new SerializedObject(menu.gameObject.AddComponent<UIPanel>());
            panel.FindProperty("side").enumValueIndex = (int)PanelSide.Menu;
            panel.ApplyModifiedPropertiesWithoutUndo();

            var view = new SerializedObject(menu.gameObject.AddComponent<EscapeMenuView>());
            view.FindProperty("list").objectReferenceValue = menu;
            view.FindProperty("buttonTemplate").objectReferenceValue = button;
            view.FindProperty("headerTemplate").objectReferenceValue = header;
            view.ApplyModifiedPropertiesWithoutUndo();

            Undo.RegisterCreatedObjectUndo(menu.gameObject, "Build Escape Menu");
            EditorSceneManager.MarkSceneDirty(menu.gameObject.scene);
            Selection.activeObject = menu.gameObject;
        }

        private static Button NewButton(RectTransform parent)
        {
            var rect = NewUI("ButtonTemplate", parent);
            rect.gameObject.AddComponent<LayoutElement>().preferredHeight = 44f;

            var image = rect.gameObject.AddComponent<Image>();
            image.color = new Color(1f, 1f, 1f, 0.08f);

            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;

            var label = NewText("Label", rect, 22f, TextAlignmentOptions.MidlineLeft);
            label.text = "Button";
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = new Vector2(16f, 0f);
            label.rectTransform.offsetMax = new Vector2(-16f, 0f);

            rect.gameObject.SetActive(false);
            return button;
        }

        private static TMP_Text NewHeader(RectTransform parent)
        {
            var header = NewText("HeaderTemplate", parent, 16f, TextAlignmentOptions.BottomLeft);
            header.text = "Header";
            header.color = new Color(1f, 1f, 1f, 0.5f);
            header.fontStyle = FontStyles.UpperCase | FontStyles.Bold;
            header.gameObject.AddComponent<LayoutElement>().preferredHeight = 28f;

            header.gameObject.SetActive(false);
            return header;
        }

        private static TextMeshProUGUI NewText(string name, Transform parent, float size, TextAlignmentOptions alignment)
        {
            var text = NewUI(name, parent).gameObject.AddComponent<TextMeshProUGUI>();
            text.fontSize = size;
            text.alignment = alignment;
            text.raycastTarget = false;
            return text;
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

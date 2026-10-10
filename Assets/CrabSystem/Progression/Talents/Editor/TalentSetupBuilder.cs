using System.Collections.Generic;
using NinjaGame.Progression;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// Tools → Crab → Talents → Set Up (10 Oct). Makes the TalentRules asset, puts a TalentModule on Base_PC (so the
// Mirror too) with the XP level cap matching the rules, and adds a Talents page to the player menu of the open
// scene, next to the Spellbook. Rerunnable: what exists is kept.
public static class TalentSetupBuilder
{
    const string Tag = "TalentSetup";
    const string RulesPath = "Assets/Database/Talents/TalentRules.asset";
    const string PlayerPath = "Assets/Database/Characters/PlayerCharacter/Base_PC.prefab";
    const string PageId = "talents";
    const string TemplatePageId = "spellbook";

    [MenuItem("Tools/Crab/Talents/Set Up")]
    public static void Build()
    {
        TalentRules rules = Rules();
        AddModule(rules);
        AddPage();
        AssetDatabase.SaveAssets();
        Debug.Log($"[{Tag}] Done. Make trees in Tools → Crab → Wizards → Talents, or Tools → Crab → Talents → Create Test Trees.");
    }

    public static TalentRules Rules()
    {
        var rules = AssetDatabase.LoadAssetAtPath<TalentRules>(RulesPath);
        if (rules != null) return rules;

        CrabWizardGUI.EnsureFolder("Assets/Database/Talents");
        rules = ScriptableObject.CreateInstance<TalentRules>();
        AssetDatabase.CreateAsset(rules, RulesPath);
        Debug.Log($"[{Tag}] rules: {RulesPath} made.");
        return rules;
    }

    static void AddModule(TalentRules rules)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PlayerPath);
        var rpg = root.GetComponentInChildren<RPGSystem>(true);
        if (rpg == null)
        {
            Debug.LogError($"[{Tag}] No RPGSystem on Base_PC to sit beside.");
            PrefabUtility.UnloadPrefabContents(root);
            return;
        }

        var module = root.GetComponentInChildren<TalentModule>(true);
        bool created = module == null;
        if (created)
        {
            var go = new GameObject("TalentModule");
            go.transform.SetParent(rpg.transform.parent, false);
            module = go.AddComponent<TalentModule>();
        }

        var so = new SerializedObject(module);
        so.FindProperty("rules").objectReferenceValue = rules;
        so.ApplyModifiedPropertiesWithoutUndo();

        var rpgSo = new SerializedObject(rpg);
        rpgSo.FindProperty("maxLevel").intValue = rules.levelCap;
        rpgSo.ApplyModifiedPropertiesWithoutUndo();

        PrefabUtility.SaveAsPrefabAsset(root, PlayerPath);
        PrefabUtility.UnloadPrefabContents(root);
        Debug.Log($"[{Tag}] player: TalentModule {(created ? "added to" : "rewired on")} Base_PC; level cap {rules.levelCap}.");
    }

    // A copy of the Spellbook page's button and a new slot holding the TalentPanelView, registered as a page.
    static void AddPage()
    {
        var directory = Object.FindAnyObjectByType<UIPanelDirectory>(FindObjectsInactive.Include);
        if (directory == null)
        {
            Debug.LogWarning($"[{Tag}] No player menu (UIPanelDirectory) in the open scene; open the Zoo and run this again for the page.");
            return;
        }

        var so = new SerializedObject(directory);
        SerializedProperty pages = so.FindProperty("pages");
        SerializedProperty template = Page(pages, TemplatePageId);
        if (Page(pages, PageId) != null)
        {
            Debug.Log($"[{Tag}] menu: Talents page already there.");
            return;
        }
        if (template == null)
        {
            Debug.LogError($"[{Tag}] No '{TemplatePageId}' page to copy the button and slot from.");
            return;
        }

        var templateButton = (Button)template.FindPropertyRelative("button").objectReferenceValue;
        SerializedProperty templateSlots = template.FindPropertyRelative("slots");
        var templateSlot = templateSlots.arraySize > 0 ? (UIPanelSlot)templateSlots.GetArrayElementAtIndex(0).objectReferenceValue : null;
        var templateHighlight = (Graphic)template.FindPropertyRelative("highlight").objectReferenceValue;
        if (templateButton == null || templateSlot == null)
        {
            Debug.LogError($"[{Tag}] The '{TemplatePageId}' page has no button or slot to copy.");
            return;
        }

        Button button = CopyButton(templateButton);
        UIPanelSlot slot = NewSlot(templateSlot);

        pages.arraySize++;
        SerializedProperty page = pages.GetArrayElementAtIndex(pages.arraySize - 1);
        page.FindPropertyRelative("pageId").stringValue = PageId;
        page.FindPropertyRelative("button").objectReferenceValue = button;
        SerializedProperty slots = page.FindPropertyRelative("slots");
        slots.arraySize = 1;
        slots.GetArrayElementAtIndex(0).objectReferenceValue = slot;
        page.FindPropertyRelative("highlight").objectReferenceValue = MatchingHighlight(templateButton, button, templateHighlight);
        so.ApplyModifiedProperties();

        EditorSceneManager.MarkSceneDirty(directory.gameObject.scene);
        Debug.Log($"[{Tag}] menu: Talents page added to {directory.gameObject.scene.name}. Save the scene.");
    }

    static SerializedProperty Page(SerializedProperty pages, string pageId)
    {
        for (int i = 0; i < pages.arraySize; i++)
        {
            SerializedProperty page = pages.GetArrayElementAtIndex(i);
            if (page.FindPropertyRelative("pageId").stringValue == pageId) return page;
        }
        return null;
    }

    static Button CopyButton(Button template)
    {
        GameObject copy = Object.Instantiate(template.gameObject, template.transform.parent);
        copy.name = "Button_Talents";
        copy.transform.SetSiblingIndex(template.transform.GetSiblingIndex() + 1);
        Undo.RegisterCreatedObjectUndo(copy, "Talents page");

        foreach (TMP_Text text in copy.GetComponentsInChildren<TMP_Text>(true)) text.text = "Talents";
        foreach (Text text in copy.GetComponentsInChildren<Text>(true)) text.text = "Talents";

        // The directory wires the click itself; drop whatever the Spellbook button was given in the inspector.
        Button button = copy.GetComponent<Button>();
        while (button.onClick.GetPersistentEventCount() > 0) UnityEditor.Events.UnityEventTools.RemovePersistentListener(button.onClick, 0);
        return button;
    }

    static Graphic MatchingHighlight(Button template, Button copy, Graphic highlight)
    {
        if (highlight == null || !highlight.transform.IsChildOf(template.transform)) return null;

        string path = AnimationUtility.CalculateTransformPath(highlight.transform, template.transform);
        Transform found = path.Length == 0 ? copy.transform : copy.transform.Find(path);
        return found != null ? found.GetComponent<Graphic>() : null;
    }

    static UIPanelSlot NewSlot(UIPanelSlot template)
    {
        var templateRect = (RectTransform)template.transform;
        var go = new GameObject("Slot_Talents", typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(go, "Talents page");

        var rect = (RectTransform)go.transform;
        rect.SetParent(templateRect.parent, false);
        rect.SetSiblingIndex(templateRect.GetSiblingIndex() + 1);
        rect.anchorMin = templateRect.anchorMin;
        rect.anchorMax = templateRect.anchorMax;
        rect.pivot = templateRect.pivot;
        rect.anchoredPosition = templateRect.anchoredPosition;
        rect.sizeDelta = templateRect.sizeDelta;

        if (template.TryGetComponent(out Image background))
        {
            Image image = go.AddComponent<Image>();
            image.sprite = background.sprite;
            image.type = background.type;
            image.color = background.color;
        }

        UIPanelSlot slot = go.AddComponent<UIPanelSlot>();
        var slotSo = new SerializedObject(slot);
        slotSo.FindProperty("slotId").stringValue = "Talents";
        slotSo.FindProperty("defaultViewId").stringValue = PageId;
        slotSo.ApplyModifiedPropertiesWithoutUndo();

        var view = new GameObject("TalentView", typeof(RectTransform));
        var viewRect = (RectTransform)view.transform;
        viewRect.SetParent(rect, false);
        viewRect.anchorMin = Vector2.zero;
        viewRect.anchorMax = Vector2.one;
        viewRect.offsetMin = Vector2.zero;
        viewRect.offsetMax = Vector2.zero;

        TalentPanelView panel = view.AddComponent<TalentPanelView>();
        var viewSo = new SerializedObject(panel);
        viewSo.FindProperty("viewId").stringValue = PageId;
        viewSo.FindProperty("displayName").stringValue = "Talents";
        viewSo.FindProperty("span").enumValueIndex = (int)ViewSpan.Full;
        viewSo.ApplyModifiedPropertiesWithoutUndo();

        go.SetActive(false);
        return slot;
    }
}

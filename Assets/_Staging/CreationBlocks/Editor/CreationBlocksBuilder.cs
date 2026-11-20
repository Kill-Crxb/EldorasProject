using System.Collections.Generic;
using CrabSystem;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// Tools → Characters → Build Creation Blocks. Staging: graduates with the creation panel (CR4).
//
// Fills the two hand-built blocks on MenuScene's CharacterCreationPanel so the layout is in place outside play mode,
// read from the default model's creation groups:
//
//   CrabCustomiseBlock         Hair (previous / next over hair_style), Hair color (one swatch per hair_colour option,
//                              tinted, with a selected marker), one Slider row per slider group cloned from Slider Entry.
//   CrabRPGCustomisationBlock  Name entry, the six core stats in StatIds.Cores order, Randomize. AllocationUi and the
//                              trait / racial entry templates are hidden until level-up points and the roll (CR3) exist.
//
// The panel finds placed rows by group id. Any group without one spawns into the customise block's Rows. The old
// BodyShapeBlock, AppearanceRows and RandomizeButton are hidden. Rerun after adding a shape key or a hair colour.

public static class CreationBlocksBuilder
{
    private const string ScenePath = "Assets/Database/Scenes/MenuScene.unity";
    private const string CustomiseBlock = "CrabCustomiseBlock";
    private const string RpgBlock = "CrabRPGCustomisationBlock";
    private const string HairStyleGroup = "hair_style";
    private const string HairColourGroup = "hair_colour";

    // Stat_Economy.md, adopted 13 Nov: 3 each + 8 rolled, cap 6 at creation.
    private const int StatPool = 26;
    private const int StatCap = 6;

    private static readonly string[] StatFields = { "mindText", "bodyText", "spiritText", "resilienceText", "enduranceText", "insightText" };
    private static readonly string[] StatNames = { "Mind", "Body", "Spirit", "Resilience", "Endurance", "Insight" };
    private static readonly string[] StatShort = { "MND", "BOD", "SPI", "RES", "END", "INS" };

    [MenuItem("Tools/Characters/Build Creation Blocks")]
    private static void Build()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var scene = EditorSceneManager.OpenScene(ScenePath);

        var panel = Object.FindFirstObjectByType<CharacterCreationPanel>(FindObjectsInactive.Include);
        if (panel == null)
        {
            Debug.LogError($"[CreationBlocksBuilder] No CharacterCreationPanel in {ScenePath}.");
            return;
        }

        var customise = panel.transform.Find(CustomiseBlock);
        var rpg = panel.transform.Find(RpgBlock);
        if (customise == null || rpg == null)
        {
            Debug.LogError($"[CreationBlocksBuilder] {CustomiseBlock} and {RpgBlock} must sit directly under CharacterCreationPanel.");
            return;
        }

        var panelSo = new SerializedObject(panel);
        var appearance = DefaultAppearance(panelSo);
        if (appearance == null)
        {
            Debug.LogError("[CreationBlocksBuilder] The panel's default model has no ModelAppearance. Set the model database and default model first.");
            return;
        }

        BuildHairRow(customise, appearance);
        BuildColourRow(customise, appearance);
        BuildSliderRows(customise, appearance);
        BuildRpgBlock(rpg, panelSo);
        Rewire(panel, panelSo, customise);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"[CreationBlocksBuilder] Built the creation blocks from {appearance.name}.");
    }

    // ── Model ─────────────────────────────────────────────────────────────────

    // The model the panel opens on, or the first playable one.
    private static ModelAppearance DefaultAppearance(SerializedObject panelSo)
    {
        var database = panelSo.FindProperty("modelDatabase").objectReferenceValue as ModelDatabase;
        if (database == null) return null;

        string defaultId = panelSo.FindProperty("defaultModelId").stringValue;
        GameObject chosen = null;

        foreach (var variant in database.AllModels)
        {
            if (variant == null || !variant.playable || variant.prefab == null) continue;
            if (chosen == null || variant.modelId == defaultId) chosen = variant.prefab;
        }

        return chosen != null ? chosen.GetComponent<ModelAppearance>() : null;
    }

    private static ModelAppearance.Group FindGroup(ModelAppearance appearance, string groupId)
    {
        foreach (var group in appearance.Groups)
            if (group.groupId == groupId) return group;

        return null;
    }

    // ── Customise block ───────────────────────────────────────────────────────

    private static void BuildHairRow(Transform block, ModelAppearance appearance)
    {
        var hair = block.Find("Hair");
        if (hair == null)
        {
            Debug.LogError($"[CreationBlocksBuilder] No Hair row under {CustomiseBlock}.");
            return;
        }

        var row = RowOn(hair);
        row.FindProperty("groupId").stringValue = HairStyleGroup;
        row.FindProperty("value").objectReferenceValue = Child<TextMeshProUGUI>(hair, "Text (TMP)");
        row.FindProperty("previousButton").objectReferenceValue = Child<Button>(hair, "Button (1)");
        row.FindProperty("nextButton").objectReferenceValue = Child<Button>(hair, "Button");
        row.ApplyModifiedPropertiesWithoutUndo();

        var group = FindGroup(appearance, HairStyleGroup);
        if (group == null) Debug.LogWarning($"[CreationBlocksBuilder] {appearance.name} has no {HairStyleGroup} group; the Hair row will stay hidden in play.");
        else SetText(Child<TextMeshProUGUI>(hair, "Text (TMP)"), group.options.Length > 0 ? group.options[0].displayName : "—");
    }

    private static void BuildColourRow(Transform block, ModelAppearance appearance)
    {
        var colour = block.Find("Hair color");
        var grid = colour != null ? colour.Find("Hair (1)") : null;
        if (grid == null)
        {
            Debug.LogError($"[CreationBlocksBuilder] No Hair color / Hair (1) swatch grid under {CustomiseBlock}.");
            return;
        }

        var group = FindGroup(appearance, HairColourGroup);
        if (group == null) Debug.LogWarning($"[CreationBlocksBuilder] {appearance.name} has no {HairColourGroup} group yet (Customisation tab → Hair colour → Write). Swatches left as they are; the row stays hidden in play.");
        else SyncSwatches(grid, group);

        var row = RowOn(colour);
        row.FindProperty("groupId").stringValue = HairColourGroup;
        row.FindProperty("label").objectReferenceValue = Child<TextMeshProUGUI>(colour, "Text (TMP)");
        row.FindProperty("selectedMarker").objectReferenceValue = Marker(grid);

        var buttons = Swatches(grid);
        var choices = row.FindProperty("choices");
        choices.arraySize = buttons.Count;
        for (int i = 0; i < buttons.Count; i++)
            choices.GetArrayElementAtIndex(i).objectReferenceValue = buttons[i];
        row.ApplyModifiedPropertiesWithoutUndo();
    }

    // One swatch per option in option order, coloured with its tint. The first swatch is the template for new ones.
    private static void SyncSwatches(Transform grid, ModelAppearance.Group group)
    {
        var buttons = Swatches(grid);
        if (buttons.Count == 0)
        {
            Debug.LogError("[CreationBlocksBuilder] The swatch grid needs at least one Button to copy.");
            return;
        }

        var template = buttons[0];
        while (buttons.Count < group.options.Length)
        {
            var copy = Object.Instantiate(template, grid);
            var copiedMarker = copy.transform.Find("Selected");
            if (copiedMarker != null) Object.DestroyImmediate(copiedMarker.gameObject);
            buttons.Add(copy);
        }

        for (int i = group.options.Length; i < buttons.Count; i++)
            Object.DestroyImmediate(buttons[i].gameObject);

        for (int i = 0; i < group.options.Length; i++)
        {
            var option = group.options[i];
            buttons[i].name = $"Swatch {option.optionId}";
            buttons[i].transform.SetSiblingIndex(i);
            buttons[i].image.color = option.tint;
        }
    }

    private static List<Button> Swatches(Transform grid)
    {
        var buttons = new List<Button>();
        foreach (Transform child in grid)
        {
            var button = child.GetComponent<Button>();
            if (button != null) buttons.Add(button);
        }
        return buttons;
    }

    // A hollow frame over the picked swatch, starting on the first. The panel moves it in play.
    private static RectTransform Marker(Transform grid)
    {
        foreach (var existing in grid.GetComponentsInChildren<RectTransform>(true))
            if (existing.name == "Selected") return existing;

        var swatches = Swatches(grid);
        if (swatches.Count == 0) return null;

        var go = new GameObject("Selected", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        var rect = (RectTransform)go.transform;
        rect.SetParent(swatches[0].transform, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(-3f, -3f);
        rect.offsetMax = new Vector2(3f, 3f);

        go.GetComponent<LayoutElement>().ignoreLayout = true;

        var image = go.GetComponent<Image>();
        image.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd");
        image.type = Image.Type.Sliced;
        image.fillCenter = false;
        image.raycastTarget = false;
        return rect;
    }

    // One row per slider group, in group order. Slider Entry is used for the first and copied for the rest.
    private static void BuildSliderRows(Transform block, ModelAppearance appearance)
    {
        var template = block.Find("Slider Entry");
        Transform previous = null;

        foreach (var group in appearance.Groups)
        {
            if (!group.chosenAtCreation || !group.slider) continue;

            var row = RowFor(block, group.groupId);
            if (row == null && template != null)
            {
                row = template;
                template = null;
            }
            if (row == null && previous != null) row = Object.Instantiate(previous.gameObject, block).transform;
            if (row == null)
            {
                Debug.LogError($"[CreationBlocksBuilder] No Slider Entry under {CustomiseBlock} to copy.");
                return;
            }

            if (previous != null) row.SetSiblingIndex(previous.GetSiblingIndex() + 1);
            row.name = $"Slider {group.displayName}";
            WireSlider(row, group);
            previous = row;
        }
    }

    private static void WireSlider(Transform row, ModelAppearance.Group group)
    {
        var label = Child<TextMeshProUGUI>(row, "Text (TMP)");
        var view = RowOn(row);
        view.FindProperty("groupId").stringValue = group.groupId;
        view.FindProperty("label").objectReferenceValue = label;
        view.FindProperty("slider").objectReferenceValue = Child<Slider>(row, "Slider");
        view.ApplyModifiedPropertiesWithoutUndo();
        SetText(label, group.displayName);
    }

    private static Transform RowFor(Transform block, string groupId)
    {
        foreach (var view in block.GetComponentsInChildren<AppearanceRowView>(true))
            if (view.GroupId == groupId) return view.transform;

        return null;
    }

    // ── RPG block ─────────────────────────────────────────────────────────────

    private static void BuildRpgBlock(Transform block, SerializedObject panelSo)
    {
        panelSo.FindProperty("nameInputField").objectReferenceValue = block.Find("Name Entry/InputField (TMP)")?.GetComponent<TMP_InputField>();
        panelSo.FindProperty("randomizeButton").objectReferenceValue = Randomize(block);
        panelSo.FindProperty("statPointPool").intValue = StatPool;
        panelSo.FindProperty("maxPerStat").intValue = StatCap;
        WireStats(block, panelSo);

        SetActive(block.Find("AllocationUi (1)"), false);
        SetActive(block.Find("Perks & Traits/Trait Entry"), false);
        SetActive(block.Find("Racial Effects/Racial Entry"), false);
    }

    private static Button Randomize(Transform block)
    {
        var buttons = block.Find("Stat Buttons");
        if (buttons == null) return null;

        foreach (Transform child in buttons)
            if (child.name.StartsWith("RandomizeButton")) return child.GetComponent<Button>();

        return null;
    }

    // The containers are copies of one Mind panel; in child order they become the cores in StatIds.Cores order.
    private static void WireStats(Transform block, SerializedObject panelSo)
    {
        var stats = block.Find("CoreStatsUi (1)");
        if (stats == null || stats.childCount < StatFields.Length)
        {
            Debug.LogError($"[CreationBlocksBuilder] CoreStatsUi (1) needs {StatFields.Length} stat containers.");
            return;
        }

        for (int i = 0; i < StatFields.Length; i++)
        {
            var container = stats.GetChild(i);
            container.name = $"{StatNames[i]}Container";

            var title = Ending<TextMeshProUGUI>(container, "Title");
            var amount = Ending<TextMeshProUGUI>(container, "Amount");
            if (title != null) title.name = $"{StatNames[i]}Title";
            if (amount != null) amount.name = $"{StatNames[i]}Amount";
            SetText(title, StatShort[i]);

            panelSo.FindProperty(StatFields[i]).objectReferenceValue = amount;
        }
    }

    // ── Panel ─────────────────────────────────────────────────────────────────

    private static void Rewire(CharacterCreationPanel panel, SerializedObject panelSo, Transform customise)
    {
        var rows = customise.Find("Rows");
        panelSo.FindProperty("rowParent").objectReferenceValue = rows;
        panelSo.FindProperty("sliderRowParent").objectReferenceValue = rows;
        panelSo.ApplyModifiedPropertiesWithoutUndo();

        SetActive(panel.transform.Find("BodyShapeBlock"), false);
        SetActive(panel.transform.Find("AppearanceRows"), false);
        SetActive(panel.transform.Find("RandomizeButton"), false);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static SerializedObject RowOn(Transform target)
    {
        var view = target.GetComponent<AppearanceRowView>();
        if (view == null) view = target.gameObject.AddComponent<AppearanceRowView>();
        return new SerializedObject(view);
    }

    private static T Child<T>(Transform parent, string name) where T : Component
    {
        var child = parent.Find(name);
        return child != null ? child.GetComponent<T>() : null;
    }

    private static T Ending<T>(Transform root, string suffix) where T : Component
    {
        foreach (var component in root.GetComponentsInChildren<T>(true))
            if (component.name.EndsWith(suffix)) return component;

        return null;
    }

    private static void SetText(TextMeshProUGUI text, string value)
    {
        if (text == null) return;
        text.text = value;
        EditorUtility.SetDirty(text);
    }

    private static void SetActive(Transform target, bool active)
    {
        if (target != null) target.gameObject.SetActive(active);
    }
}

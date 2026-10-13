using System;
using System.Collections.Generic;
using UnityEngine;

// The parts of a model that can change: which meshes show and which materials they wear. Each group
// shows exactly one of its options. A group is either chosen at creation (hair colour: saved in
// model.json, offered by the creation panel) or driven by equipment (an outfit: an equipped item names
// the option through ItemDefinition.appearanceGroup, and the group falls back to its unequipped option
// when the slot empties; equipment.json is the truth). ModelModule does the applying. Sits on the model
// prefab's root.
//
// Skins: a piece of clothing is made to sit over a skin trimmed to fit it, so a matched set shows its own
// trimmed skin. Any mix the skins don't list shows the fallback (the whole body), so nothing has a hole.
[Serializable]
public class AppearanceChoice
{
    public string groupId;
    public string optionId;
}

public class ModelAppearance : MonoBehaviour
{
    [Serializable]
    public class MaterialSwap
    {
        public Renderer renderer;
        public int slot;
        public Material material;
    }

    [Serializable]
    public class Option
    {
        public string optionId;
        public string displayName;

        [Tooltip("Shown when this option is picked, hidden when another option in the group is.")]
        public GameObject[] show = new GameObject[0];

        public MaterialSwap[] swaps = new MaterialSwap[0];
    }

    [Serializable]
    public class Group
    {
        public string groupId;
        public string displayName;

        [Tooltip("Picked once in character creation and saved with the model. Off: driven by equipment.")]
        public bool chosenAtCreation;

        [Tooltip("Equipment groups: shown while no equipped item drives the group. Empty = the first option.")]
        public string unequippedOptionId;

        public Option[] options = new Option[0];
    }

    [Serializable]
    public class Skin
    {
        public GameObject show;

        [Tooltip("Shown only while every one of these groups shows this option.")]
        public AppearanceChoice[] whenWearing = new AppearanceChoice[0];
    }

    [SerializeField] private Group[] groups = new Group[0];

    [Tooltip("Checked in order; the first whose options are all showing is the skin shown.")]
    [SerializeField] private Skin[] skins = new Skin[0];

    [Tooltip("Shown when no skin matches the mix being worn.")]
    [SerializeField] private GameObject fallbackSkin;

    private readonly Dictionary<string, string> showing = new Dictionary<string, string>();

    public IReadOnlyList<Group> Groups => groups;

    // Creation groups only; equipment groups are left as the equipment set them. A group missing from
    // the choices, or naming an option it doesn't have, shows its first option.
    public void Apply(IReadOnlyList<AppearanceChoice> choices)
    {
        foreach (var group in groups)
            if (group.chosenAtCreation) ApplyGroup(group, IndexOf(group, ChosenOptionId(group, choices)));
    }

    // Every group at its default: creation groups at their first option, equipment groups unequipped.
    public void ApplyDefaults()
    {
        foreach (var group in groups)
            ApplyGroup(group, DefaultIndex(group));
    }

    // False when the model has no such group or option, so the caller knows nothing changed.
    public bool Show(string groupId, string optionId)
    {
        Group group = Find(groupId);
        if (group == null) return false;

        int index = IndexOf(group, optionId);
        if (index < 0) return false;

        ApplyGroup(group, index);
        return true;
    }

    public void ShowDefault(string groupId)
    {
        Group group = Find(groupId);
        if (group != null) ApplyGroup(group, DefaultIndex(group));
    }

    public static int IndexOf(Group group, string optionId)
    {
        for (int i = 0; i < group.options.Length; i++)
            if (group.options[i].optionId == optionId) return i;

        return -1;
    }

    private Group Find(string groupId)
    {
        foreach (var group in groups)
            if (group.groupId == groupId) return group;

        return null;
    }

    private static int DefaultIndex(Group group)
    {
        return group.chosenAtCreation ? 0 : IndexOf(group, group.unequippedOptionId);
    }

    private static string ChosenOptionId(Group group, IReadOnlyList<AppearanceChoice> choices)
    {
        if (choices == null) return null;

        foreach (var choice in choices)
            if (choice != null && choice.groupId == group.groupId) return choice.optionId;

        return null;
    }

    private void ApplyGroup(Group group, int index)
    {
        if (group.options.Length == 0) return;
        if (index < 0) index = 0;

        foreach (var option in group.options)
            SetShown(option.show, false);

        var chosen = group.options[index];
        SetShown(chosen.show, true);

        foreach (var swap in chosen.swaps)
            ApplySwap(swap);

        showing[group.groupId] = chosen.optionId;
        ShowSkin();
    }

    private void ShowSkin()
    {
        if (skins.Length == 0 && fallbackSkin == null) return;

        Skin match = Array.Find(skins, Matches);
        foreach (var skin in skins)
            if (skin.show != null) skin.show.SetActive(false);

        if (fallbackSkin != null) fallbackSkin.SetActive(match == null);
        if (match != null && match.show != null) match.show.SetActive(true);
    }

    private bool Matches(Skin skin)
    {
        foreach (var worn in skin.whenWearing)
            if (Showing(worn.groupId) != worn.optionId) return false;

        return true;
    }

    // A freshly spawned model hasn't applied every group yet, so read the group off its meshes: the option
    // whose objects are on, or else the option that shows nothing.
    private string Showing(string groupId)
    {
        if (showing.TryGetValue(groupId, out string optionId)) return optionId;

        Group group = Find(groupId);
        if (group == null) return null;

        foreach (var option in group.options)
            if (option.show.Length > 0 && option.show[0] != null && option.show[0].activeSelf) return option.optionId;

        foreach (var option in group.options)
            if (option.show.Length == 0) return option.optionId;

        return null;
    }

    private static void SetShown(GameObject[] objects, bool shown)
    {
        foreach (var go in objects)
            if (go != null) go.SetActive(shown);
    }

    private static void ApplySwap(MaterialSwap swap)
    {
        if (swap == null || swap.renderer == null || swap.material == null) return;

        Material[] materials = swap.renderer.sharedMaterials;
        if (swap.slot < 0 || swap.slot >= materials.Length) return;

        materials[swap.slot] = swap.material;
        swap.renderer.sharedMaterials = materials;
    }
}

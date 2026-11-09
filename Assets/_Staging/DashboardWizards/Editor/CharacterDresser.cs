using System.Collections.Generic;
using CrabSystem;
using UnityEngine;

// Puts looks and gear on a model the way ModelModule does at runtime: equipment groups unequipped, then the creation
// picks, then each piece's look on the model and its equipped prefab on the slot's socket. Used by CharacterStage and by
// the models NPC Setup bakes into placed NPCs. Graduates with WizardTab.

public static class CharacterDresser
{
    public static void Dress(GameObject model, IReadOnlyList<AppearanceChoice> looks, IReadOnlyList<ItemDefinition> gear)
    {
        var appearance = model.GetComponent<ModelAppearance>();
        if (appearance != null) appearance.ApplyDefaults();
        if (appearance != null) appearance.Apply(looks);

        foreach (var item in gear)
            Wear(model, item, appearance);
    }

    // The prefab a model id names in the database, or null.
    public static GameObject ModelPrefab(ModelDatabase database, string modelId)
    {
        if (database == null || string.IsNullOrEmpty(modelId)) return null;

        var variant = database.GetModel(modelId);
        return variant != null ? variant.prefab : null;
    }

    // The items the ids name, in order; ids with no item are skipped.
    public static List<ItemDefinition> Items(List<ItemDefinition> all, IEnumerable<string> itemIds)
    {
        var found = new List<ItemDefinition>();
        if (itemIds == null) return found;

        foreach (string itemId in itemIds)
        {
            var item = all.Find(i => i.itemId == itemId);
            if (item != null) found.Add(item);
        }

        return found;
    }

    private static void Wear(GameObject model, ItemDefinition item, ModelAppearance appearance)
    {
        if (appearance != null && !string.IsNullOrEmpty(item.appearanceGroup)) appearance.Show(item.appearanceGroup, item.appearanceOption);
        if (item.equippedPrefab == null) return;

        var sockets = model.GetComponentInChildren<ModelSocketProvider>(true);
        var slot = item.subType != null ? item.subType.equipmentSlot : null;
        var socket = sockets != null ? sockets.GetSocket(slot) : null;
        if (socket == null) return;

        var visual = Object.Instantiate(item.equippedPrefab, socket);
        SocketScale.Normalise(visual.transform, item.equippedPrefab.transform.localScale);
    }
}

using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// Does this weapon prefab actually connect?
//
// A weapon with no hitbox equips, appears in hand and plays the full swing — and passes straight
// through everything. There is no error at any point, because nothing is wrong: AbilitySystem asks
// for WeaponHitbox components under the entity, finds none, and enables none.
//
// Shared by the weapon wizard (as warnings before it writes) and the asset report (as problems on
// existing items).
public static class WeaponHitboxCheck
{
    /// <summary>Distinct hitbox tags on the prefab. Empty string is a real, matchable tag.</summary>
    public static List<string> Tags(GameObject prefab)
    {
        List<string> tags = new List<string>();
        if (prefab == null) return tags;

        foreach (WeaponHitbox hitbox in prefab.GetComponentsInChildren<WeaponHitbox>(true))
            if (!tags.Contains(hitbox.HitboxTag))
                tags.Add(hitbox.HitboxTag);

        return tags;
    }

    public static List<string> Problems(GameObject prefab)
    {
        List<string> problems = new List<string>();
        if (prefab == null) return problems;

        WeaponHitbox[] hitboxes = prefab.GetComponentsInChildren<WeaponHitbox>(true);

        if (hitboxes.Length == 0)
        {
            problems.Add($"'{prefab.name}' carries no WeaponHitbox. It will equip, appear in hand and play the swing — and connect with nothing. AbilitySystem enables hitboxes found under the entity; there are none to enable, and it logs nothing.");
            return problems;
        }

        foreach (WeaponHitbox hitbox in hitboxes)
        {
            if (HasCollider(hitbox)) continue;

            problems.Add($"WeaponHitbox on '{hitbox.name}' has no Collider assigned and none on its own GameObject. It logs an error in Awake and never activates.");
        }

        List<string> tags = Tags(prefab);

        if (!AnyAbilityActivates(tags, out string unreachable))
            problems.Add(unreachable);

        return problems;
    }

    // Awake falls back to GetComponent when the field is empty, so either counts. The field is
    // private, hence the SerializedObject read rather than a guess.
    static bool HasCollider(WeaponHitbox hitbox)
    {
        if (hitbox.GetComponent<Collider>() != null) return true;

        SerializedProperty property = new SerializedObject(hitbox).FindProperty("hitboxCollider");
        return property != null && property.objectReferenceValue != null;
    }

    // An ability naming no tags activates every hitbox, so one such ability makes any tag reachable.
    static bool AnyAbilityActivates(List<string> tags, out string detail)
    {
        detail = null;

        foreach (AbilityDefinition ability in CrabWizardGUI.LoadAll<AbilityDefinition>())
        {
            if (ability.hitboxTags == null || ability.hitboxTags.Count == 0) return true;

            foreach (string wanted in ability.hitboxTags)
            {
                if (string.IsNullOrEmpty(wanted)) continue;

                foreach (string have in tags)
                    if (string.Equals(wanted, have, StringComparison.OrdinalIgnoreCase)) return true;
            }
        }

        string shown = tags.Count == 0 ? "(none)" : string.Join(", ", Quoted(tags));
        detail = $"No AbilityDefinition activates hitbox tag {shown}. Every ability names specific tags and none of them match, so no attack will ever open this hitbox.";
        return false;
    }

    static List<string> Quoted(List<string> values)
    {
        List<string> quoted = new List<string>(values.Count);

        foreach (string value in values)
            quoted.Add(string.IsNullOrEmpty(value) ? "''" : "'" + value + "'");

        return quoted;
    }
}

using System.Collections.Generic;
using NinjaGame.Stats;
using UnityEditor;
using UnityEngine;

// Tools → Crab → Dials → Set Up (10 Oct). Makes the two dial schemas (MovementStats, CombatDials), loads them on
// every base character prefab, names each resource's regen dials, points the Dash's push at mov.dash_speed and
// tags it "dodge". Rerunnable: schema entries are added if missing, never removed or renamed (the SecondryStats
// lesson: a renamed stat id silently zeroes).
public static class DialsBuilder
{
    const string Tag = "DialsBuilder";
    const string SchemaFolder = "Assets/Database/Resources/StatDatabase";
    const string ResourceFolder = "Assets/CrabSystem/Resources/ResourceDefinitions";
    const string DashPath = "Assets/Database/Resources/AbilityDatabase/Movement/Dash.asset";
    const string CharacterFolder = "Assets/Database/Characters";

    static readonly (string id, string name, string shortName, string description)[] Movement =
    {
        (DialIds.WalkSpeed, "Walk Speed", "WLK", "m/s added to walk speed."),
        (DialIds.RunSpeed, "Run Speed", "RUN", "m/s added to run speed."),
        (DialIds.SprintSpeed, "Sprint Speed", "SPR", "m/s added to sprint speed."),
        (DialIds.CrouchSpeed, "Crouch Speed", "CRH", "m/s added to crouch speed."),
        (DialIds.Accel, "Acceleration", "ACC", "Added to ground and air acceleration. Lower it with friction to keep top speed."),
        (DialIds.Friction, "Friction", "FRC", "Added to ground and momentum friction."),
        (DialIds.JumpSpeed, "Jump Speed", "JMP", "m/s added to the jump's upward speed."),
        (DialIds.AirJumpSpeed, "Air Jump Speed", "AJS", "m/s added to an air jump's upward speed."),
        (DialIds.AirJumps, "Air Jumps", "AJ", "Extra air jumps."),
        (DialIds.AirTurn, "Air Turn", "ATN", "Degrees per second added to air turning."),
        (DialIds.AirStrafe, "Air Strafe", "AST", "m/s added to air strafe."),
        (DialIds.WallJumps, "Wall Jumps", "WJ", "Extra wall jumps per airtime."),
        (DialIds.Mantles, "Mantles", "MNT", "Extra mantles per airtime."),
        (DialIds.DashSpeed, "Dash Speed", "DSH", "m/s added to the dash's push (and its cap)."),
    };

    static readonly (string id, string name, string shortName, string description)[] Combat =
    {
        (DialIds.ParryWindow, "Parry Window", "PRY", "Frames (60 fps) added to the parry window."),
        (DialIds.ParryStun, "Parry Stun", "PST", "Frames added to the flinch a parried attacker takes."),
        (DialIds.RiposteWindow, "Riposte Window", "RIP", "Seconds added to the riposte window after a parry."),
        (DialIds.BlockStamina, "Block Stamina", "BLK", "Added to the stamina a block costs. Negative is cheaper."),
        (DialIds.MaxPosture, "Max Posture", "PSM", "Added to the posture bar."),
        (DialIds.PostureRecovery, "Posture Recovery", "PSR", "Posture recovered per second, added."),
        (DialIds.DodgeInvuln, "Dodge I-Frames", "IFR", "Seconds added to an evasive move's invulnerability."),
        (DialIds.HealthRegen, "Health Regen", "HRG", "Added to health refill per second."),
        (DialIds.HealthRegenDelay, "Health Regen Delay", "HRD", "Seconds added before health refills."),
        (DialIds.StaminaRegen, "Stamina Regen", "SRG", "Added to stamina refill per second."),
        (DialIds.StaminaRegenDelay, "Stamina Regen Delay", "SRD", "Seconds added before stamina refills."),
        (DialIds.ManaRegen, "Mana Regen", "MRG", "Added to mana refill per second."),
        (DialIds.ManaRegenDelay, "Mana Regen Delay", "MRD", "Seconds added before mana refills."),
        (DialIds.ChargeRegen, "Charge Regen", "CRG", "Added to movement-charge refill per second."),
    };

    // resourceId → (rate dial, delay dial).
    static readonly Dictionary<string, (string rate, string delay)> ResourceDials = new()
    {
        { "health", (DialIds.HealthRegen, DialIds.HealthRegenDelay) },
        { "stamina", (DialIds.StaminaRegen, DialIds.StaminaRegenDelay) },
        { "mana", (DialIds.ManaRegen, DialIds.ManaRegenDelay) },
        { "movement_charges", (DialIds.ChargeRegen, "") },
    };

    [MenuItem("Tools/Crab/Dials/Set Up")]
    public static void Build()
    {
        Schema("MovementStats", Movement);
        Schema("CombatDials", Combat);
        LoadOnCharacters("MovementStats", "CombatDials");
        NameResourceDials();
        PointDash();
        AssetDatabase.SaveAssets();
        Debug.Log($"[{Tag}] Done. Every dial starts at 0; talents, gear and Tools → Crab → Dials → Build Burden Track move them.");
    }

    static void Schema(string assetName, (string id, string name, string shortName, string description)[] entries)
    {
        string path = $"{SchemaFolder}/{assetName}.asset";
        var schema = AssetDatabase.LoadAssetAtPath<StatSchema>(path);
        if (schema == null)
        {
            schema = ScriptableObject.CreateInstance<StatSchema>();
            AssetDatabase.CreateAsset(schema, path);
        }

        var so = new SerializedObject(schema);
        so.FindProperty("derived").boolValue = true;
        SerializedProperty list = so.FindProperty("entries");

        int added = 0;
        foreach ((string id, string name, string shortName, string description) in entries)
        {
            if (schema.Find(id) != null) continue;

            list.arraySize++;
            SerializedProperty entry = list.GetArrayElementAtIndex(list.arraySize - 1);
            entry.FindPropertyRelative("id").stringValue = id;
            entry.FindPropertyRelative("displayName").stringValue = name;
            entry.FindPropertyRelative("shortName").stringValue = shortName;
            entry.FindPropertyRelative("description").stringValue = description;
            entry.FindPropertyRelative("defaultValue").floatValue = 0f;
            entry.FindPropertyRelative("minValue").floatValue = -999f;
            entry.FindPropertyRelative("maxValue").floatValue = 999f;
            added++;
        }

        so.ApplyModifiedPropertiesWithoutUndo();
        Debug.Log($"[{Tag}] schema: {assetName} — {added} dials added, {list.arraySize} in all.");
    }

    // Base prefabs only: variants inherit the list.
    static void LoadOnCharacters(params string[] schemaIds)
    {
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { CharacterFolder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (asset == null || PrefabUtility.GetPrefabAssetType(asset) != PrefabAssetType.Regular) continue;
            if (asset.GetComponentInChildren<StatSystem>(true) == null) continue;

            GameObject root = PrefabUtility.LoadPrefabContents(path);
            var so = new SerializedObject(root.GetComponentInChildren<StatSystem>(true));
            SerializedProperty ids = so.FindProperty("schemaIds");

            int added = 0;
            foreach (string schemaId in schemaIds)
            {
                if (Contains(ids, schemaId)) continue;
                ids.arraySize++;
                ids.GetArrayElementAtIndex(ids.arraySize - 1).stringValue = schemaId;
                added++;
            }

            if (added > 0)
            {
                so.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, path);
                Debug.Log($"[{Tag}] character: {asset.name} loads the dial schemas.");
            }
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    static bool Contains(SerializedProperty list, string value)
    {
        for (int i = 0; i < list.arraySize; i++)
        {
            if (list.GetArrayElementAtIndex(i).stringValue == value) return true;
        }
        return false;
    }

    static void NameResourceDials()
    {
        foreach (string guid in AssetDatabase.FindAssets("t:ResourceDefinition", new[] { ResourceFolder }))
        {
            var resource = AssetDatabase.LoadAssetAtPath<ResourceDefinition>(AssetDatabase.GUIDToAssetPath(guid));
            if (resource == null || !ResourceDials.TryGetValue(resource.resourceId, out (string rate, string delay) dials)) continue;

            resource.regenStatId = dials.rate;
            resource.regenDelayStatId = dials.delay;
            EditorUtility.SetDirty(resource);
            Debug.Log($"[{Tag}] resource: {resource.resourceId} regen reads {dials.rate}{(dials.delay.Length > 0 ? " and " + dials.delay : "")}.");
        }
    }

    static void PointDash()
    {
        var dash = AssetDatabase.LoadAssetAtPath<AbilityDefinition>(DashPath);
        if (dash == null)
        {
            Debug.LogError($"[{Tag}] Missing {DashPath}.");
            return;
        }

        var so = new SerializedObject(dash);
        SerializedProperty effects = so.FindProperty("movementEffects");
        for (int i = 0; i < effects.arraySize; i++)
            effects.GetArrayElementAtIndex(i).FindPropertyRelative("speedStatId").stringValue = DialIds.DashSpeed;

        SerializedProperty tags = so.FindProperty("tags");
        if (!Contains(tags, "dodge"))
        {
            tags.arraySize++;
            tags.GetArrayElementAtIndex(tags.arraySize - 1).stringValue = "dodge";
        }

        so.ApplyModifiedPropertiesWithoutUndo();
        Debug.Log($"[{Tag}] ability: {dash.abilityId} pushes by mov.dash_speed and is tagged dodge.");
    }
}

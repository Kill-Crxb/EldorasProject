using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

/// <summary>
/// Creates a pre-filled LayerConfig asset for NinjaGame with the agreed layer
/// definitions and collision matrix rules.
///
/// Run via: Tools → CrabSystem → Create NinjaGame Layer Config
/// </summary>
public static class LayerConfigFactory
{
    [MenuItem("Tools/CrabSystem/Create NinjaGame Layer Config")]
    public static void CreateNinjaGameLayerConfig()
    {
        var config = ScriptableObject.CreateInstance<LayerConfig>();

        // ── Layer definitions ─────────────────────────────────────────────
        config.layers = new List<LayerDefinition>
        {
            new LayerDefinition { index = 0,  name = "Default",        isBuiltIn = true,  description = "Catch-all — keep things off this" },
            new LayerDefinition { index = 1,  name = "TransparentFX",  isBuiltIn = true,  description = "Unity built-in" },
            new LayerDefinition { index = 2,  name = "Ignore Raycast", isBuiltIn = true,  description = "Unity built-in" },
            new LayerDefinition { index = 3,  name = "Terrain",        isBuiltIn = false, description = "Ground, floors, static world geometry" },
            new LayerDefinition { index = 4,  name = "Water",          isBuiltIn = true,  description = "Unity built-in" },
            new LayerDefinition { index = 5,  name = "UI",             isBuiltIn = true,  description = "Unity built-in" },
            new LayerDefinition { index = 6,  name = "StaticProp",     isBuiltIn = false, description = "Non-interactive world objects — pillars, walls, rocks" },
            new LayerDefinition { index = 7,  name = "DynamicProp",    isBuiltIn = false, description = "Moveable/destructible objects — crates, chests, barrels" },
            new LayerDefinition { index = 8,  name = "Player",         isBuiltIn = false, description = "Player characters" },
            new LayerDefinition { index = 9,  name = "NPC",            isBuiltIn = false, description = "All non-player entities — enemies and friendlies alike" },
            new LayerDefinition { index = 10, name = "Projectile",     isBuiltIn = false, description = "Arrows, spells, thrown objects" },
            new LayerDefinition { index = 11, name = "Hitbox",         isBuiltIn = false, description = "Weapon hitboxes and attack volumes" },
            new LayerDefinition { index = 12, name = "Hurtbox",        isBuiltIn = false, description = "Entity vulnerable areas — where hits land" },
            new LayerDefinition { index = 13, name = "Trigger",        isBuiltIn = false, description = "Area triggers, quest volumes, zone detection" },
            new LayerDefinition { index = 14, name = "Interactable",   isBuiltIn = false, description = "Interactive objects — levers, doors, pickups" },
        };

        // ── Collision matrix rules ────────────────────────────────────────
        // Only non-obvious rules are noted. Anything not listed uses Unity default.
        config.collisionRules = new List<LayerCollisionRule>
        {
            // Terrain
            R(3,  6,  false, "Terrain ↔ StaticProp — no interaction needed"),
            R(3,  7,  true,  "Terrain ↔ DynamicProp — props rest on terrain"),
            R(3,  8,  true,  "Terrain ↔ Player — player walks on terrain"),
            R(3,  9,  true,  "Terrain ↔ NPC — NPCs walk on terrain"),
            R(3,  10, true,  "Terrain ↔ Projectile — arrows hit ground"),
            R(3,  11, true,  "Terrain ↔ Hitbox — weapons bounce off terrain (CQC penalty)"),
            R(3,  12, false, "Terrain ↔ Hurtbox — terrain doesn't hurt entities"),
            R(3,  13, false, "Terrain ↔ Trigger — no interaction"),
            R(3,  14, false, "Terrain ↔ Interactable — no interaction"),

            // StaticProp
            R(6,  7,  true,  "StaticProp ↔ DynamicProp — props collide with static world"),
            R(6,  8,  true,  "StaticProp ↔ Player — player collides with walls/pillars"),
            R(6,  9,  true,  "StaticProp ↔ NPC — NPCs collide with walls"),
            R(6,  10, true,  "StaticProp ↔ Projectile — projectiles hit walls"),
            R(6,  11, true,  "StaticProp ↔ Hitbox — weapons bounce off walls"),
            R(6,  12, false, "StaticProp ↔ Hurtbox — walls don't register hits"),
            R(6,  13, false, "StaticProp ↔ Trigger — no interaction"),
            R(6,  14, false, "StaticProp ↔ Interactable — no interaction"),

            // DynamicProp
            R(7,  8,  true,  "DynamicProp ↔ Player — player pushes props"),
            R(7,  9,  true,  "DynamicProp ↔ NPC — NPCs interact with props"),
            R(7,  10, true,  "DynamicProp ↔ Projectile — projectiles hit props"),
            R(7,  11, false, "DynamicProp ↔ Hitbox — weapons don't physically push props (handled by damage)"),
            R(7,  12, false, "DynamicProp ↔ Hurtbox — no interaction"),
            R(7,  13, false, "DynamicProp ↔ Trigger — no interaction"),
            R(7,  14, false, "DynamicProp ↔ Interactable — no interaction"),

            // Player
            R(8,  9,  true,  "Player ↔ NPC — players and NPCs collide physically"),
            R(8,  10, true,  "Player ↔ Projectile — projectiles hit player"),
            R(8,  11, false, "Player ↔ Hitbox — player body not directly hittable, only Hurtbox"),
            R(8,  12, false, "Player ↔ Hurtbox — hurtboxes not on Player layer"),
            R(8,  13, true,  "Player ↔ Trigger — player enters trigger zones"),
            R(8,  14, true,  "Player ↔ Interactable — player can interact with objects"),

            // NPC
            R(9,  10, true,  "NPC ↔ Projectile — projectiles hit NPCs"),
            R(9,  11, false, "NPC ↔ Hitbox — NPC body not directly hittable, only Hurtbox"),
            R(9,  12, false, "NPC ↔ Hurtbox — no interaction"),
            R(9,  13, true,  "NPC ↔ Trigger — NPCs can enter trigger zones"),
            R(9,  14, false, "NPC ↔ Interactable — NPCs don't interact with objects"),

            // Projectile
            R(10, 11, true,  "Projectile ↔ Hitbox — players can parry projectiles with a weapon"),
            R(10, 12, true,  "Projectile ↔ Hurtbox — projectiles deal damage"),
            R(10, 13, false, "Projectile ↔ Trigger — no interaction"),
            R(10, 14, false, "Projectile ↔ Interactable — no interaction"),

            // Hitbox
            R(11, 11, true,  "Hitbox ↔ Hitbox — weapon clashing, skill ceiling mechanic"),
            R(11, 12, true,  "Hitbox ↔ Hurtbox — primary damage detection path"),
            R(11, 13, false, "Hitbox ↔ Trigger — no interaction"),
            R(11, 14, false, "Hitbox ↔ Interactable — no interaction"),

            // Hurtbox
            R(12, 12, false, "Hurtbox ↔ Hurtbox — hurtboxes don't interact with each other"),
            R(12, 13, false, "Hurtbox ↔ Trigger — no interaction"),
            R(12, 14, false, "Hurtbox ↔ Interactable — no interaction"),

            // Trigger / Interactable
            R(13, 14, false, "Trigger ↔ Interactable — no interaction"),
        };

        string path = "Assets/CrabSystem/Configs/NinjaGameLayerConfig.asset";

        // Ensure directory exists
        System.IO.Directory.CreateDirectory("Assets/CrabSystem/Configs");
        AssetDatabase.CreateAsset(config, path);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.FocusProjectWindow();
        Selection.activeObject = config;

        Debug.Log($"[LayerConfigFactory] Created NinjaGame LayerConfig at {path}");
        EditorUtility.DisplayDialog("LayerConfig Created",
            $"NinjaGame LayerConfig created at:\n{path}\n\nOpen Tools → CrabSystem → Layer Manager and assign it.",
            "OK");
    }

    private static LayerCollisionRule R(int a, int b, bool collides, string note = "")
    {
        return new LayerCollisionRule
        {
            layerIndexA = a,
            layerIndexB = b,
            collides = collides,
            note = note,
        };
    }
}
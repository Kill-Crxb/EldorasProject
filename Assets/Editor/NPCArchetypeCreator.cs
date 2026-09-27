#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using RPG.Factions;

public class NPCArchetypeCreator : EditorWindow
{
    private string archetypeName = "Bear";
    private FactionDefinition faction;
    private NPCType npcType = NPCType.Beast;
    private NPCImportance importance = NPCImportance.Soldier;

    private int mind = 5;
    private int body = 18;
    private int spirit = 8;
    private int resilience = 15;
    private int endurance = 20;
    private int insight = 8;

    private string modelId = "bear_brown";
    private string combatBehaviorClassName = "BearCombatBehavior";
    private AbilityDefinition[] abilities = new AbilityDefinition[0];

    private Vector2 scrollPosition;

    [MenuItem("Window/RPG/NPC Archetype Creator")]
    public static void ShowWindow()
    {
        GetWindow<NPCArchetypeCreator>("Archetype Creator");
    }

    private void OnGUI()
    {
        scrollPosition = GUILayout.BeginScrollView(scrollPosition);

        GUILayout.Label("NPC Archetype Creator", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        GUILayout.Label("Quick Presets", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Bear Soldier"))
            LoadBearSoldierPreset();
        if (GUILayout.Button("Bear Elite"))
            LoadBearElitePreset();
        if (GUILayout.Button("Bear Boss"))
            LoadBearBossPreset();
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.Space();

        GUILayout.Label("Identity", EditorStyles.boldLabel);
        archetypeName = EditorGUILayout.TextField("Archetype Name", archetypeName);
        faction = (FactionDefinition)EditorGUILayout.ObjectField("Faction", faction, typeof(FactionDefinition), false);
        npcType = (NPCType)EditorGUILayout.EnumPopup("NPC Type", npcType);
        importance = (NPCImportance)EditorGUILayout.EnumPopup("Importance", importance);
        EditorGUILayout.Space();

        GUILayout.Label("Base Stats (Level 1)", EditorStyles.boldLabel);
        mind = EditorGUILayout.IntField("Mind", mind);
        body = EditorGUILayout.IntField("Body", body);
        spirit = EditorGUILayout.IntField("Spirit", spirit);
        resilience = EditorGUILayout.IntField("Resilience", resilience);
        endurance = EditorGUILayout.IntField("Endurance", endurance);
        insight = EditorGUILayout.IntField("Insight", insight);
        EditorGUILayout.Space();

        GUILayout.Label("Model", EditorStyles.boldLabel);
        modelId = EditorGUILayout.TextField("Model ID", modelId);
        EditorGUILayout.Space();

        GUILayout.Label("Combat", EditorStyles.boldLabel);
        combatBehaviorClassName = EditorGUILayout.TextField("Combat Behavior Class", combatBehaviorClassName);
        EditorGUILayout.Space();

        GUILayout.Label("Abilities", EditorStyles.boldLabel);
        SerializedObject so = new SerializedObject(this);
        SerializedProperty abilitiesProperty = so.FindProperty("abilities");
        EditorGUILayout.PropertyField(abilitiesProperty, true);
        so.ApplyModifiedProperties();
        EditorGUILayout.Space();

        EditorGUILayout.Space();
        if (GUILayout.Button("Create Archetype Asset", GUILayout.Height(30)))
        {
            CreateArchetype();
        }

        EditorGUILayout.Space();
        EditorGUILayout.HelpBox(
            "This will create an archetype asset at:\n" +
            $"Resources/NPCArchetypes/{GetArchetypeFileName()}.asset",
            MessageType.Info
        );

        GUILayout.EndScrollView();
    }

    private void LoadBearSoldierPreset()
    {
        archetypeName = "Bear";
        faction = FindFactionAsset("faction_wildlife");
        npcType = NPCType.Beast;
        importance = NPCImportance.Soldier;
        mind = 5;
        body = 18;
        spirit = 8;
        resilience = 15;
        endurance = 20;
        insight = 8;
        modelId = "bear_brown";
        combatBehaviorClassName = "BearCombatBehavior";
    }

    private void LoadBearElitePreset()
    {
        archetypeName = "Bear Elite";
        faction = FindFactionAsset("faction_wildlife");
        npcType = NPCType.Beast;
        importance = NPCImportance.Elite;
        mind = 6;
        body = 25;
        spirit = 10;
        resilience = 20;
        endurance = 30;
        insight = 10;
        modelId = "bear_brown";
        combatBehaviorClassName = "BearCombatBehavior";
    }

    private void LoadBearBossPreset()
    {
        archetypeName = "Bear King";
        faction = FindFactionAsset("faction_wildlife");
        npcType = NPCType.Beast;
        importance = NPCImportance.Boss;
        mind = 8;
        body = 40;
        spirit = 12;
        resilience = 30;
        endurance = 50;
        insight = 12;
        modelId = "bear_brown";
        combatBehaviorClassName = "BearCombatBehavior";
    }

    private static FactionDefinition FindFactionAsset(string factionId)
    {
        foreach (var guid in AssetDatabase.FindAssets("t:FactionDefinition"))
        {
            var asset = AssetDatabase.LoadAssetAtPath<FactionDefinition>(AssetDatabase.GUIDToAssetPath(guid));
            if (asset != null && asset.FactionId == factionId)
                return asset;
        }
        return null;
    }

    private string GetArchetypeFileName()
    {
        return $"archetype_{npcType.ToString().ToLower()}_{importance.ToString().ToLower()}";
    }

    private void CreateArchetype()
    {
        var archetype = ScriptableObject.CreateInstance<NPCArchetype>();

        archetype.archetypeId = GetArchetypeFileName();
        archetype.archetypeName = archetypeName;
        archetype.faction = faction;
        archetype.npcType = npcType;
        archetype.importance = importance;

        archetype.baseStatOverrides = new StatBaseOverride[]
        {
            new StatBaseOverride { statId = "core.mind", baseValue = mind },
            new StatBaseOverride { statId = "core.body", baseValue = body },
            new StatBaseOverride { statId = "core.spirit", baseValue = spirit },
            new StatBaseOverride { statId = "core.resilience", baseValue = resilience },
            new StatBaseOverride { statId = "core.endurance", baseValue = endurance },
            new StatBaseOverride { statId = "core.insight", baseValue = insight },
        };

        archetype.abilities = new List<AbilityDefinition>(abilities);
        archetype.combatBehaviorClassName = combatBehaviorClassName;
        archetype.modelPool = new List<string> { modelId };
        archetype.randomizeModel = false;

        archetype.useGenericName = true;
        archetype.genericName = archetypeName;

        archetype.aggressiveToHostileFactions = true;
        archetype.assistsAlliedFactions = false;
        archetype.defendsFactionMembers = true;

        string resourcesPath = "Assets/Resources";
        if (!AssetDatabase.IsValidFolder(resourcesPath))
        {
            AssetDatabase.CreateFolder("Assets", "Resources");
        }

        string archetypesPath = "Assets/Resources/NPCArchetypes";
        if (!AssetDatabase.IsValidFolder(archetypesPath))
        {
            AssetDatabase.CreateFolder("Assets/Resources", "NPCArchetypes");
        }

        string assetPath = $"{archetypesPath}/{GetArchetypeFileName()}.asset";
        AssetDatabase.CreateAsset(archetype, assetPath);
        AssetDatabase.SaveAssets();

        EditorUtility.FocusProjectWindow();
        Selection.activeObject = archetype;

        Debug.Log($"Created archetype: {assetPath}");
    }
}
#endif
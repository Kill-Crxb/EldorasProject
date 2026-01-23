#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

/// <summary>
/// Editor utility for quickly creating NPC Archetypes with common presets
/// Window → RPG → NPC Archetype Creator
/// </summary>
public class NPCArchetypeCreator : EditorWindow
{
    private string archetypeName = "Bear";
    private NPCFaction faction = NPCFaction.Wildlife;
    private NPCType npcType = NPCType.Beast;
    private NPCImportance importance = NPCImportance.Soldier;

    // Stats
    private int body = 18;
    private int endurance = 20;
    private float healthMultiplier = 1.2f;
    private float damageMultiplier = 1.0f;

    // Model
    private string modelId = "bear_brown";

    // Combat
    private string combatBehaviorClassName = "BearCombatBehavior";

    // Abilities
    private AbilityDefinition[] abilities = new AbilityDefinition[0];

    [MenuItem("Window/RPG/NPC Archetype Creator")]
    public static void ShowWindow()
    {
        GetWindow<NPCArchetypeCreator>("Archetype Creator");
    }

    private void OnGUI()
    {
        GUILayout.Label("NPC Archetype Creator", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        // Quick Presets
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

        // Identity
        GUILayout.Label("Identity", EditorStyles.boldLabel);
        archetypeName = EditorGUILayout.TextField("Archetype Name", archetypeName);
        faction = (NPCFaction)EditorGUILayout.EnumPopup("Faction", faction);
        npcType = (NPCType)EditorGUILayout.EnumPopup("NPC Type", npcType);
        importance = (NPCImportance)EditorGUILayout.EnumPopup("Importance", importance);
        EditorGUILayout.Space();

        // Stats
        GUILayout.Label("Stats", EditorStyles.boldLabel);
        body = EditorGUILayout.IntField("Body", body);
        endurance = EditorGUILayout.IntField("Endurance", endurance);
        healthMultiplier = EditorGUILayout.FloatField("Health Multiplier", healthMultiplier);
        damageMultiplier = EditorGUILayout.FloatField("Damage Multiplier", damageMultiplier);
        EditorGUILayout.Space();

        // Model
        GUILayout.Label("Model", EditorStyles.boldLabel);
        modelId = EditorGUILayout.TextField("Model ID", modelId);
        EditorGUILayout.Space();

        // Combat
        GUILayout.Label("Combat", EditorStyles.boldLabel);
        combatBehaviorClassName = EditorGUILayout.TextField("Combat Behavior Class", combatBehaviorClassName);
        EditorGUILayout.Space();

        // Abilities
        GUILayout.Label("Abilities", EditorStyles.boldLabel);
        SerializedObject so = new SerializedObject(this);
        SerializedProperty abilitiesProperty = so.FindProperty("abilities");
        EditorGUILayout.PropertyField(abilitiesProperty, true);
        so.ApplyModifiedProperties();
        EditorGUILayout.Space();

        // Create Button
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
    }

    private void LoadBearSoldierPreset()
    {
        archetypeName = "Bear";
        faction = NPCFaction.Wildlife;
        npcType = NPCType.Beast;
        importance = NPCImportance.Soldier;
        body = 18;
        endurance = 20;
        healthMultiplier = 1.2f;
        damageMultiplier = 1.0f;
        modelId = "bear_brown";
        combatBehaviorClassName = "BearCombatBehavior";
    }

    private void LoadBearElitePreset()
    {
        archetypeName = "Bear";
        faction = NPCFaction.Wildlife;
        npcType = NPCType.Beast;
        importance = NPCImportance.Elite;
        body = 25;
        endurance = 30;
        healthMultiplier = 1.8f;
        damageMultiplier = 1.2f;
        modelId = "bear_brown";
        combatBehaviorClassName = "BearCombatBehavior";
    }

    private void LoadBearBossPreset()
    {
        archetypeName = "Bear King";
        faction = NPCFaction.Wildlife;
        npcType = NPCType.Beast;
        importance = NPCImportance.Boss;
        body = 40;
        endurance = 50;
        healthMultiplier = 3.0f;
        damageMultiplier = 1.5f;
        modelId = "bear_brown";
        combatBehaviorClassName = "BearCombatBehavior";
    }

    private string GetArchetypeFileName()
    {
        return $"archetype_{faction.ToString().ToLower()}_{npcType.ToString().ToLower()}_{importance.ToString().ToLower()}";
    }

    private void CreateArchetype()
    {
        // Create the archetype
        var archetype = ScriptableObject.CreateInstance<NPCArchetype>();

        // Set identity
        archetype.archetypeId = GetArchetypeFileName();
        archetype.archetypeName = archetypeName;
        archetype.faction = faction;
        archetype.npcType = npcType;
        archetype.importance = importance;

        // Set stats
        archetype.baseStats = new StatAllocation
        {
            mind = 5,
            body = body,
            spirit = 8,
            resilience = 15,
            endurance = endurance,
            insight = 8,
            healthMultiplier = healthMultiplier,
            damageMultiplier = damageMultiplier
        };

        // Set abilities
        archetype.abilities = new System.Collections.Generic.List<AbilityDefinition>(abilities);

        // Set combat behavior
        archetype.combatBehaviorClassName = combatBehaviorClassName;

        // Set model
        archetype.modelPool = new System.Collections.Generic.List<string> { modelId };
        archetype.randomizeModel = false;

        // Set name
        archetype.useGenericName = true;
        archetype.genericName = archetypeName;

        // Set faction settings
        archetype.aggressiveToHostileFactions = true;
        archetype.assistsAlliedFactions = false;
        archetype.defendsFactionMembers = true;

        // Create Resources folder if it doesn't exist
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

        // Save the asset
        string assetPath = $"{archetypesPath}/{GetArchetypeFileName()}.asset";
        AssetDatabase.CreateAsset(archetype, assetPath);
        AssetDatabase.SaveAssets();

        // Ping the asset
        EditorUtility.FocusProjectWindow();
        Selection.activeObject = archetype;

        Debug.Log($"Created archetype: {assetPath}");
    }
}
#endif
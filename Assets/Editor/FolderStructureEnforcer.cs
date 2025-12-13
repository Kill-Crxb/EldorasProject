using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;
using System.Linq;

/// <summary>
/// Unity Editor tool to enforce NinjaGame package folder structure.
/// Features:
/// - Validates current file locations
/// - Auto-migrates files to correct folders
/// - Generates missing folders
/// - Validates new script placements
/// </summary>
public class FolderStructureEnforcer : EditorWindow
{
    private const string ROOT_FOLDER = "Assets/NinjaGame";

    private Vector2 scrollPosition;
    private bool showValidationResults = true;
    private bool showMigrationOptions = true;
    private List<FileValidationResult> validationResults = new List<FileValidationResult>();

    // Statistics
    private int correctFiles = 0;
    private int misplacedFiles = 0;
    private int missingFolders = 0;

    [MenuItem("NinjaGame/Tools/Folder Structure Enforcer")]
    public static void ShowWindow()
    {
        var window = GetWindow<FolderStructureEnforcer>("Folder Structure Enforcer");
        window.minSize = new Vector2(800, 600);
        window.Show();
    }

    void OnEnable()
    {
        ValidateStructure();
    }

    void OnGUI()
    {
        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("NinjaGame Package Folder Structure Tool", EditorStyles.boldLabel);
        EditorGUILayout.Space(5);

        DrawStatistics();
        EditorGUILayout.Space(10);

        DrawActionButtons();
        EditorGUILayout.Space(10);

        DrawValidationResults();
    }

    void DrawStatistics()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("Structure Analysis", EditorStyles.boldLabel);

        EditorGUILayout.LabelField($"✅ Correctly Placed: {correctFiles} files", EditorStyles.label);

        GUIStyle warningStyle = new GUIStyle(EditorStyles.label);
        warningStyle.normal.textColor = misplacedFiles > 0 ? Color.yellow : Color.green;
        EditorGUILayout.LabelField($"⚠️ Misplaced Files: {misplacedFiles} files", warningStyle);

        GUIStyle errorStyle = new GUIStyle(EditorStyles.label);
        errorStyle.normal.textColor = missingFolders > 0 ? new Color(1f, 0.5f, 0f) : Color.green;
        EditorGUILayout.LabelField($"📁 Missing Folders: {missingFolders} folders", errorStyle);

        EditorGUILayout.EndVertical();
    }

    void DrawActionButtons()
    {
        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("🔄 Refresh Validation", GUILayout.Height(30)))
        {
            ValidateStructure();
        }

        if (GUILayout.Button("📁 Create Missing Folders", GUILayout.Height(30)))
        {
            CreateAllFolders();
        }

        GUI.enabled = misplacedFiles > 0;
        if (GUILayout.Button("🚀 Auto-Migrate All Files", GUILayout.Height(30)))
        {
            if (EditorUtility.DisplayDialog(
                "Confirm Migration",
                $"This will move {misplacedFiles} files to their correct locations.\n\nThis action can be undone with Edit > Undo.\n\nContinue?",
                "Migrate Files",
                "Cancel"))
            {
                MigrateAllFiles();
            }
        }
        GUI.enabled = true;

        EditorGUILayout.EndHorizontal();
    }

    void DrawValidationResults()
    {
        showValidationResults = EditorGUILayout.Foldout(showValidationResults, "Validation Results", true);

        if (!showValidationResults) return;

        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

        foreach (var result in validationResults)
        {
            DrawFileResult(result);
        }

        EditorGUILayout.EndScrollView();
    }

    void DrawFileResult(FileValidationResult result)
    {
        Color bgColor = result.IsCorrect ? new Color(0.2f, 0.4f, 0.2f, 0.3f) : new Color(0.5f, 0.3f, 0f, 0.3f);

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        // File name and status
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField(result.IsCorrect ? "✅" : "⚠️", GUILayout.Width(30));
        EditorGUILayout.LabelField(result.FileName, EditorStyles.boldLabel);
        EditorGUILayout.EndHorizontal();

        // Current location
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Current:", GUILayout.Width(80));
        EditorGUILayout.LabelField(result.CurrentPath, EditorStyles.miniLabel);
        EditorGUILayout.EndHorizontal();

        // Expected location (if misplaced)
        if (!result.IsCorrect)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Expected:", GUILayout.Width(80));
            GUIStyle expectedStyle = new GUIStyle(EditorStyles.miniLabel);
            expectedStyle.normal.textColor = Color.cyan;
            EditorGUILayout.LabelField(result.ExpectedPath, expectedStyle);
            EditorGUILayout.EndHorizontal();

            // Individual move button
            if (GUILayout.Button("Move to Correct Location", GUILayout.Height(25)))
            {
                MoveFile(result);
            }
        }

        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(2);
    }

    void ValidateStructure()
    {
        validationResults.Clear();
        correctFiles = 0;
        misplacedFiles = 0;

        // Find all C# scripts in the project
        string[] allScripts = Directory.GetFiles("Assets", "*.cs", SearchOption.AllDirectories);

        foreach (string scriptPath in allScripts)
        {
            // Skip Editor scripts, Tests outside NinjaGame, etc.
            if (scriptPath.Contains("/Editor/") && !scriptPath.Contains("NinjaGame"))
                continue;

            string fileName = Path.GetFileName(scriptPath);
            string expectedPath = GetExpectedPath(fileName);

            if (expectedPath == null)
                continue; // Not a NinjaGame script

            string normalizedCurrent = scriptPath.Replace("\\", "/");
            string normalizedExpected = expectedPath.Replace("\\", "/");

            bool isCorrect = normalizedCurrent == normalizedExpected;

            validationResults.Add(new FileValidationResult
            {
                FileName = fileName,
                CurrentPath = normalizedCurrent,
                ExpectedPath = normalizedExpected,
                IsCorrect = isCorrect
            });

            if (isCorrect)
                correctFiles++;
            else
                misplacedFiles++;
        }

        // Check for missing folders
        missingFolders = CountMissingFolders();

        // Sort: misplaced first, then alphabetically
        validationResults = validationResults
            .OrderBy(r => r.IsCorrect)
            .ThenBy(r => r.FileName)
            .ToList();

        Debug.Log($"[Folder Structure Enforcer] Validation complete: {correctFiles} correct, {misplacedFiles} misplaced, {missingFolders} missing folders");
    }

    string GetExpectedPath(string fileName)
    {
        // This is the mapping logic - maps each file to its correct location
        // Returns null if file is not part of NinjaGame package

        var mapping = GetFolderMapping();

        if (mapping.ContainsKey(fileName))
        {
            return $"{ROOT_FOLDER}/{mapping[fileName]}/{fileName}";
        }

        return null; // Not a NinjaGame file
    }

    Dictionary<string, string> GetFolderMapping()
    {
        return new Dictionary<string, string>
        {
            // Core/Brain
            { "ControllerBrain.cs", "Core/Brain" },
            { "EntityType.cs", "Core/Brain" },
            { "IBrainModule.cs", "Core/Brain/Interfaces" },
            { "IPhysicsModule.cs", "Core/Brain/Interfaces" },
            { "IPlayerModule.cs", "Core/Brain/Interfaces" },
            { "IStaticModule.cs", "Core/Brain/Interfaces" },
            { "IInputHandler.cs", "Core/Brain/Interfaces" },
            
            // Core/Coordinators
            { "ProviderCoordinator.cs", "Core/Coordinators/Base" },
            { "CameraCoordinator.cs", "Core/Coordinators" },
            { "CombatProviderCoordinator.cs", "Core/Coordinators" },
            { "IdentityProviderCoordinator.cs", "Core/Coordinators" },
            { "InputProviderCoordinator.cs", "Core/Coordinators" },
            { "InventoryProviderCoordinator.cs", "Core/Coordinators" },
            { "MovementProviderCoordinator.cs", "Core/Coordinators" },
            { "StatsProviderCoordinator.cs", "Core/Coordinators" },
            
            // Core/Utilities
            { "Timer.cs", "Core/Utilities" },
            { "CountdownTimer.cs", "Core/Utilities" },
            { "IntervalTimer.cs", "Core/Utilities" },
            { "StopwatchTimer.cs", "Core/Utilities" },
            { "FrequencyTimer.cs", "Core/Utilities" },
            
            // Combat/Damage
            { "DamageModule.cs", "Combat/Damage" },
            { "IDamageable.cs", "Combat/Damage" },
            { "CombatAttackData.cs", "Combat/Damage/Data" },
            { "CombatDamagePacket.cs", "Combat/Damage/Data" },
            { "NPCDamageable.cs", "Combat/Damage/NPC" },
            
            // Combat/Abilities
            { "AbilityModule.cs", "Combat/Abilities" },
            { "AbilityLoadoutModule.cs", "Combat/Abilities" },
            { "ActiveDefenseModule.cs", "Combat/Abilities" },
            { "IAbilityProvider.cs", "Combat/Abilities/Interfaces" },
            { "AbilityDefinition.cs", "Combat/Abilities/Data" },
            { "AbilitySlotData.cs", "Combat/Abilities/Data" },
            { "DefenseAbilityData.cs", "Combat/Abilities/Data" },
            { "NaturalWeaponHitbox.cs", "Combat/Abilities/NaturalWeapons" },
            { "NaturalWeaponAnimationEvents.cs", "Combat/Abilities/NaturalWeapons" },
            { "NaturalWeaponInitializer.cs", "Combat/Abilities/NaturalWeapons" },
            { "NullAbilityProvider.cs", "Combat/Abilities/NullProviders" },
            
            // Combat/Defense
            { "IDefenseProvider.cs", "Combat/Defense/Interfaces" },
            { "IDefenseCapability.cs", "Combat/Defense/Interfaces" },
            { "NullDefenseProvider.cs", "Combat/Defense/NullProviders" },
            
            // Combat/Effects
            { "EffectManager.cs", "Combat/Effects" },
            { "DamageEffect.cs", "Combat/Effects" },
            { "DamageOverTimeEffect.cs", "Combat/Effects" },
            { "HealEffect.cs", "Combat/Effects" },
            { "HealOverTimeEffect.cs", "Combat/Effects" },
            { "KnockbackEffect.cs", "Combat/Effects" },
            { "MovementEffect.cs", "Combat/Effects" },
            
            // Combat/Weapons
            { "WeaponData.cs", "Combat/Weapons/Data" },
            
            // Stats/Core
            { "RPGCoreStats.cs", "Stats/Core" },
            { "RPGSecondaryStats.cs", "Stats/Core" },
            { "RPGResources.cs", "Stats/Core" },
            { "StatMaskFlags.cs", "Stats/Core" },
            { "ResourceMaskFlags.cs", "Stats/Core" },
            
            // Stats/Health
            { "IHealthProvider.cs", "Stats/Health/Interfaces" },
            { "NullHealthProvider.cs", "Stats/Health/NullProviders" },
            { "InvincibleHealthProvider.cs", "Stats/Health/NullProviders" },
            
            // Stats/Resources
            { "IResourceProvider.cs", "Stats/Resources/Interfaces" },
            { "NullResourceProvider.cs", "Stats/Resources/NullProviders" },
            
            // Stats/CombatStats
            { "ICombatStatsProvider.cs", "Stats/CombatStats/Interfaces" },
            { "NullCombatStats.cs", "Stats/CombatStats/NullProviders" },
            
            // Movement/Player
            { "ThirdPersonController.cs", "Movement/Player" },
            { "LocomotionModule.cs", "Movement/Player" },
            { "FeetDetectionModule.cs", "Movement/Player" },
            
            // Movement/NPC
            { "NPCMovementModule.cs", "Movement/NPC" },
            
            // Movement/Animation
            { "AnimationProvider.cs", "Movement/Animation" },
            { "AnimationStateModule.cs", "Movement/Animation" },
            { "AnimationEventForwarder.cs", "Movement/Animation" },
            { "AnimationEventType.cs", "Movement/Animation" },
            { "NullAnimationProvider.cs", "Movement/Animation/NullProviders" },
            
            // Movement/Interfaces
            { "IMovementProvider.cs", "Movement/Interfaces" },
            { "IMovementState.cs", "Movement/Interfaces" },
            { "IAnimationProvider.cs", "Movement/Interfaces" },
            
            // Movement/NullProviders
            { "StaticMovementProvider.cs", "Movement/NullProviders" },
            
            // Input
            { "InputModule.cs", "Input" },
            { "PlayerAbilityInputHandler.cs", "Input" },
            { "IInputProvider.cs", "Input/Interfaces" },
            
            // Camera
            { "SimpleThirdPersonCamera.cs", "Camera" },
            { "MMOStyleCamera.cs", "Camera" },
            { "TargetLockModule.cs", "Camera" },
            { "BasicTargetable.cs", "Camera" },
            { "ICameraProvider.cs", "Camera/Interfaces" },
            { "ICameraImplementation.cs", "Camera/Interfaces" },
            
            // AI/Core
            { "AIModule.cs", "AI/Core" },
            { "AIStateUpdater.cs", "AI/Core" },
            { "AITargetDetection.cs", "AI/Core" },
            { "AIDebugVisualizer.cs", "AI/Core" },
            
            // AI/Combat/Base
            { "AICombatBehaviorModule.cs", "AI/Combat/Base" },
            { "CombatStateMachine.cs", "AI/Combat/Base" },
            { "TacticalCombatBehavior.cs", "AI/Combat/Base" },
            { "IAICombatBehavior.cs", "AI/Combat/Base/Interfaces" },
            
            // AI/Combat/Implementations/Bear
            { "BearCombatBehavior.cs", "AI/Combat/Implementations/Bear" },
            { "BearCombatStateMachine.cs", "AI/Combat/Implementations/Bear" },
            { "BearCombatStates.cs", "AI/Combat/Implementations/Bear" },
            
            // AI/Combat/Tactical
            { "TacticalPositioningSystem.cs", "AI/Combat/Tactical" },
            { "TacticalPoint.cs", "AI/Combat/Tactical" },
            { "TacticalEnums.cs", "AI/Combat/Tactical" },
            
            // NPC/Core
            { "NPCModule.cs", "NPC/Core" },
            { "NPCConfigurationHandler.cs", "NPC/Core" },
            { "NPCHealthHandler.cs", "NPC/Core" },
            { "NPCEnums.cs", "NPC/Core" },
            { "INPCHandler.cs", "NPC/Core/Interfaces" },
            
            // NPC/Data
            { "NPCArchetype.cs", "NPC/Data" },
            
            // NPC/UI
            { "NPCNameplate.cs", "NPC/UI" },
            
            // Player/Identity
            { "PlayerInfoModule.cs", "Player/Identity" },
            { "IdentityHandler.cs", "Player/Identity" },
            { "IPlayerInfoHandler.cs", "Player/Identity/Interfaces" },
            
            // Player/UI
            { "ThirdPersonUI.cs", "Player/UI" },
            { "PlayerInfoPanel.cs", "Player/UI" },
            { "CoreStatsUi.cs", "Player/UI" },
            { "SecondaryStatsUi.cs", "Player/UI" },
            { "StatAllocationUi.cs", "Player/UI" },
            
            // Inventory/Core
            { "PlayerItemsModule.cs", "Inventory/Core" },
            { "InventoryEnums.cs", "Inventory/Core" },
            { "EquipmentSlot.cs", "Inventory/Core" },
            
            // Inventory/Grid
            { "InventoryGridController.cs", "Inventory/Grid" },
            { "InventoryGridData.cs", "Inventory/Grid" },
            { "GridArea.cs", "Inventory/Grid" },
            { "GridPosition.cs", "Inventory/Grid" },
            
            // Inventory/Items/Data
            { "ItemDefinition.cs", "Inventory/Items/Data" },
            { "ItemInstance.cs", "Inventory/Items/Data" },
            { "ItemDatabase.cs", "Inventory/Items/Data" },
            { "ItemDatabaseSetup.cs", "Inventory/Items/Data" },
            { "ContainerData.cs", "Inventory/Items/Data" },
            
            // Inventory/Items/UI
            { "ItemIconVisual.cs", "Inventory/Items/UI" },
            { "ItemOverlayVisual.cs", "Inventory/Items/UI" },
            { "ItemTooltip.cs", "Inventory/Items/UI" },
            { "ItemTooltipData.cs", "Inventory/Items/UI" },
            
            // Inventory/Interfaces
            { "IInventoryProvider.cs", "Inventory/Interfaces" },
            { "IEquipmentProvider.cs", "Inventory/Interfaces" },
            
            // Inventory/NullProviders
            { "NullInventoryProvider.cs", "Inventory/NullProviders" },
            { "NullEquipmentProvider.cs", "Inventory/NullProviders" },
            
            // Inventory/UI
            { "GridSlotBackground.cs", "Inventory/UI" },
            { "MenuManager.cs", "Inventory/UI" },
            { "TooltipManager.cs", "Inventory/UI" },
            { "ContentButtons.cs", "Inventory/UI" },
            
            // Factions
            { "FactionManager.cs", "Factions" },
            { "FactionAffiliationHandler.cs", "Factions" },
            { "PlayerFactionHandler.cs", "Factions" },
            { "FactionType.cs", "Factions" },
            { "FactionRelationshipConfig.cs", "Factions/Data" },
            
            // Models
            { "ModelModule.cs", "Models" },
            { "ModelDatabase.cs", "Models" },
            { "ModelSocketConfig.cs", "Models/Data" },
            
            // Projectiles
            { "ProjectileModule.cs", "Projectiles" },
            { "Projectile.cs", "Projectiles" },
            { "ShurikenProjectile.cs", "Projectiles" },
            { "ProjectileData.cs", "Projectiles/Data" },
            
            // Persistence
            { "SaveSystemModule.cs", "Persistence" },
            
            // Tests
            { "LevelUpTester.cs", "Tests" },
            { "ModelSwapTester.cs", "Tests" },
            { "InventoryGridDataTests.cs", "Tests" },
            { "NPCSpawner.cs", "Tests" },
        };
    }

    void CreateAllFolders()
    {
        var folders = new List<string>
        {
            // Core
            $"{ROOT_FOLDER}/Core/Brain",
            $"{ROOT_FOLDER}/Core/Brain/Interfaces",
            $"{ROOT_FOLDER}/Core/Coordinators",
            $"{ROOT_FOLDER}/Core/Coordinators/Base",
            $"{ROOT_FOLDER}/Core/Utilities",
            
            // Combat
            $"{ROOT_FOLDER}/Combat/Damage",
            $"{ROOT_FOLDER}/Combat/Damage/Data",
            $"{ROOT_FOLDER}/Combat/Damage/NPC",
            $"{ROOT_FOLDER}/Combat/Abilities",
            $"{ROOT_FOLDER}/Combat/Abilities/Interfaces",
            $"{ROOT_FOLDER}/Combat/Abilities/Data",
            $"{ROOT_FOLDER}/Combat/Abilities/NaturalWeapons",
            $"{ROOT_FOLDER}/Combat/Abilities/NullProviders",
            $"{ROOT_FOLDER}/Combat/Defense",
            $"{ROOT_FOLDER}/Combat/Defense/Interfaces",
            $"{ROOT_FOLDER}/Combat/Defense/NullProviders",
            $"{ROOT_FOLDER}/Combat/Effects",
            $"{ROOT_FOLDER}/Combat/Weapons",
            $"{ROOT_FOLDER}/Combat/Weapons/Data",
            
            // Stats
            $"{ROOT_FOLDER}/Stats/Core",
            $"{ROOT_FOLDER}/Stats/Health",
            $"{ROOT_FOLDER}/Stats/Health/Interfaces",
            $"{ROOT_FOLDER}/Stats/Health/NullProviders",
            $"{ROOT_FOLDER}/Stats/Resources",
            $"{ROOT_FOLDER}/Stats/Resources/Interfaces",
            $"{ROOT_FOLDER}/Stats/Resources/NullProviders",
            $"{ROOT_FOLDER}/Stats/CombatStats",
            $"{ROOT_FOLDER}/Stats/CombatStats/Interfaces",
            $"{ROOT_FOLDER}/Stats/CombatStats/NullProviders",
            
            // Movement
            $"{ROOT_FOLDER}/Movement/Player",
            $"{ROOT_FOLDER}/Movement/NPC",
            $"{ROOT_FOLDER}/Movement/Animation",
            $"{ROOT_FOLDER}/Movement/Animation/NullProviders",
            $"{ROOT_FOLDER}/Movement/Interfaces",
            $"{ROOT_FOLDER}/Movement/NullProviders",
            
            // Input
            $"{ROOT_FOLDER}/Input",
            $"{ROOT_FOLDER}/Input/Interfaces",
            
            // Camera
            $"{ROOT_FOLDER}/Camera",
            $"{ROOT_FOLDER}/Camera/Interfaces",
            
            // AI
            $"{ROOT_FOLDER}/AI/Core",
            $"{ROOT_FOLDER}/AI/Combat",
            $"{ROOT_FOLDER}/AI/Combat/Base",
            $"{ROOT_FOLDER}/AI/Combat/Base/Interfaces",
            $"{ROOT_FOLDER}/AI/Combat/Implementations",
            $"{ROOT_FOLDER}/AI/Combat/Implementations/Bear",
            $"{ROOT_FOLDER}/AI/Combat/Tactical",
            
            // NPC
            $"{ROOT_FOLDER}/NPC/Core",
            $"{ROOT_FOLDER}/NPC/Core/Interfaces",
            $"{ROOT_FOLDER}/NPC/Data",
            $"{ROOT_FOLDER}/NPC/UI",
            
            // Player
            $"{ROOT_FOLDER}/Player/Identity",
            $"{ROOT_FOLDER}/Player/Identity/Interfaces",
            $"{ROOT_FOLDER}/Player/UI",
            
            // Inventory
            $"{ROOT_FOLDER}/Inventory/Core",
            $"{ROOT_FOLDER}/Inventory/Grid",
            $"{ROOT_FOLDER}/Inventory/Items",
            $"{ROOT_FOLDER}/Inventory/Items/Data",
            $"{ROOT_FOLDER}/Inventory/Items/UI",
            $"{ROOT_FOLDER}/Inventory/Interfaces",
            $"{ROOT_FOLDER}/Inventory/NullProviders",
            $"{ROOT_FOLDER}/Inventory/UI",
            
            // Factions
            $"{ROOT_FOLDER}/Factions",
            $"{ROOT_FOLDER}/Factions/Data",
            
            // Models
            $"{ROOT_FOLDER}/Models",
            $"{ROOT_FOLDER}/Models/Data",
            
            // Projectiles
            $"{ROOT_FOLDER}/Projectiles",
            $"{ROOT_FOLDER}/Projectiles/Data",
            
            // Persistence
            $"{ROOT_FOLDER}/Persistence",
            
            // Tests
            $"{ROOT_FOLDER}/Tests",
        };

        int created = 0;
        foreach (string folder in folders)
        {
            if (!Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
                created++;
            }
        }

        AssetDatabase.Refresh();
        Debug.Log($"[Folder Structure Enforcer] Created {created} missing folders");

        ValidateStructure();
    }

    int CountMissingFolders()
    {
        var allFolders = new HashSet<string>();
        var mapping = GetFolderMapping();

        foreach (var folder in mapping.Values)
        {
            allFolders.Add($"{ROOT_FOLDER}/{folder}");
        }

        int missing = 0;
        foreach (string folder in allFolders)
        {
            if (!Directory.Exists(folder))
                missing++;
        }

        return missing;
    }

    void MigrateAllFiles()
    {
        int moved = 0;

        AssetDatabase.StartAssetEditing();

        try
        {
            foreach (var result in validationResults)
            {
                if (!result.IsCorrect)
                {
                    if (MoveFileInternal(result))
                        moved++;
                }
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
            AssetDatabase.Refresh();
        }

        Debug.Log($"[Folder Structure Enforcer] Migrated {moved} files");
        ValidateStructure();
    }

    void MoveFile(FileValidationResult result)
    {
        AssetDatabase.StartAssetEditing();

        try
        {
            if (MoveFileInternal(result))
            {
                Debug.Log($"[Folder Structure Enforcer] Moved {result.FileName} to correct location");
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
            AssetDatabase.Refresh();
        }

        ValidateStructure();
    }

    bool MoveFileInternal(FileValidationResult result)
    {
        // Ensure target directory exists
        string targetDir = Path.GetDirectoryName(result.ExpectedPath);
        if (!Directory.Exists(targetDir))
        {
            Directory.CreateDirectory(targetDir);
        }

        // Move the asset
        string error = AssetDatabase.MoveAsset(result.CurrentPath, result.ExpectedPath);

        if (!string.IsNullOrEmpty(error))
        {
            Debug.LogError($"[Folder Structure Enforcer] Failed to move {result.FileName}: {error}");
            return false;
        }

        return true;
    }

    private class FileValidationResult
    {
        public string FileName;
        public string CurrentPath;
        public string ExpectedPath;
        public bool IsCorrect;
    }
}
using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;

public class FolderStructureValidator : AssetPostprocessor
{
    private static bool validationEnabled = true;

    [InitializeOnLoadMethod]
    static void Initialize()
    {
        validationEnabled = EditorPrefs.GetBool("NinjaGame_FolderValidation", true);
    }

    [MenuItem("NinjaGame/Tools/Toggle Auto-Validation")]
    public static void ToggleValidation()
    {
        validationEnabled = !validationEnabled;
        EditorPrefs.SetBool("NinjaGame_FolderValidation", validationEnabled);
        Debug.Log($"[FolderStructureValidator] Auto-validation {(validationEnabled ? "ENABLED" : "DISABLED")}");
    }

    [MenuItem("NinjaGame/Tools/Set Scripts Root Folder")]
    public static void SetRootFolder()
    {
        string current = GetRootFolder();
        string selected = EditorUtility.OpenFolderPanel("Select Scripts Root Folder", current, "");
        if (string.IsNullOrEmpty(selected)) return;

        // Convert absolute path to project-relative (Assets/...)
        string projectPath = Application.dataPath.Replace("/Assets", "");
        if (selected.StartsWith(projectPath))
            selected = selected.Substring(projectPath.Length + 1);

        EditorPrefs.SetString("NinjaGame_ScriptsRoot", selected);
        Debug.Log($"[FolderStructureValidator] Scripts root set to: {selected}");
    }

    [MenuItem("NinjaGame/Tools/Print Scripts Root Folder")]
    public static void PrintRootFolder()
    {
        Debug.Log($"[FolderStructureValidator] Current scripts root: {GetRootFolder()}");
    }

    public static string GetRootFolder()
    {
        return EditorPrefs.GetString("NinjaGame_ScriptsRoot", "Assets/NinjaGame/Scripts");
    }

    static void OnPostprocessAllAssets(
        string[] importedAssets,
        string[] deletedAssets,
        string[] movedAssets,
        string[] movedFromAssetPaths)
    {
        if (!validationEnabled) return;

        foreach (string asset in importedAssets)
            ValidateAssetPlacement(asset, "created");

        foreach (string asset in movedAssets)
            ValidateAssetPlacement(asset, "moved");
    }

    static void ValidateAssetPlacement(string assetPath, string action)
    {
        if (!assetPath.EndsWith(".cs")) return;
        if (assetPath.Contains("/Editor/")) return;

        string root = GetRootFolder();
        if (!assetPath.StartsWith(root)) return;

        string fileName = Path.GetFileName(assetPath);
        string expectedFolder = GetExpectedFolder(fileName);
        if (expectedFolder == null) return;

        string expectedPath = $"{root}/{expectedFolder}/{fileName}".Replace("\\", "/");
        string normalizedActual = assetPath.Replace("\\", "/");

        if (normalizedActual != expectedPath)
        {
            Debug.LogWarning(
                $"[FolderStructureValidator] '{fileName}' was {action} to the wrong folder.\n" +
                $"  Current:  {normalizedActual}\n" +
                $"  Expected: {expectedPath}",
                AssetDatabase.LoadAssetAtPath<Object>(assetPath)
            );
        }
    }

    static string GetExpectedFolder(string fileName)
    {
        var mapping = GetFolderMapping();
        return mapping.TryGetValue(fileName, out string folder) ? folder : null;
    }

    public static Dictionary<string, string> GetFolderMapping()
    {
        return new Dictionary<string, string>
        {
            // -------------------------------------------------------------------------
            // Core Architecture
            // -------------------------------------------------------------------------
            { "ControllerBrain.cs",             "CoreArchitecture" },
            { "ManagerBrain.cs",                "CoreArchitecture" },
            { "IBrainModule.cs",                "CoreArchitecture" },
            { "IStaticModule.cs",               "CoreArchitecture" },
            { "IContextModule.cs",              "CoreArchitecture" },
            { "IGameManager.cs",                "CoreArchitecture" },
            { "IPlayerModule.cs",               "CoreArchitecture" },
            { "GameEvents.cs",                  "CoreArchitecture" },
            { "EntityConfigs.cs",               "CoreArchitecture" },
            { "EntityType.cs",                  "CoreArchitecture" },

            // -------------------------------------------------------------------------
            // Persistence & Account
            // -------------------------------------------------------------------------
            { "SaveManager.cs",                 "Persistence" },
            { "AccountManager.cs",              "Persistence" },
            { "LocalSaveProvider.cs",           "Persistence" },
            { "ISaveProvider.cs",               "Persistence" },
            { "ISaveable.cs",                   "Persistence" },

            // -------------------------------------------------------------------------
            // Menu & Character Creation
            // -------------------------------------------------------------------------
            { "LoginScreen.cs",                 "Menu" },
            { "CharacterSelectScreen.cs",       "Menu" },
            { "CharacterCreationPanel.cs",       "Menu" },
            { "CharacterCreationData.cs",        "Menu" },
            { "CharacterSlotUI.cs",              "Menu" },
            { "PlayerOriginDatabase.cs",         "Menu" },
            { "MenuManager.cs",                  "Menu" },
            { "SceneLoader.cs",                  "Menu" },

            // -------------------------------------------------------------------------
            // Stats & RPG
            // -------------------------------------------------------------------------
            { "StatEngine.cs",                  "StatsRPG" },
            { "StatSystem.cs",                  "StatsRPG" },
            { "StatSystem_Generated.cs",        "StatsRPG" },
            { "StatsManager.cs",                "StatsRPG" },
            { "StatSchema.cs",                  "StatsRPG" },
            { "StatHandle.cs",                  "StatsRPG" },
            { "StatNode.cs",                    "StatsRPG" },
            { "StatCoordinator.cs",             "StatsRPG" },
            { "StatMaskFlags.cs",               "StatsRPG" },
            { "StatEngineProfiler.cs",          "StatsRPG" },
            { "StatAllocationUi.cs",            "StatsRPG" },
            { "StatSystemEditor.cs",            "StatsRPG" },
            { "StatSystemTest.cs",              "StatsRPG" },
            { "RPGSystem.cs",                   "StatsRPG" },
            { "LevelUpTester.cs",               "StatsRPG" },
            { "CompositeValueSource.cs",        "StatsRPG" },
            { "StatValueSource.cs",             "StatsRPG" },
            { "ValueSourceDefinition.cs",       "StatsRPG" },
            { "IStatProvider.cs",               "StatsRPG" },
            { "SemanticBridgeSystem.cs",        "StatsRPG" },
            { "StateValueSource.cs",            "StatsRPG" },

            // -------------------------------------------------------------------------
            // Resources
            // -------------------------------------------------------------------------
            { "ResourceSystem.cs",              "Resources" },
            { "ResourceManager.cs",             "Resources" },
            { "ResourceDefinition.cs",          "Resources" },
            { "ResourceValueSource.cs",         "Resources" },
            { "IResourceProvider.cs",           "Resources" },
            { "IHealthProvider.cs",             "Resources" },
            { "ResourceDebugDisplay.cs",        "Resources" },

            // -------------------------------------------------------------------------
            // Identity & Faction
            // -------------------------------------------------------------------------
            { "IdentitySystem.cs",              "IdentityFaction" },
            { "IdentityHandler.cs",             "IdentityFaction" },
            { "UniversalIdentityHandler.cs",    "IdentityFaction" },
            { "IIdentityHandler.cs",            "IdentityFaction" },
            { "IIdentityLevel.cs",              "IdentityFaction" },
            { "IPlayerInfoHandler.cs",          "IdentityFaction" },
            { "PlayerInfoModule.cs",            "IdentityFaction" },
            { "FactionManager.cs",              "IdentityFaction" },
            { "FactionRelationshipConfig.cs",   "IdentityFaction" },
            { "FactionType.cs",                 "IdentityFaction" },
            { "UniversalFactionHandler.cs",     "IdentityFaction" },
            { "PlayerFactionHandler.cs",        "IdentityFaction" },
            { "NPCArchetype.cs",                "IdentityFaction" },
            { "NPCEnums.cs",                    "IdentityFaction" },

            // -------------------------------------------------------------------------
            // Item, Inventory & Equipment
            // -------------------------------------------------------------------------
            { "ItemSystem.cs",                  "ItemInventoryEquipment" },
            { "InventorySystem.cs",             "ItemInventoryEquipment" },
            { "EquipmentSystem.cs",             "ItemInventoryEquipment" },
            { "LootSystem.cs",                  "ItemInventoryEquipment" },
            { "ItemManager.cs",                 "ItemInventoryEquipment" },
            { "ItemDatabase.cs",                "ItemInventoryEquipment" },
            { "ItemDefinition.cs",              "ItemInventoryEquipment" },
            { "ItemInstance.cs",                "ItemInventoryEquipment" },
            { "ItemBaseType.cs",                "ItemInventoryEquipment" },
            { "ItemCatagory.cs",                "ItemInventoryEquipment" },
            { "ItemSubtype.cs",                 "ItemInventoryEquipment" },
            { "ItemStatModifier.cs",            "ItemInventoryEquipment" },
            { "ItemResourceModifier.cs",        "ItemInventoryEquipment" },
            { "ItemUpgradeSlot.cs",             "ItemInventoryEquipment" },
            { "UpgradeslotDefinition.cs",       "ItemInventoryEquipment" },
            { "InventoryGridData.cs",           "ItemInventoryEquipment" },
            { "InventoryGridController.cs",     "ItemInventoryEquipment" },
            { "InventoryEnums.cs",              "ItemInventoryEquipment" },
            { "EquipmentSlotDefinition.cs",     "ItemInventoryEquipment" },
            { "EquipmentSlotConfig.cs",         "ItemInventoryEquipment" },
            { "EquipmentLoadout.cs",            "ItemInventoryEquipment" },
            { "ContainerContents.cs",           "ItemInventoryEquipment" },
            { "ContainerData.cs",               "ItemInventoryEquipment" },
            { "ContainerItem.cs",               "ItemInventoryEquipment" },
            { "ChestPopulator.cs",              "ItemInventoryEquipment" },
            { "GridTransferManager.cs",         "ItemInventoryEquipment" },
            { "GridArea.cs",                    "ItemInventoryEquipment" },
            { "GridPosition.cs",                "ItemInventoryEquipment" },
            { "GridSlotBackground.cs",          "ItemInventoryEquipment" },
            { "UniversalGrid.cs",               "ItemInventoryEquipment" },
            { "UniversalInventoryGrid.cs",      "ItemInventoryEquipment" },
            { "IInventoryProvider.cs",          "ItemInventoryEquipment" },
            { "IEquipmentProvider.cs",          "ItemInventoryEquipment" },
            { "InventoryGridDataTests.cs",      "ItemInventoryEquipment" },

            // -------------------------------------------------------------------------
            // Damage & Combat
            // -------------------------------------------------------------------------
            { "DamageSystem.cs",                "DamageCombat" },
            { "DamageManager.cs",               "DamageCombat" },
            { "DamageCalculationConfig.cs",     "DamageCombat" },
            { "CombatDamagePacket.cs",          "DamageCombat" },
            { "CombatAttackData.cs",            "DamageCombat" },
            { "CombatStatMapping.cs",           "DamageCombat" },
            { "SimpleAttackHitbox.cs",          "DamageCombat" },
            { "NaturalWeaponHitbox.cs",         "DamageCombat" },
            { "NaturalWeaponAnimationEvents.cs","DamageCombat" },
            { "NaturalWeaponInitializer.cs",    "DamageCombat" },
            { "DamageEffect.cs",                "DamageCombat" },
            { "DamageOverTimeEffect.cs",        "DamageCombat" },
            { "HealEffect.cs",                  "DamageCombat" },
            { "HealOverTimeEffect.cs",          "DamageCombat" },
            { "KnockbackEffect.cs",             "DamageCombat" },
            { "MovementEffect.cs",              "DamageCombat" },
            { "EffectManagerModule.cs",         "DamageCombat" },
            { "IDamageable.cs",                 "DamageCombat" },
            { "IDefenseCapability.cs",          "DamageCombat" },
            { "IDefenseProvider.cs",            "DamageCombat" },
            { "WeaponData.cs",                  "DamageCombat" },
            { "TargetDummy.cs",                 "DamageCombat" },

            // -------------------------------------------------------------------------
            // Abilities
            // -------------------------------------------------------------------------
            { "AbilitySystem.cs",               "Abilities" },
            { "AbilityDefinition.cs",           "Abilities" },
            { "AbilityDefinition_Animation.cs", "Abilities" },
            { "AbilityDefinition_Blackboard.cs","Abilities" },
            { "AbilityDefinition_Chaining.cs",  "Abilities" },
            { "AbilityDefinition_Costs.cs",     "Abilities" },
            { "AbilityDefinition_Defense.cs",   "Abilities" },
            { "AbilityDefinition_LegacyExecute.cs", "Abilities" },
            { "AbilityDefinition_Validation.cs","Abilities" },
            { "AbilityInstance.cs",             "Abilities" },
            { "AbilityLoadoutModule.cs",        "Abilities" },
            { "AbilityLoadoutConfiguration.cs", "Abilities" },
            { "AbilitySlotData.cs",             "Abilities" },
            { "RuntimeAbilityManager.cs",       "Abilities" },
            { "GrantedAbilityData.cs",          "Abilities" },
            { "IAbilityProvider.cs",            "Abilities" },
            { "IAbilityControlSource.cs",       "Abilities" },

            // -------------------------------------------------------------------------
            // Movement & Locomotion
            // -------------------------------------------------------------------------
            { "MovementSystem.cs",              "MovementLocomotion" },
            { "LocomotionHandler.cs",           "MovementLocomotion" },
            { "ARPGLocomotionHandler.cs",       "MovementLocomotion" },
            { "ParkourFPSLocomotionHandler.cs", "MovementLocomotion" },
            { "ParkourStateController.cs",      "MovementLocomotion" },
            { "FPSControlSource.cs",            "MovementLocomotion" },
            { "MovementInput.cs",               "MovementLocomotion" },
            { "FeetDetectionModule.cs",         "MovementLocomotion" },
            { "IMovementControlSource.cs",      "MovementLocomotion" },
            { "IPhysicsModule.cs",              "MovementLocomotion" },
            { "MovementDebugDisplay.cs",        "MovementLocomotion" },

            // -------------------------------------------------------------------------
            // Animation
            // -------------------------------------------------------------------------
            { "AnimationSystem.cs",             "Animation" },
            { "AnimationEventForwarder.cs",     "Animation" },
            { "AnimationEventType.cs",          "Animation" },
            { "IAnimationProvider.cs",          "Animation" },
            { "StateMachineModule.cs",          "Animation" },
            { "StateMachineModule_IStateProvider.cs", "Animation" },
            { "Statemachine.cs",                "Animation" },
            { "StatePermissionMatrix.cs",       "Animation" },
            { "stateenums.cs",                  "Animation" },
            { "IStateProvider.cs",              "Animation" },

            // -------------------------------------------------------------------------
            // AI & GOAP
            // -------------------------------------------------------------------------
            { "AISystem.cs",                    "AIGOAP" },
            { "GOAPModule.cs",                  "AIGOAP" },
            { "GOAPContext.cs",                 "AIGOAP" },
            { "GOAPAction.cs",                  "AIGOAP" },
            { "GOAPGoal.cs",                    "AIGOAP" },
            { "FateSystem.cs",                  "AIGOAP" },
            { "TacticalCombatBehavior.cs",      "AIGOAP" },
            { "TacticalPositioningSystem.cs",   "AIGOAP" },
            { "TacticalPoint.cs",               "AIGOAP" },
            { "TacticalEnums.cs",               "AIGOAP" },
            { "AICombatBehaviorModule.cs",      "AIGOAP" },
            { "IAICombatBehavior.cs",           "AIGOAP" },
            { "PerceptionModule.cs",            "AIGOAP" },
            { "PathfindingModule.cs",           "AIGOAP" },
            { "BlackboardSystem.cs",            "AIGOAP" },
            { "Blackboard.cs",                  "AIGOAP" },
            { "BlackboardSchema.cs",            "AIGOAP" },
            { "BlackboardKey.cs",               "AIGOAP" },
            { "BlackboardCondition.cs",         "AIGOAP" },
            { "BlackboardConditionLibrary.cs",  "AIGOAP" },
            { "GoapDebugUiController.cs",       "AIGOAP" },
            { "BlackboardDebugDisplay.cs",      "AIGOAP" },

            // -------------------------------------------------------------------------
            // Interaction
            // -------------------------------------------------------------------------
            { "InteractionSystem.cs",           "Interaction" },
            { "InteractionPrompt.cs",           "Interaction" },
            { "InteractionHud.cs",              "Interaction" },
            { "InteractionIconPrompt.cs",       "Interaction" },
            { "InteractionIconDatabase.cs",     "Interaction" },
            { "InteractionAction.cs",           "Interaction" },

            // -------------------------------------------------------------------------
            // UI Windows
            // -------------------------------------------------------------------------
            { "UIWindowManager.cs",             "UIWindows" },
            { "UIWindow.cs",                    "UIWindows" },
            { "UniversalWindowManager.cs",      "UIWindows" },
            { "InventoryWindow.cs",             "UIWindows" },
            { "UniversalInventoryWindow.cs",    "UIWindows" },
            { "EquipmentWindow.cs",             "UIWindows" },
            { "StatsWindow.cs",                 "UIWindows" },
            { "ContainerWIndow.cs",             "UIWindows" },
            { "WorldspaceWindowAdapter.cs",     "UIWindows" },
            { "CoreStatsUi.cs",                 "UIWindows" },
            { "SecondaryStatsUi.cs",            "UIWindows" },
            { "TooltipManager.cs",              "UIWindows" },
            { "ItemTooltip.cs",                 "UIWindows" },
            { "ItemTooltipData.cs",             "UIWindows" },
            { "ItemIconVisual.cs",              "UIWindows" },
            { "ItemOverlayVisual.cs",           "UIWindows" },
            { "EquipmentSlotVisual.cs",         "UIWindows" },
            { "EquipmentItemIcon.cs",           "UIWindows" },
            { "ContentButtons.cs",              "UIWindows" },
            { "NPCNameplate.cs",                "UIWindows" },
            { "NameplateManager.cs",            "UIWindows" },
            { "PlayerInfoPanel.cs",             "UIWindows" },

            // -------------------------------------------------------------------------
            // Camera
            // -------------------------------------------------------------------------
            { "CameraCoordinator.cs",           "Camera" },
            { "MMOStyleCamera.cs",              "Camera" },
            { "SimpleThirdPersonCamera.cs",     "Camera" },
            { "FPSCamera.cs",                   "Camera" },
            { "ICameraProvider.cs",             "Camera" },
            { "ICameraImplementation.cs",       "Camera" },

            // -------------------------------------------------------------------------
            // Input
            // -------------------------------------------------------------------------
            { "InputSystem.cs",                 "Input" },
            { "PlayerInputControls.cs",         "Input" },
            { "IInputProvider.cs",              "Input" },
            { "IInputHandler.cs",               "Input" },

            // -------------------------------------------------------------------------
            // Model & Visual
            // -------------------------------------------------------------------------
            { "ModelModule.cs",                 "ModelVisual" },
            { "ModelDatabase.cs",               "ModelVisual" },
            { "ModelSocketConfig.cs",           "ModelVisual" },
            { "ModelSwapTester.cs",             "ModelVisual" },
            { "CharacterConfigurationHandler.cs","ModelVisual" },
            { "TargetLockModule.cs",            "ModelVisual" },
            { "BasicTargetable.cs",             "ModelVisual" },

            // -------------------------------------------------------------------------
            // NPC & Spawning
            // -------------------------------------------------------------------------
            { "NPCSpawner.cs",                  "NPCSpawning" },
            { "ChestInventoryDebugger.cs",      "NPCSpawning" },

            // -------------------------------------------------------------------------
            // Timers & Utilities
            // -------------------------------------------------------------------------
            { "Timer.cs",                       "Utilities" },
            { "CountdownTimer.cs",              "Utilities" },
            { "FrequencyTimer.cs",              "Utilities" },
            { "IntervalTimer.cs",               "Utilities" },
            { "StopwatchTimer.cs",              "Utilities" },
        };
    }
}
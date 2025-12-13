using UnityEngine;
using UnityEditor;
using System.IO;

/// <summary>
/// Asset post-processor that validates script placement when files are created or moved.
/// Warns developers when scripts are placed in incorrect folders.
/// </summary>
public class FolderStructureValidator : AssetPostprocessor
{
    private const string ROOT_FOLDER = "Assets/NinjaGame";
    private static bool validationEnabled = true;

    [MenuItem("NinjaGame/Tools/Toggle Auto-Validation")]
    public static void ToggleValidation()
    {
        validationEnabled = !validationEnabled;
        EditorPrefs.SetBool("NinjaGame_FolderValidation", validationEnabled);
        Debug.Log($"[Folder Structure Validator] Auto-validation {(validationEnabled ? "ENABLED" : "DISABLED")}");
    }

    [InitializeOnLoadMethod]
    static void Initialize()
    {
        validationEnabled = EditorPrefs.GetBool("NinjaGame_FolderValidation", true);
    }

    static void OnPostprocessAllAssets(
        string[] importedAssets,
        string[] deletedAssets,
        string[] movedAssets,
        string[] movedFromAssetPaths)
    {
        if (!validationEnabled) return;

        // Check imported (newly created) assets
        foreach (string asset in importedAssets)
        {
            ValidateAssetPlacement(asset, "created");
        }

        // Check moved assets
        foreach (string asset in movedAssets)
        {
            ValidateAssetPlacement(asset, "moved");
        }
    }

    static void ValidateAssetPlacement(string assetPath, string action)
    {
        // Only validate C# scripts
        if (!assetPath.EndsWith(".cs")) return;

        // Only validate NinjaGame scripts
        if (!assetPath.Contains("NinjaGame")) return;

        // Skip Editor scripts
        if (assetPath.Contains("/Editor/")) return;

        string fileName = Path.GetFileName(assetPath);
        string expectedPath = GetExpectedPath(fileName);

        if (expectedPath == null) return; // Not a mapped file

        string normalizedAsset = assetPath.Replace("\\", "/");
        string normalizedExpected = expectedPath.Replace("\\", "/");

        if (normalizedAsset != normalizedExpected)
        {
            Debug.LogWarning(
                $"[Folder Structure Validator] Script '{fileName}' was {action} to incorrect location!\n" +
                $"Current:  {normalizedAsset}\n" +
                $"Expected: {normalizedExpected}\n" +
                $"Use 'NinjaGame > Tools > Folder Structure Enforcer' to auto-fix.",
                AssetDatabase.LoadAssetAtPath<Object>(assetPath)
            );
        }
    }

    static string GetExpectedPath(string fileName)
    {
        var mapping = GetSharedFolderMapping();

        if (mapping.ContainsKey(fileName))
        {
            return $"{ROOT_FOLDER}/{mapping[fileName]}/{fileName}";
        }

        return null;
    }

    static System.Collections.Generic.Dictionary<string, string> GetSharedFolderMapping()
    {
        // This duplicates the mapping from FolderStructureEnforcer
        // In a production system, you'd share this via a static class or ScriptableObject
        return new System.Collections.Generic.Dictionary<string, string>
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
}
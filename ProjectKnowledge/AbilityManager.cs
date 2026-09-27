using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Global registry of every ability that exists. Definitions come from the inspector array
/// and from any Resources/AbilityDatabase folder.
///
/// Entities still hold their own AbilityDefinition references — this registry is for the
/// places that only have an id: saved hotbar slots, granted abilities, debug tools.
/// </summary>
public class AbilityManager : MonoBehaviour, IGameManager
{
    // Any folder named "Resources" anywhere under Assets is a resources root, so the
    // database folder is found by this name alone, wherever it sits in the project.
    private const string DatabaseFolder = "AbilityDatabase";

    private static AbilityManager instance;
    public static AbilityManager Instance => instance;

    [Header("Ability Definitions")]
    [Tooltip("Optional. Everything in Resources/AbilityDatabase is registered regardless.")]
    [SerializeField] private AbilityDefinition[] abilityDefinitions;

    private readonly Dictionary<string, AbilityDefinition> definitionLookup = new();

    #region IGameManager

    public string ManagerName => "Ability Manager";
    public int InitializationPriority => 7; // After Items (5), before Resources (10)
    public bool IsEnabled => enabled;
    public bool IsInitialized { get; private set; }

    public void Initialize()
    {
        if (IsInitialized) return;

        instance = this;
        LoadAbilities();
        IsInitialized = true;
    }

    public void LateInitialize() { }

    public void Shutdown() { }

    public ValidationResult Validate()
    {
        var result = ValidationResult.Success();

        if (!IsInitialized) return result;

        if (definitionLookup.Count == 0)
        {
            result.Warnings.Add($"No abilities loaded — inspector array is empty and Resources/{DatabaseFolder} found nothing");
            return result;
        }

        result.Info.Add($"{definitionLookup.Count} abilities registered");
        return result;
    }

    #endregion

    #region Loading

    private void LoadAbilities()
    {
        definitionLookup.Clear();

        int count = Register(abilityDefinitions);
        count += Register(Resources.LoadAll<AbilityDefinition>(DatabaseFolder));

        if (count == 0)
        {
            Debug.LogWarning($"[AbilityManager] No abilities loaded. Assign definitions in the inspector, " +
                             $"or drop them in a Resources/{DatabaseFolder} folder.");
        }
    }

    /// <summary>
    /// Adds definitions to the lookup, skipping anything unusable. Returns how many landed.
    /// Called once for the inspector array and once for the database folder, so an asset
    /// listed in both is silently ignored the second time rather than reported as a clash.
    /// </summary>
    private int Register(AbilityDefinition[] definitions)
    {
        if (definitions == null) return 0;

        int count = 0;

        foreach (var definition in definitions)
        {
            if (definition == null) continue;

            if (string.IsNullOrEmpty(definition.abilityId))
            {
                Debug.LogWarning($"[AbilityManager] '{definition.name}' has no abilityId, skipping.");
                continue;
            }

            if (definitionLookup.TryGetValue(definition.abilityId, out var existing))
            {
                if (existing != definition)
                    Debug.LogError($"[AbilityManager] Duplicate abilityId '{definition.abilityId}' on " +
                                   $"'{definition.name}' and '{existing.name}'. Ids must be unique.");
                continue;
            }

            definitionLookup[definition.abilityId] = definition;
            count++;
        }

        return count;
    }

    #endregion

    #region Public API

    public static AbilityDefinition GetDefinition(string abilityId)
    {
        if (instance == null || !instance.IsInitialized)
        {
            Debug.LogError("[AbilityManager] Not initialized — ManagerBrain should own this manager.");
            return null;
        }

        if (string.IsNullOrEmpty(abilityId)) return null;

        if (instance.definitionLookup.TryGetValue(abilityId, out var definition)) return definition;

        Debug.LogWarning($"[AbilityManager] Ability not found: {abilityId}");
        return null;
    }

    public static bool HasAbility(string abilityId)
    {
        if (instance == null || !instance.IsInitialized) return false;
        if (string.IsNullOrEmpty(abilityId)) return false;

        return instance.definitionLookup.ContainsKey(abilityId);
    }

    public static AbilityDefinition[] GetAllAbilities()
    {
        if (instance == null || !instance.IsInitialized) return new AbilityDefinition[0];

        var all = new AbilityDefinition[instance.definitionLookup.Count];
        instance.definitionLookup.Values.CopyTo(all, 0);
        return all;
    }

    public static string[] GetAllAbilityIds()
    {
        if (instance == null || !instance.IsInitialized) return new string[0];

        var ids = new string[instance.definitionLookup.Count];
        instance.definitionLookup.Keys.CopyTo(ids, 0);
        return ids;
    }

    public static int GetAbilityCount() => instance != null ? instance.definitionLookup.Count : 0;

    #endregion
}

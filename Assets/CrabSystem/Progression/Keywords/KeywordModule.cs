using System.Collections.Generic;
using NinjaGame.Progression;
using UnityEngine;

// What this character's keywords do (KeywordDefinition). On every hit an ability lands, each keyword it carries —
// on its asset or granted by a socket or talent (AbilitySystem.Modifiers) — pays its on-hit gains. When this
// character's hit is being resolved, the keywords on the move in flight add their accuracy, advantage and bonus
// dice if their condition holds (DamageSystem asks through IHitModifier). Talents add to a keyword through keyed
// bonuses (KeywordBonusReward).
public class KeywordModule : MonoBehaviour, IBrainModule, IHitModifier
{
    private const string KeywordFolder = "Keywords";

    public bool IsEnabled { get; set; } = true;

    private ControllerBrain brain;
    private AbilitySystem abilities;
    private ResourceSystem resources;

    private static Dictionary<string, KeywordDefinition> catalogue;
    private readonly Dictionary<string, KeywordBonus> bonuses = new();

    public void Initialize(ControllerBrain controllerBrain)
    {
        brain = controllerBrain;
    }

    public void LateInitialize()
    {
        abilities = brain.Abilities;
        resources = brain.Resources;
        if (abilities != null) abilities.OnHitLanded += HandleHitLanded;
    }

    public void UpdateModule() { }

    private void OnDestroy()
    {
        if (abilities != null) abilities.OnHitLanded -= HandleHitLanded;
    }

    public static KeywordDefinition Find(string keywordId)
    {
        if (catalogue == null)
        {
            catalogue = new Dictionary<string, KeywordDefinition>();
            foreach (KeywordDefinition keyword in Resources.LoadAll<KeywordDefinition>(KeywordFolder))
            {
                if (!string.IsNullOrEmpty(keyword.keywordId)) catalogue[keyword.keywordId] = keyword;
            }
        }

        return keywordId != null && catalogue.TryGetValue(keywordId, out KeywordDefinition found) ? found : null;
    }

    public static IEnumerable<KeywordDefinition> All
    {
        get
        {
            Find(null);
            return catalogue.Values;
        }
    }

    public void SetBonus(string key, KeywordBonus bonus) => bonuses[key] = bonus;

    public void RemoveBonus(string key) => bonuses.Remove(key);

    // ---- On hit ----

    private void HandleHitLanded(AbilityDefinition ability, ControllerBrain target)
    {
        if (!IsEnabled || ability == null || resources == null) return;

        foreach (KeywordDefinition keyword in All)
        {
            if (!abilities.Modifiers.HasTag(ability, keyword.keywordId)) continue;

            foreach (ResourceGain gain in keyword.onHit)
            {
                if (gain.resource != null && gain.amount > 0f) resources.RestoreResource(gain.resource, gain.amount);
            }
        }
    }

    // ---- Hit bonuses (IHitModifier) ----

    public void ModifyHit(ControllerBrain defender, ref float accuracy, ref bool advantage, ref float bonusDamage)
    {
        AbilityDefinition ability = abilities != null ? abilities.CurrentAbility : null;
        if (!IsEnabled || ability == null) return;

        foreach (KeywordDefinition keyword in All)
        {
            if (!abilities.Modifiers.HasTag(ability, keyword.keywordId)) continue;
            if (!ConditionHolds(keyword, defender)) continue;

            int dice = keyword.bonusDice;
            int faces = keyword.bonusDieFaces;
            accuracy += keyword.accuracy;
            advantage |= keyword.advantage;

            foreach (KeywordBonus bonus in bonuses.Values)
            {
                if (bonus.KeywordId != keyword.keywordId) continue;
                accuracy += bonus.Accuracy;
                dice += bonus.Dice;
                bonusDamage += bonus.Flat;
                if (bonus.DieFaces > faces) faces = bonus.DieFaces;
            }

            for (int i = 0; i < dice; i++) bonusDamage += Random.Range(1, Mathf.Max(1, faces) + 1);
        }
    }

    private bool ConditionHolds(KeywordDefinition keyword, ControllerBrain defender)
    {
        if (keyword.condition == KeywordCondition.Always) return true;
        if (defender == null) return false;

        Transform them = defender.EntityRoot != null ? defender.EntityRoot : defender.transform;
        Transform me = brain.EntityRoot != null ? brain.EntityRoot : brain.transform;
        Vector3 toMe = me.position - them.position;
        toMe.y = 0f;
        if (toMe.sqrMagnitude < 0.0001f) return false;

        return Vector3.Angle(them.forward, toMe) >= keyword.behindAngle;
    }
}

// A talent's addition to one keyword, held under the reward's key.
public struct KeywordBonus
{
    public string KeywordId;
    public float Accuracy;
    public int Dice;
    public int DieFaces;
    public float Flat;
}

using System;
using UnityEngine;

public class NullAbilityProvider : MonoBehaviour, IAbilityProvider
{
    public bool CanUseAbility(string abilityId) => false;
    public void UseAbility(string abilityId) { }
    public float GetAbilityCooldown(string abilityId) => 0f;
    public float GetAbilityMaxCooldown(string abilityId) => 0f;
    public bool IsAbilityOnCooldown(string abilityId) => false;

    public event Action<string> OnAbilityUsed;
    public event Action<string, float> OnAbilityCooldownChanged;
}
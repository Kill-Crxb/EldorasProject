using System;

public interface IAbilityProvider
{
    bool CanUseAbility(string abilityId);
    void UseAbility(string abilityId);
    float GetAbilityCooldown(string abilityId);
    float GetAbilityMaxCooldown(string abilityId);
    bool IsAbilityOnCooldown(string abilityId);

    event Action<string> OnAbilityUsed;
    event Action<string, float> OnAbilityCooldownChanged;
}
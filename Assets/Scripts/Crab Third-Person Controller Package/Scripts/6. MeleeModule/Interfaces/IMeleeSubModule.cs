// Interface for combat sub-modules
using UnityEngine;

public interface IMeleeSubModule
{
    bool IsEnabled { get; set; }
    void Initialize(MeleeModule parentCombat);
    void UpdateSubModule();
}
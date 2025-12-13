using System;
using UnityEngine;

public interface IDefenseProvider
{
    bool IsBlocking();
    bool IsParrying();
    bool CanDefend();

    float ProcessIncomingDamage(float damage, Vector3 attackDirection);
    float GetDefensiveMultiplier(Vector3 attackDirection);

    event Action OnBlockStart;
    event Action OnBlockEnd;
    event Action OnPerfectBlock;
}
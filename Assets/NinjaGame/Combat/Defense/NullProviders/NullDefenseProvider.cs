using System;
using UnityEngine;

public class NullDefenseProvider : MonoBehaviour, IDefenseProvider
{
    public bool IsBlocking() => false;
    public bool IsParrying() => false;
    public bool CanDefend() => false;

    public float ProcessIncomingDamage(float damage, Vector3 attackDirection) => damage;
    public float GetDefensiveMultiplier(Vector3 attackDirection) => 1f;

    public event Action OnBlockStart;
    public event Action OnBlockEnd;
    public event Action OnPerfectBlock;
}
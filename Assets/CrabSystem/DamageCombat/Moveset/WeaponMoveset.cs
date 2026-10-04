using System.Collections.Generic;
using UnityEngine;

public enum MovesetChain { Light, Heavy, Running, Air, Parry }

// A weapon's default moveset (Moveset_Build.md). Each chain is a string of follow-ups; any chain may
// be short or empty, and an empty chain makes that press do nothing. Heavy has no input of its own —
// procs and AI fire it through MovesetModule.Perform.
[CreateAssetMenu(fileName = "Moveset_", menuName = "Combat/Weapon Moveset")]
public class WeaponMoveset : ScriptableObject
{
    [Tooltip("LMB on the ground at walking pace.")]
    public List<AbilityDefinition> light = new List<AbilityDefinition>();

    [Tooltip("No input. Fired by procs and AI.")]
    public List<AbilityDefinition> heavy = new List<AbilityDefinition>();

    [Tooltip("LMB while IsRunning or IsSprinting.")]
    public List<AbilityDefinition> running = new List<AbilityDefinition>();

    [Tooltip("LMB while airborne.")]
    public List<AbilityDefinition> air = new List<AbilityDefinition>();

    [Tooltip("RMB, held. A Defensive ability; the guard lasts while RMB is down.")]
    public AbilityDefinition block;

    [Tooltip("LMB while blocking.")]
    public List<AbilityDefinition> parry = new List<AbilityDefinition>();

    [Tooltip("Seconds after a step ends in which the next press still continues the chain.")]
    public float chainGrace = 0.5f;

    public List<AbilityDefinition> Chain(MovesetChain chain) => chain switch
    {
        MovesetChain.Heavy => heavy,
        MovesetChain.Running => running,
        MovesetChain.Air => air,
        MovesetChain.Parry => parry,
        _ => light
    };
}

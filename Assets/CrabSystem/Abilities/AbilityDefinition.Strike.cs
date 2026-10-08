using System;
using System.Collections.Generic;
using UnityEngine;

// Strikes (Combat_Framework.md §2.5, Strike_Build.md). One entry per Strike(n) event in the move's clip:
// the shape of that hit check. The weapon supplies the reach; an entry adds to it and says how wide, how
// high and whether walls block. A move with no entries strikes once with the default shape.
public partial class AbilityDefinition
{
    [Header("Strikes")]
    [Tooltip("One entry per Strike(n) event in the clip, in order: Strike(0) uses the first. A move with none " +
             "strikes once with the default shape. The last entry's strike ends the active phase.")]
    public List<StrikeShape> strikes = new List<StrikeShape>();

    static readonly StrikeShape DefaultStrike = new StrikeShape();

    public int StrikeCount => strikes != null && strikes.Count > 0 ? strikes.Count : 1;

    // Past the end reads the last entry, so an extra Strike event in a clip still checks a sane shape.
    public StrikeShape StrikeAt(int index)
    {
        if (strikes == null || strikes.Count == 0) return DefaultStrike;
        return strikes[Mathf.Clamp(index, 0, strikes.Count - 1)];
    }
}

[Serializable]
public class StrikeShape
{
    [Tooltip("Metres added to the weapon's reach for this strike. Negative shortens it.")]
    public float reachBonus = 0f;

    [Tooltip("Width of the cone in degrees, centred on the attacker's facing. 360 hits all the way round.")]
    [Range(0f, 360f)] public float arc = 90f;

    [Tooltip("Bottom of the band the strike covers, in metres above the attacker's feet. A target whose body " +
             "overlaps the band is in.")]
    public float heightMin = 0f;

    [Tooltip("Top of the band, in metres above the attacker's feet.")]
    public float heightMax = 2f;

    [Tooltip("Terrain and static props between the attacker's chest and the target block the hit.")]
    public bool lineOfSight = true;
}

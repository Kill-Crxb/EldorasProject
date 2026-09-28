using System;
using System.Collections.Generic;
using UnityEngine;

// Move block — Combat_Framework.md §2, Move_Block_Build.md.
// Frames are 60 fps and counted from the ability's start. An ability with no startup frames
// has no move data and behaves exactly as before.
public partial class AbilityDefinition
{
    [Header("Move — frames at 60 fps")]
    public SpeedClass speedClass = SpeedClass.None;
    public MoveFrames frames;
    public HitProperties hit;
    public List<CancelRoute> routes = new List<CancelRoute>();

    public bool HasMoveData => frames.startup > 0;
    public int ActiveStart => frames.startup;
    public int RecoveryStart => frames.startup + frames.active;
    public int TotalFrames => frames.startup + frames.active + frames.recovery;

    public MovePhase PhaseAt(int frame)
    {
        if (!HasMoveData || frame < 0) return MovePhase.None;
        if (frame < ActiveStart) return MovePhase.Startup;
        if (frame < RecoveryStart) return MovePhase.Active;
        if (frame < TotalFrames) return MovePhase.Recovery;
        return MovePhase.None;
    }

    public bool InCancelWindow(int frame) => InRange(frame, frames.cancelFrom, frames.cancelTo);
    public bool HasIFramesAt(int frame) => InRange(frame, frames.iframeFrom, frames.iframeTo);
    public bool HasArmorAt(int frame) => InRange(frame, frames.armorFrom, frames.armorTo);

    static bool InRange(int frame, int from, int to) => to > from && frame >= from && frame < to;

    [ContextMenu("Move/Fill from speed class")]
    void FillFromSpeedClass()
    {
        if (speedClass == SpeedClass.None) return;
        frames.startup = ClassStartup(speedClass);
        frames.active = ClassActive(speedClass);
        frames.recovery = ClassRecovery(speedClass);
        hit.blockAdvantage = ClassOnBlock(speedClass);
        hit.hitStop = ClassHitStop(speedClass);
        hit.blockStamina = ClassBlockStamina(speedClass);
    }

    // Class defaults — Move_Block_Build.md "Speed classes". Technique startup is 12 + 8 per seal
    // and is composed at cast time, so only its base is here.
    public static int ClassStartup(SpeedClass c) => c switch
    {
        SpeedClass.Quick => 10,
        SpeedClass.Standard => 16,
        SpeedClass.Heavy => 26,
        SpeedClass.Technique => 12,
        _ => 0
    };

    public static int ClassActive(SpeedClass c) => c switch
    {
        SpeedClass.Quick => 3,
        SpeedClass.Standard => 4,
        SpeedClass.Heavy => 5,
        _ => 0
    };

    public static int ClassRecovery(SpeedClass c) => c switch
    {
        SpeedClass.Quick => 15,
        SpeedClass.Standard => 22,
        SpeedClass.Heavy => 34,
        SpeedClass.Technique => 30,
        _ => 0
    };

    public static int ClassOnBlock(SpeedClass c) => c switch
    {
        SpeedClass.Quick => -2,
        SpeedClass.Standard => -5,
        SpeedClass.Heavy => -12,
        _ => 0
    };

    public static int ClassHitStop(SpeedClass c) => c switch
    {
        SpeedClass.Quick => 4,
        SpeedClass.Standard => 6,
        SpeedClass.Heavy => 10,
        SpeedClass.Technique => 8,
        _ => 0
    };

    public static int ClassBlockStamina(SpeedClass c) => c switch
    {
        SpeedClass.Quick => 4,
        SpeedClass.Standard => 5,
        SpeedClass.Heavy => 7,
        _ => 0
    };
}

public enum SpeedClass { None, Quick, Standard, Heavy, Technique }
public enum MovePhase { None, Startup, Active, Recovery }
public enum GuardType { Strike, Perilous, Grab }
public enum HitState { None, Flinch, Stagger, Knockdown, Launch, GuardBreak }
public enum Tracking { None, Partial, Full }
public enum FeelTier { None, Light, Medium, Heavy }
public enum MoveCategory { None, Normal, Technique, Dodge }
public enum CancelCondition { OnHit, OnHitOrBlock, Always }

[Flags]
public enum HitBypass { None = 0, Pierce = 1, Exposed = 2, Grab = 4 }

[Serializable]
public struct MoveFrames
{
    public int startup;
    public int active;
    public int recovery;

    [Tooltip("Frames from ability start. 0/0 = none.")]
    public int cancelFrom;
    public int cancelTo;
    public int iframeFrom;
    public int iframeTo;
    public int armorFrom;
    public int armorTo;
}

[Serializable]
public struct HitProperties
{
    public GuardType guard;
    public HitState onHit;
    [Tooltip("Base hit-state frames. Scaled by applied ÷ average damage, clamped 0.5–1.5 (CF2).")]
    public int onHitFrames;
    [Tooltip("Defender's stamina cost on block. On parry the attacker pays it instead.")]
    public int blockStamina;
    [Tooltip("Attacker's frame advantage when blocked. Usually negative.")]
    public int blockAdvantage;
    public Tracking tracking;
    public int hitStop;
    public FeelTier feel;
    public HitBypass bypass;
}

[Serializable]
public struct CancelRoute
{
    [Tooltip("A specific move. Leave empty to use the category.")]
    public AbilityDefinition into;
    public MoveCategory intoCategory;
    public CancelCondition when;
    public int costStamina;
    public int costMana;
}

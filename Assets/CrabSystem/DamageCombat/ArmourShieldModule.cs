using System;
using UnityEngine;

// The armour shield (Combat_Framework §6.1): a Halo-style shield for PHYSICAL damage only. Its segments
// are the armour dice (atr.arm_dice d4s) with the flat armour (atr.arm) under them. DamageSystem hands
// it the physical part of an unguarded hit; what it can't hold goes on to health. While it holds, a
// physical hit costs no health. It refills after a delay without taking damage, and trickles back one
// segment at a time even under pressure (Combat_Tuning_Chart.md: without it, armour does nothing against
// non-stop swings).
public class ArmourShieldModule : MonoBehaviour, IBrainModule
{
    [SerializeField] private bool isEnabled = true;

    [Tooltip("Size of each armour die. d4 today (Stat_Resolution §4).")]
    [SerializeField] private int dieFaces = 4;

    [Tooltip("Seconds per segment the shield regains even while being hit. 0 turns the trickle off.")]
    [SerializeField] private float trickleSeconds = 2f;

    [SerializeField] private SoakBar shield = new SoakBar();

    private const string ArmourDiceStat = "atr.arm_dice";
    private const string ArmourStat = "atr.arm";

    private ControllerBrain brain;

    public bool IsEnabled { get => isEnabled; set => isEnabled = value; }
    public IBarSource Bar => shield;

    // Amount absorbed, and whether that emptied the shield. For feedback (VFX shopping list: absorb / break).
    public event Action<float, bool> OnShieldHit;

    // Until the entity clock lands (CrabSystem_Standard §9).
    private static float Now => Time.time;

    public void Initialize(ControllerBrain controllerBrain)
    {
        brain = controllerBrain;
        Refresh();
    }

    public void UpdateModule()
    {
        Refresh();
        shield.Tick(Now);
        shield.Trickle(Now, trickleSeconds);
    }

    // Returns what the shield couldn't hold.
    public float Absorb(float physicalDamage)
    {
        if (!isEnabled) return physicalDamage;

        float overflow = shield.Absorb(physicalDamage, Now);
        float absorbed = physicalDamage - overflow;
        if (absorbed > 0f) OnShieldHit?.Invoke(absorbed, shield.IsEmpty);
        return overflow;
    }

    // Equipping armour or a Stoneskin changes the dice; the bar rebuilds full when they do.
    private void Refresh()
    {
        if (brain == null || brain.Stats == null) return;

        float flat = brain.Stats.GetValue(ArmourStat);
        int dice = Mathf.FloorToInt(brain.Stats.GetValue(ArmourDiceStat));
        shield.Configure(flat, dice, dieFaces, 0);
    }
}

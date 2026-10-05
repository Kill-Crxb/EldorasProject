using System;

// One flat stat an item gives while it is equipped — armour pieces set atr.arm_dice, atr.arm, atr.arm_def
// and equip.load (Combat_Framework §6.1, §6.4). Flat only: no percentages.
[Serializable]
public class ItemStatBonus
{
    [IdRef(IdKind.Stat)] public string statId;
    public float amount;
}

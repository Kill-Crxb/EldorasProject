// Every stat id code names, outside the dials (DialIds). Assets pick ids with [IdRef(IdKind.Stat)]; code names them
// only here, and Tools → CrabSystem → Validate Ids checks each constant against the stat schemas. Renaming a stat:
// change the id in its schema, put the old id in the entry's formerIds, then update the constant.

namespace CrabSystem
{
    public static class StatIds
    {
        public const string Level = "character.level";
        public const string MaxHealth = "character.max_health";

        public const string Mind = "core.mind";
        public const string Body = "core.body";
        public const string Spirit = "core.spirit";
        public const string Resilience = "core.resilience";
        public const string Endurance = "core.endurance";
        public const string Insight = "core.insight";

        public static readonly string[] Cores = { Mind, Body, Spirit, Resilience, Endurance, Insight };

        // Hit resolution and soak (Stat_Resolution §4)
        public const string Finesse = "cmb.finesse";
        public const string Avoidance = "def.avoidance";
        public const string Deflection = "def.deflection";
        public const string Armour = "atr.arm";
        public const string ArmourDice = "atr.arm_dice";
        public const string ArmourDefense = "atr.arm_def";
        public const string BonusDamage = "cmb.bonus_damage";
        public const string BonusDice = "cmb.bonus_dice";

        // Saving throws: the bearer's defence against the source's matching stat (Stat_Resolution §6)
        public const string Toughness = "def.toughness";
        public const string Alteration = "def.alteration";
        public const string Dexterity = "def.dexterity";
        public const string Preparation = "def.preparation";
        public const string Efficacy = "cmb.efficacy";
        public const string Arcana = "cmb.arcana";
        public const string Initiative = "cmb.initiative";
        public const string Cunning = "cmb.cunning";
    }
}

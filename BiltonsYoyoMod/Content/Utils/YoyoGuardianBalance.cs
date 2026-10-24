namespace BiltonsYoyoMod.Content.Utils
{
    // Fix #9: these used to be ModConfig sliders (BaseProjectileDamage, GuardianYoyoDamage,
    // WormContactDamage, etc.). They're now fixed constants; the only remaining knob is the
    // Standard/Hell difficulty toggle in YoyoGuardianConfig, which multiplies these at runtime
    // (see YoyoGuardian.Dmg() and the various NPC.damage assignments across the mod).
    public static class YoyoGuardianBalance
    {
        public const int BaseProjectileDamage = 75;
        public const int GuardianYoyoDamage = 70;
        public const int WormContactDamage = 50;
        public const int StarGuardianContactDamage = 75;
        public const int DesperationDashDamage = 50;
        public const float UnderworldDamageMultiplier = 2.0f;

        // Fix #7/#9: fixed base HP per vanilla difficulty. Multiplied at runtime by both the
        // player-count "balance" factor Terraria passes into ApplyDifficultyAndPlayerScaling
        // (so HP scales up automatically with more players, same as vanilla bosses) and the
        // Standard/Hell multiplier from the config.
        public const int ClassicHPBase = 200000;
        public const int ExpertHPBase = 260000;
        public const int MasterHPBase = 300000;

        public const int Phase3ClassicHPBase = 650000;
        public const int Phase3ExpertHPBase = 780000;
        public const int Phase3MasterHPBase = 950000;

        // "Takes 1000% more damage" = 1x (its normal damage taken) + 1000% = 11x.
        public const float Phase3DamageTakenMultiplier = 11f;
        public const int Phase3B2ExplosionDamage = 50;
    }
}
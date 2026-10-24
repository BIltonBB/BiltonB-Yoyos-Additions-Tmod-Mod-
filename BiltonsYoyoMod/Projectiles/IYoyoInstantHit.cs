namespace BiltonsYoyoMod.Projectiles
{
    // Marker: yoyo-spawned child projectiles designed for INSTANT point-blank
    // contact (the yoyo parks on the enemy, minions should shred on arrival).
    // YoyoHitSpawnGraceGlobalProjectile skips these (no 1-second harmless
    // window); per-type local NPC immunity already prevents shredding.
    public interface IYoyoInstantHit
    {
    }
}

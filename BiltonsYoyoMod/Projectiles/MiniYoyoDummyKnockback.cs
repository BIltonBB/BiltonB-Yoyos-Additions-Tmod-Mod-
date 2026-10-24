using Terraria;
using Terraria.ModLoader;

namespace BiltonsYoyoMod.Projectiles
{
    // Dummies (immortal test targets) must not drift: several yoyo attacks carry
    // heavy knockback, and a displaced dummy hitbox desyncs from its drawn sprite,
    // making every follow-up shot appear to "miss". Zero knockback vs immortals.
    public class MiniYoyoDummyKnockback : GlobalProjectile
    {
        public override bool InstancePerEntity => true;

        public override void ModifyHitNPC(Projectile projectile, NPC target, ref NPC.HitModifiers modifiers)
        {
            if (DummySafety.IsTrainingDummy(target))
                modifiers.Knockback *= 0f;
        }
    }
}

using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using BiltonsYoyoMod.Content.NPCs.Bosses;
using BiltonsYoyoMod.Content.NPCs.Mobs;
using BiltonsYoyoMod.Content.Projectiles;
using BiltonsYoyoMod.Content.Utils;

namespace BiltonsYoyoMod.Content
{
    // Damage the Guardian's own hostile projectiles deal to players.
    public class GlobalDamageReduction : GlobalProjectile
    {
        public override bool InstancePerEntity => true;

        public override void ModifyHitPlayer(Projectile projectile, Player target, ref Player.HurtModifiers modifiers)
        {
            bool isGuardianProjectile =
                projectile.type == ModContent.ProjectileType<CounterweightYG>() ||
                projectile.type == ModContent.ProjectileType<PointedYG>() ||
                projectile.type == ModContent.ProjectileType<LooseStoneBearingsYG>() ||
                projectile.type == ModContent.ProjectileType<StoneGuardianYoyo>();

            if (!isGuardianProjectile) return;

            modifiers.FinalDamage *= 0.03f;
        }
    }

    // Reduces contact damage dealt by Guardian-related NPCs.
    public class GlobalNPCDamageReduction : GlobalNPC
    {
        public override bool InstancePerEntity => true;

        public override void ModifyHitPlayer(NPC npc, Player target, ref Player.HurtModifiers modifiers)
        {
            if (npc.type == ModContent.NPCType<StarGuardianNPC>())
            {
                modifiers.FinalDamage *= 0.1f;
            }
        }
    }

    // Guardian and its related enemies take bonus damage from yoyos.
    public class GlobalYoyoBonusDamage : GlobalNPC
    {
        public override void ModifyHitByProjectile(NPC npc, Projectile projectile, ref NPC.HitModifiers modifiers)
        {
            if (npc.ModNPC is YoyoGuardian guardian)
            {
                // Guardian takes 50% damage while its summoned worm is alive.
                if (guardian.IsRealBoss && guardian.WormActiveDamageHalved)
                    modifiers.SourceDamage *= 0.5f;

                // Phase 3: 1000% MORE damage taken = 11x total damage.
                if (guardian.Phase == YoyoGuardianPhase.Phase3)
                    modifiers.SourceDamage *= YoyoGuardianBalance.Phase3DamageTakenMultiplier;
            }

            if (projectile.aiStyle != ProjAIStyleID.Yoyo) return;
            if (!IsGuardianAlignedTarget(npc)) return;

            modifiers.SourceDamage *= 2f;
        }

        private bool IsGuardianAlignedTarget(NPC npc)
        {
            if (npc.type == ModContent.NPCType<YoyoGuardian>()) return true;
            if (npc.type == ModContent.NPCType<GuardianOrbitShield>()) return true;
            if (npc.type == ModContent.NPCType<StarGuardianNPC>()) return true;

            var wormTag = npc.GetGlobalNPC<YoyoGuardianWormTag>();
            return wormTag.isGuardianWorm;
        }
    }
}
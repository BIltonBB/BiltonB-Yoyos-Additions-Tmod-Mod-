using Terraria;
using Terraria.ModLoader;
using Terraria.ID;

namespace BiltonsYoyoMod.Players
{
    public class YoyoAdrenalineAPlayer : ModPlayer
    {
        // SETTINGS

        // 100 yoyo hits required.
        public const int RequiredHits = 100;

        // 5 seconds = 300 ticks.
        public const int EmpoweredDuration = 300;

        public const float EmpoweredMultiplier = 1.5f;


        // STATE

        public bool enabled;

        // Compatibility field.
        public float charge;

        // Actual number of yoyo hits.
        public int hitCount;

        // Remaining empowered time.
        public int empoweredTimer;

        // Compatibility field.
        public bool empoweredHit;


        // STATUS

        public bool Empowered
        {
            get
            {
                return empoweredTimer > 0;
            }
        }


        public float FillPercentage
        {
            get
            {
                if (Empowered)
                {
                    return
                        empoweredTimer /
                        (float)EmpoweredDuration;
                }

                return
                    hitCount /
                    (float)RequiredHits;
            }
        }


        // RESET

        public override void ResetEffects()
        {
            enabled = false;
        }


        // TIMER

        public override void PostUpdate()
        {
            if (!enabled)
                return;


            if (empoweredTimer > 0)
            {
                empoweredTimer--;


                if (empoweredTimer <= 0)
                {
                    empoweredTimer = 0;
                    hitCount = 0;
                    charge = 0f;
                    empoweredHit = false;
                }


                return;
            }


            charge =
                hitCount /
                (float)RequiredHits;


            if (charge > 1f)
            {
                charge = 1f;
            }
        }


        // ADD CHARGE

        public void AddCharge(float amount)
        {
            if (!enabled)
                return;


            if (Empowered)
                return;


            int hitsToAdd =
                (int)amount;


            if (hitsToAdd <= 0)
                return;


            hitCount += hitsToAdd;


            if (hitCount >= RequiredHits)
            {
                hitCount = RequiredHits;
                charge = 1f;
                empoweredHit = true;

                empoweredTimer =
                    EmpoweredDuration;

                return;
            }


            charge =
                hitCount /
                (float)RequiredHits;
        }


        // DAMAGE

        public override void ModifyHitNPCWithProj(
            Projectile proj,
            NPC target,
            ref NPC.HitModifiers modifiers)
        {
            if (!enabled)
                return;


            if (!Empowered)
                return;


            if (!IsYoyoProjectile(proj))
                return;


            modifiers.FinalDamage *=
                EmpoweredMultiplier;
        }


        // YOYO HIT

        public override void OnHitNPCWithProj(
            Projectile proj,
            NPC target,
            NPC.HitInfo hit,
            int damageDone)
        {
            if (!enabled)
                return;


            if (Empowered)
                return;


            if (!IsYoyoProjectile(proj))
                return;


            AddCharge(1f);
        }


        // YOYO CHECK

        private bool IsYoyoProjectile(
            Projectile projectile)
        {
            return
                projectile.aiStyle ==
                ProjAIStyleID.Yoyo;
        }
    }
}
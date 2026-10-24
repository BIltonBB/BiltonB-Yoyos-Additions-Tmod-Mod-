using System;
using System.Collections.Generic;
using System.IO;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria.GameContent;

namespace BiltonsYoyoMod.Projectiles
{
    public class DutchmanYoyoProj : ModProjectile
    {
        private const int MaxHP = 200;

        private int hp = -1; // -1 = uninitialized
        // Armor-shard staging (visual only, derived).
        private int lastThird = 3;
        private Vector2 lastHitDir = Vector2.UnitY; // distress smoke lean
        private int fireTimer = 120;
        private int iframes;
        private bool forcedReturn;
        private int volleySide = 1; // alternating broadside battery: +1 starboard / -1 port
        private int queueLeft; // staggered second pair of the volley
        private int queueTimer;
        private Vector2 queueVelA;
        private Vector2 queueVelB;
        private bool queueDesperate;
        private int forcedBurn; // burning-hulk ticks during the forced return
        private readonly List<Vector2> wake = new List<Vector2>();

        public override void SetStaticDefaults()
        {
            // Afterimage ghosts for the hull (ExampleMod Basic Projectile pattern).
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 6;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 0;
        }

        public override void SetDefaults()
        {
            Projectile.width = 16;
            Projectile.height = 16;

            Projectile.aiStyle = ProjAIStyleID.Yoyo;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.penetrate = -1;

            Projectile.DamageType = DamageClass.MeleeNoSpeed;
            Projectile.scale = 1f;

            ProjectileID.Sets.YoyosLifeTimeMultiplier[Projectile.type] = 14f;
            ProjectileID.Sets.YoyosMaximumRange[Projectile.type] = 260f;
            ProjectileID.Sets.YoyosTopSpeed[Projectile.type] = 11f;
        }

        // M1: warm-sea lift so the hull reads in dark water/caves without fullbrighting.
        // Fullbright lift killed: sprite now respects cave lighting like surroundings.
        public override Color? GetAlpha(Color lightColor)
        {
            return null;
        }

        public override void SendExtraAI(BinaryWriter writer)
        {
            writer.Write(hp);
            writer.Write(forcedReturn);
            writer.Write(fireTimer);
            writer.Write(volleySide);
            writer.Write(queueLeft);
            writer.Write(queueTimer);
            writer.WriteVector2(queueVelA);
            writer.WriteVector2(queueVelB);
            writer.Write(queueDesperate);
            writer.Write(forcedBurn);
        }

        public override void ReceiveExtraAI(BinaryReader reader)
        {
            hp = reader.ReadInt32();
            forcedReturn = reader.ReadBoolean();
            fireTimer = reader.ReadInt32();
            volleySide = reader.ReadInt32();
            queueLeft = reader.ReadInt32();
            queueTimer = reader.ReadInt32();
            queueVelA = reader.ReadVector2();
            queueVelB = reader.ReadVector2();
            queueDesperate = reader.ReadBoolean();
            forcedBurn = reader.ReadInt32();
        }

        public override void AI()
        {
            if (hp < 0)
                hp = MaxHP;

            if (iframes > 0)
                iframes--;

            Player owner = Main.player[Projectile.owner];

            MiniYoyoFx.RibbonPush(wake, Projectile.Center, 10f, 12);
            MiniYoyoFx.ApplyMotionReadability(Projectile);

            // Sail mist: rare wide breath under the hull while underway.
            if (Main.rand.NextBool(26))
            {
                Dust mist = Dust.NewDustPerfect(Projectile.Center + new Vector2(Main.rand.NextFloat(-10f, 10f), 8f), DustID.Smoke, new Vector2(Main.rand.NextFloat(-0.5f, 0.5f), 0.2f), 140, default, 1.1f);
                mist.noGravity = true;
            }

            // Hull wake: one thin sea-mist wisp every 3rd tick along the path.
            // Was: smoke + spark every tick (~120 dust/s ambient cloud).
            // NOTE: yoyo timeLeft is pinned at 2 by the yoyo AI, so gate on GameUpdateCount.
            if (Main.GameUpdateCount % 3 == 0 && Projectile.velocity.LengthSquared() > 4f)
            {
                Dust wisp = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(5f, 5f), DustID.Smoke, -Projectile.velocity * 0.06f + new Vector2(0f, -0.5f), 120, default, 0.7f);
                wisp.noGravity = true;
            }
            if (Main.rand.NextBool(10))
            {
                Dust ember = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(7f, 7f), DustID.SolarFlare, new Vector2(0f, -0.6f), 120, default, 0.7f);
                ember.noGravity = true;
            }

            // M5: fast-move bow spray snapped behind the hull when she runs.
            if (Projectile.velocity.LengthSquared() > 36f && Main.rand.NextBool(10))
            {
                Vector2 back = Projectile.velocity.SafeNormalize(Vector2.UnitY);
                Dust spray = Dust.NewDustPerfect(Projectile.Center - back * 7f, DustID.SolarFlare, -back * 1.4f + Main.rand.NextVector2Circular(0.4f, 0.4f), 100, default, 0.8f);
                spray.noGravity = true;
            }

            // M3: brass-mist string shimmer along the player->hull line.
            if (Main.rand.NextBool(14) && Projectile.owner >= 0 && Projectile.owner < Main.maxPlayers)
            {
                Player lineOwner = Main.player[Projectile.owner];
                if (lineOwner != null && lineOwner.active)
                {
                    Vector2 pos = Vector2.Lerp(lineOwner.Center, Projectile.Center, Main.rand.NextFloat(0.1f, 0.95f));
                    Dust shimmer = Dust.NewDustPerfect(pos, DustID.Smoke, new Vector2(0f, -0.4f), 140, default, 0.6f);
                    shimmer.noGravity = true;
                }
            }

            // M4: breathing hull light (was a flat value).
            float breath = 1f + (float)Math.Sin(Main.GameUpdateCount * 0.2f) * 0.06f;
            Lighting.AddLight(Projectile.Center, 0.55f * breath, 0.5f * breath, 0.42f * breath);

            // Battle-damage state below half hull: BLACK distress smoke + rare spark.
            // Smoke leans off the last hit bearing (stored hull-side).
            if (hp >= 0 && hp < MaxHP / 2 && !forcedReturn)
            {
                if (Main.rand.NextBool(5))
                {
                    Dust damage = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(7f, 7f), DustID.Smoke, new Vector2(0f, -1.2f) + lastHitDir * 0.6f, 120, new Color(35, 35, 35), 1f);
                    damage.noGravity = true;
                }
                if (Main.rand.NextBool(16))
                {
                    Dust sparkshower = Dust.NewDustPerfect(Projectile.Center, DustID.SolarFlare, Main.rand.NextVector2Circular(2.5f, 2.5f), 100, default, 0.9f);
                    sparkshower.noGravity = true;
                }
            }

            // M10: desperate-mode warning glint throbs while the owner bleeds out below half.
            if (!forcedReturn && owner.statLife < owner.statLifeMax2 / 2)
            {
                float alarm = 0.5f + 0.5f * (float)Math.Sin(Main.GameUpdateCount * 0.3f);
                Lighting.AddLight(Projectile.Center, 0.9f * alarm, 0.3f * alarm, 0.25f * alarm);
            }

            if (forcedReturn)
            {
                // Forced return at 1.5x normal yoyo speed, straight home with a streak wake.
                float returnSpeed = ProjectileID.Sets.YoyosTopSpeed[Projectile.type] * 1.5f;
                Vector2 dir = (owner.Center - Projectile.Center).SafeNormalize(Vector2.Zero);
                Projectile.velocity = dir * returnSpeed;
                Projectile.rotation += 0.5f;

            MiniYoyoFx.ApplyMotionReadability(Projectile);
                // NOTE: yoyo timeLeft is pinned at 2 by the yoyo AI, so gate on GameUpdateCount.
                if (Main.GameUpdateCount % 2 == 0)
                {
                    Dust streak = Dust.NewDustPerfect(Projectile.Center, DustID.Smoke, -dir * 2f + Main.rand.NextVector2Circular(0.5f, 0.5f), 120, default, 0.7f);
                    streak.noGravity = true;
                }

                // MAJOR C (hulk half): alternating port/starboard wake puffs as she limps home.
                if (Main.GameUpdateCount % 4 < 2)
                {
                    Vector2 sidePuff = Projectile.Center + new Vector2(6f, 2f);
                    Dust puff = Dust.NewDustPerfect(sidePuff, DustID.Smoke, new Vector2(1f, 0.5f), 120, default, 0.9f);
                    puff.noGravity = true;
                }
                else
                {
                    Vector2 sidePuff = Projectile.Center + new Vector2(-6f, 2f);
                    Dust puff = Dust.NewDustPerfect(sidePuff, DustID.Smoke, new Vector2(-1f, 0.5f), 120, default, 0.9f);
                    puff.noGravity = true;
                }

                // (Bow light already rides the burn staging below; drawing lives in PreDraw.)

                // Burning-hulk staging: the first 90 ticks home bleed fire and
                // smoke, then she runs clean. Same speed throughout (visual only).
                if (forcedBurn > 0)
                {
                    forcedBurn--;
                    if (Main.GameUpdateCount % 3 == 0)
                    {
                        Dust burn = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(8f, 8f), DustID.SolarFlare, new Vector2(0f, -1.6f), 100, default, 1f);
                        burn.noGravity = true;
                    }
                    if (Main.rand.NextBool(4))
                    {
                        Dust soot = Dust.NewDustPerfect(Projectile.Center, DustID.Smoke, new Vector2(0f, -1f), 120, default, 1.1f);
                        soot.noGravity = true;
                    }
                    Lighting.AddLight(Projectile.Center, 1f * forcedBurn / 90f + 0.3f, 0.5f, 0.3f);
                }

                return;
            }

            // Visual spin on the normal path (forced path spins above).
            Projectile.rotation += 0.12f;

            // Hostile projectiles damage the yoyo (owner-side, 15-tick iframes)
            if (Main.myPlayer == Projectile.owner && iframes <= 0)
            {
                for (int i = 0; i < Main.maxProjectiles; i++)
                {
                    Projectile p = Main.projectile[i];
                    if (p.active && p.hostile && !p.friendly && p.damage > 0)
                    {
                        if (Vector2.Distance(p.Center, Projectile.Center) <= 20f)
                        {
                            hp -= 1;
                            iframes = 15;
                            Projectile.netUpdate = true;

                            for (int d = 0; d < 3; d++)
                            {
                                Dust dust = Dust.NewDustPerfect(Projectile.Center, DustID.Smoke, Main.rand.NextVector2Circular(1.8f, 1.8f), 100, default, 0.85f);
                                dust.noGravity = true;
                            }
                            Dust nick = Dust.NewDustPerfect(Projectile.Center, DustID.SolarFlare, Main.rand.NextVector2Circular(1.5f, 1.5f), 100, default, 0.9f);
                            nick.noGravity = true;
                            // M7: hot nick streak flung along the incoming bearing.
                            Vector2 inDir = (Projectile.Center - p.Center).SafeNormalize(Vector2.UnitY);
                            lastHitDir = inDir;
                            Dust streak = Dust.NewDustPerfect(Projectile.Center, DustID.SolarFlare, inDir * 3f, 100, default, 1f);
                            streak.noGravity = true;
                            // Parry flash: the eaten shot bursts white off the hull.
                            YoyoParticles.SpawnRing(Projectile.Center, 8f, 30f, 16, Color.White, 2f);
                            MiniYoyoFx.FlashFX(Projectile.Center, Color.White, 0.7f);

                            break;
                        }
                    }
                }
            }

            // Destroyed: explosion + burning-hulk forced return
            if (hp <= 0)
            {
                hp = 0;
                forcedReturn = true;
                fireTimer = 0;
                forcedBurn = 90;
                queueLeft = 0;
                Projectile.netUpdate = true;

                for (int i = 0; i < 12; i++)
                {
                    float angle = MathHelper.TwoPi * i / 12f;
                    int dustType = i % 2 == 0 ? DustID.Smoke : DustID.SolarFlare;
                    Dust dust = Dust.NewDustPerfect(Projectile.Center, dustType, angle.ToRotationVector2() * 3.2f, 100, default, 1.1f);
                    dust.noGravity = true;
                }

                // Single crisp shockwave ring (was 16 dust second ring).
                for (int i = 0; i < 10; i++)
                {
                    float angle = MathHelper.TwoPi * i / 10f;
                    Dust ring = Dust.NewDustPerfect(Projectile.Center, DustID.SolarFlare, angle.ToRotationVector2() * 5f, 100, default, 1.1f);
                    ring.noGravity = true;
                }

                // Hull-break smoke column rising off the wreck.
                for (int i = 0; i < 4; i++)
                {
                    Dust column = Dust.NewDustPerfect(Projectile.Center + new Vector2(Main.rand.NextFloat(-6f, 6f), -4f - i * 4f), DustID.Smoke, new Vector2(0f, -1.5f), 120, default, 1.2f);
                    column.noGravity = true;
                }

                Lighting.AddLight(Projectile.Center, 1f, 0.8f, 0.6f);
                MiniYoyoFx.DissolveFX(Projectile.Center, new Color(255, 190, 130));

                if (Main.myPlayer == Projectile.owner)
                {
                    // Visual-only destruction ring.
                    Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, Vector2.Zero,
                        ModContent.ProjectileType<DutchmanBlastRingProj>(), 0, 0f, Projectile.owner);

                    // Remove live cannons so they don't linger
                    for (int i = 0; i < Main.maxProjectiles; i++)
                    {
                        Projectile p = Main.projectile[i];
                        if (p.active && p.owner == Projectile.owner && p.type == ModContent.ProjectileType<DutchManYoyoCannonProj>())
                        {
                            p.Kill();
                        }
                    }
                }

                // H3: desperate-mode crack speaks in the Dutchman's own voice.
                MiniBossYoyoSound.Volley(Projectile.owner, 2, Projectile.Center, 0.55f, -0.4f);
                return;
            }

            // Cannon volley timer (random 120-180 ticks)
            fireTimer--;
            // E1: the broadside announces itself - rope tauts before the fire.
            // (Owner-side: fireTimer is not synced, so the tell stays honest.)
            if (fireTimer == 12 && Main.myPlayer == Projectile.owner)
                YoyoStringHookSystem.AnnounceVolley(Projectile, 12);
            if (fireTimer <= 0)
            {
                fireTimer = Main.rand.Next(120, 181);
                if (Main.myPlayer == Projectile.owner)
                {
                    FireVolley(owner);
                }
            }

            // Staggered second pair of the broadside, 6 ticks after the first.
            if (queueLeft > 0)
            {
                queueTimer--;
                if (queueTimer <= 0)
                {
                    queueLeft = 0;
                    if (Main.myPlayer == Projectile.owner)
                        SpawnPair(queueVelA, queueVelB, queueDesperate);
                    Projectile.netUpdate = true;
                }
            }
        }

        private void FireVolley(Player owner)
        {
            bool desperate = owner.statLife < owner.statLifeMax2 / 2;
            volleySide = -volleySide; // alternate broadside battery each volley

            Vector2[] vels = new Vector2[4];
            if (desperate)
            {
                // Target random valid enemies within 700px, speed 12
                List<NPC> valid = new List<NPC>();
                for (int i = 0; i < Main.maxNPCs; i++)
                {
                    NPC npc = Main.npc[i];
                    if (MiniYoyoFx.IsAttackable(npc) && Vector2.Distance(Projectile.Center, npc.Center) <= 700f)
                    {
                        valid.Add(npc);
                    }
                }

                for (int i = 0; i < 4; i++)
                {
                    if (valid.Count > 0)
                    {
                        NPC chosen = valid[Main.rand.Next(valid.Count)];
                        vels[i] = (chosen.Center - Projectile.Center).SafeNormalize(Vector2.UnitY) * 12f;
                    }
                    else
                    {
                        vels[i] = Vector2.UnitY.RotatedBy(Main.rand.NextFloat(-0.4f, 0.4f)) * -12f;
                    }
                }
            }
            else
            {
                // 4 shots in an upward fan: 45-degree spacing, mirrored per battery.
                float baseAngle = Main.rand.NextFloat(-0.2f, 0.2f) * volleySide;
                for (int i = 0; i < 4; i++)
                {
                    float angle = -MathHelper.PiOver2 + baseAngle + (i - 1.5f) * MathHelper.PiOver4;
                    vels[i] = angle.ToRotationVector2() * 9f;
                }
            }

            // First pair now, second pair staggered 6 ticks later (rolling broadside).
            SpawnPair(vels[0], vels[1], desperate);
            // Parrot heckle: the ship itself shouts the broadside (rare).
            if (Main.rand.NextBool(20))
                CombatText.NewText(Projectile.getRect(), new Color(255, 200, 130), Main.rand.NextBool() ? "FIRE!" : "AGAIN!");
            // Rival parrot (config, default OFF): occasionally shouts it wrong.
            bool lies = false;
            try
            {
                var lcfg = ModContent.GetInstance<Configuration.YoyoConfig>();
                lies = lcfg != null && lcfg.Feedback.ParrotLies;
            }
            catch
            {
            }
            if (lies && Main.rand.NextBool(100))
            {
                string[] wrong = { "FI...FISH?", "AQUAMAN!", "STARBOARD??", "FIRE... nevermind" };
                CombatText.NewText(Projectile.getRect(), new Color(180, 220, 180), wrong[Main.rand.Next(wrong.Length)]);
            }
            queueVelA = vels[2];
            queueVelB = vels[3];
            queueDesperate = desperate;
            queueLeft = 2;
            queueTimer = 6;
            Projectile.netUpdate = true;
        }

        private void SpawnPair(Vector2 velA, Vector2 velB, bool desperate)
        {
            int cannonType = ModContent.ProjectileType<DutchManYoyoCannonProj>();
            int damage = Projectile.damage / 2;
            foreach (Vector2 vel in new Vector2[] { velA, velB })
            {
                int idx = Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, vel, cannonType, damage, 2f, Projectile.owner);
                if (idx >= 0 && idx < Main.maxProjectiles)
                {
                    // Desperate shot: hotter fuse, read in the cannon's PreDraw.
                    Main.projectile[idx].ai[0] = desperate ? 1f : 0f;
                    // Battery side rides ai[1]: port red / starboard green tracers.
                    Main.projectile[idx].ai[1] = volleySide;
                    Main.projectile[idx].netUpdate = true;
                }
            }

            // Muzzle jets kick off the firing battery's side + rolling deck smoke.
            // Broadside recoil read: opposite shove-jets + a soft hull kick shake.
            MiniYoyoFx.FlashFX(Projectile.Center + new Vector2(volleySide * 10f, 0f), new Color(255, 190, 110));
            MiniYoyoFx.Shake(1, 6);
            // Broadside smoke-ring puffs off the firing battery (config-gated in
            // the helper, read live): alpha-blended smoke so it occludes.
            MiniYoyoFx.SmokeRingPuffs(Projectile.Center + new Vector2(volleySide * 10f, 0f),
                new Vector2(volleySide * 0.9f, -0.35f), 1f);
            for (int i = 0; i < 3; i++)
            {
                Dust jet = Dust.NewDustPerfect(Projectile.Center + new Vector2(volleySide * 10f, 0f), DustID.Smoke, new Vector2(volleySide * Main.rand.NextFloat(1f, 2.5f), Main.rand.NextFloat(-0.5f, 0.5f)), 120, default, 1f);
                jet.noGravity = true;
            }
            for (int i = 0; i < 2; i++)
            {
                Dust spark = Dust.NewDustPerfect(Projectile.Center + new Vector2(volleySide * 8f, 0f), DustID.SolarFlare, new Vector2(volleySide * 2f, Main.rand.NextFloat(-1f, 1f)), 100, default, 0.9f);
                spark.noGravity = true;
            }
            // Recoil shove off the far rail (answers the muzzle side).
            for (int i = 0; i < 2; i++)
            {
                Dust shove = Dust.NewDustPerfect(Projectile.Center + new Vector2(-volleySide * 8f, 0f), DustID.Smoke, new Vector2(-volleySide * Main.rand.NextFloat(1.5f, 3f), Main.rand.NextFloat(-0.5f, 0.5f)), 120, default, 0.9f);
                shove.noGravity = true;
            }
            // M6: one lingering hot coal off the firing battery.
            Dust coal = Dust.NewDustPerfect(Projectile.Center + new Vector2(volleySide * 10f, -2f), DustID.SolarFlare, new Vector2(volleySide * 0.5f, -0.8f), 100, default, 1.2f);
            coal.noGravity = true;
            for (int i = 0; i < 2; i++)
            {
                Dust broadside = Dust.NewDustPerfect(Projectile.Center + new Vector2(Main.rand.NextFloat(-8f, 8f), 4f), DustID.Smoke, new Vector2(Main.rand.NextFloat(-1f, 1f), 1.2f), 120, default, 1f);
                broadside.noGravity = true;
            }
        }

        public override bool PreDraw(ref Color lightColor)
        {
            // Layer 1: sea-mist wake following the hull's real path.
            MiniYoyoFx.RibbonDrawSoft(wake, Projectile.type);
            DrawAfterimages(lightColor);

            // Layer 2: hull sheen staged by integrity (warm -> amber -> blood).
            float hullFrac = hp >= 0 ? MathHelper.Clamp(hp / (float)MaxHP, 0f, 1f) : 1f;
            Color hullTint = hullFrac > 0.66f ? new Color(255, 180, 110) : hullFrac > 0.33f ? new Color(255, 150, 80) : new Color(255, 110, 70);
            MiniYoyoFx.DrawGlow(Projectile.Center, 16f, hullTint, 0.15f);
            // Armor-shard pop each time integrity drops a third (once per third).
            int third = hp >= 0 ? (hp * 3) / MaxHP : 3;
            if (third < lastThird)
            {
                lastThird = third;
                for (int i = 0; i < 6; i++)
                {
                    float angle = MathHelper.TwoPi * i / 6f;
                    Dust shard = Dust.NewDustPerfect(Projectile.Center, DustID.SolarFlare, angle.ToRotationVector2() * 3.4f, 100, default, 1.1f);
                    shard.noGravity = true;
                }
                MiniYoyoFx.FlashFX(Projectile.Center, new Color(255, 170, 100), 0.8f);
                Terraria.Audio.SoundEngine.PlaySound(Terraria.ID.SoundID.Item27, Projectile.Center);
            }
            else if (third > lastThird)
            {
                lastThird = third;
            }

            // M9: waterline sheen removed (it read as an offset orb, not a shadow).

            // Battle-damage read: dull red bleed under the hull below half (with HP bar).
            if (hp >= 0 && hp < MaxHP / 2 && !forcedReturn)
            {
                float throb = 0.28f + 0.1f * (float)Math.Sin(Main.GameUpdateCount * 0.15f);
                MiniYoyoFx.DrawGlow(Projectile.Center, 26f, new Color(220, 60, 50), throb);
            }

            // MAJOR A: gunport broadside overhaul — cannon-barrel ticks extend along the
            // exact fan bearings as the volley loads, the firing battery's half burning
            // brighter; desperate volleys kindle blood-orange instead of gold.
            // Rolling-broadside snap ring rides the synced stagger window (queueLeft).
            // Deck reload lamps: port/starboard coals brightening as the volley loads.
            if (!forcedReturn && hp > 0 && fireTimer <= 40)
            {
                float reload = 1f - fireTimer / 40f;
                MiniYoyoFx.DrawGlow(Projectile.Center + new Vector2(-10f, -2f), 8f + 4f * reload, new Color(255, 190, 110), 0.25f + 0.3f * reload);
                MiniYoyoFx.DrawGlow(Projectile.Center + new Vector2(10f, -2f), 8f + 4f * reload, new Color(255, 190, 110), 0.25f + 0.3f * reload);
            }
            if (!forcedReturn && hp > 0 && fireTimer <= 20)
            {
                Player owner = Main.player[Projectile.owner];
                bool desperateNext = owner.active && !owner.dead && owner.statLife < owner.statLifeMax2 / 2;
                Color coal = desperateNext ? new Color(255, 120, 70) : new Color(255, 190, 110);
                float kindle = 1f - fireTimer / 20f;
                for (int i = 0; i < 4; i++)
                {
                    float angle = -MathHelper.PiOver2 + (i - 1.5f) * MathHelper.PiOver4;
                    Vector2 dir = angle.ToRotationVector2();
                    float sideBoost = Math.Sign(dir.X) == volleySide ? 1f : 0.45f;
                    Vector2 breech = Projectile.Center + dir * 8f - Main.screenPosition;
                    Vector2 muzzle = Projectile.Center + dir * (14f + 6f * kindle) - Main.screenPosition;
                    MiniYoyoFx.DrawLine(breech, muzzle, 3f, coal * (0.35f + 0.5f * kindle) * sideBoost);
                    MiniYoyoFx.DrawGlow(Projectile.Center + dir * 10f, 8f + 5f * kindle, coal, (0.3f + 0.45f * kindle) * sideBoost);
                }
            }
            if (!forcedReturn && queueLeft > 0)
            {
                float t = 1f - queueTimer / 6f;
                MiniYoyoFx.DrawRing(Projectile.Center, 16f + 14f * t, 2f, new Color(255, 200, 130) * (0.6f * (1f - t) + 0.15f), 24);
            }

            // Vanilla-style enemy health bar above the yoyo when damaged.
            // Blinks its red bed below quarter hull so the breach reads coming.
            if (hp >= 0 && hp < MaxHP)
            {
                Texture2D pixel = TextureAssets.MagicPixel.Value;
                Vector2 barPos = Projectile.Center - Main.screenPosition + new Vector2(-20f, -28f);
                int barWidth = 40;
                int barHeight = 5;
                float fill = (float)hp / MaxHP;
                float bedAlpha = hp < MaxHP / 4 ? 0.6f + 0.3f * (float)Math.Sin(Main.GameUpdateCount * 0.3f) : 0.9f;

                Main.spriteBatch.Draw(pixel, new Rectangle((int)barPos.X, (int)barPos.Y, barWidth, barHeight), Color.Red * bedAlpha);
                Main.spriteBatch.Draw(pixel, new Rectangle((int)barPos.X, (int)barPos.Y, (int)(barWidth * fill), barHeight), Color.LimeGreen * 0.9f);
            }

            return true;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            MiniYoyoFx.ImpactFX(target.Center, new Color(255, 190, 130), MiniYoyoFx.HitScale(damageDone), Projectile.owner, (target.Center - Projectile.Center).ToRotation());
            // Kills confirm centrally via YoyoKillFX (covers sub-projectiles too).
        }

        public override void PostDraw(Color lightColor)
        {
            // No blob glow: PreDraw seat + lighting + _Glow file already carry
            // identity. PostDraw keeps only motion/condition reads.
            // Aura budget (C1): fury ring cut - belongs to Ogre/Betsy/DarkMage now.
        }

        private void DrawAfterimages(Color lightColor)
        {
            // Same verified pattern as DarkMage/TorchGod: TrailCacheLength ghosts, oldest->newest.
            if (Projectile.oldPos == null || Projectile.oldPos.Length < 2)
                return;
            Texture2D tex = TextureAssets.Projectile[Projectile.type].Value;
            Vector2 halfHitbox = new Vector2(Projectile.width, Projectile.height) * 0.5f;
            Vector2 origin = tex.Size() * 0.5f;
            int ghosts = Math.Min(4, Projectile.oldPos.Length - 1);
            for (int k = ghosts; k >= 1; k--)
            {
                Vector2 oldCenter = Projectile.oldPos[k] + halfHitbox;
                if (oldCenter.LengthSquared() < 1f)
                    continue;
                float age = 1f - k / (float)(ghosts + 1);
                Color color = (GetAlpha(lightColor) ?? lightColor) * (0.24f * age);
                float rot = (Projectile.oldRot != null && k < Projectile.oldRot.Length) ? Projectile.oldRot[k] : Projectile.rotation;
                Main.EntitySpriteDraw(tex, oldCenter - Main.screenPosition + new Vector2(0f, Projectile.gfxOffY),
                    null, color, rot, origin, Projectile.scale, SpriteEffects.None, 0);
            }
        }
    }
}

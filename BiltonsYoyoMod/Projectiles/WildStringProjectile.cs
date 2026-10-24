using Terraria;
using Terraria.ModLoader;
using Terraria.ID;
using Terraria.DataStructures;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.IO;

namespace BiltonsYoyoMod.Projectiles
{
    public class WildStringProjectile : ModProjectile
    {
        public enum StringVariant { Cotton, PolyCotton, Polyester }

        private struct Link
        {
            public int A;
            public int B;
            public int TimeLeft;
        }

        private StringVariant Variant => (StringVariant)(int)Projectile.ai[0];
        private readonly List<Link> links = new List<Link>();
        private int LastTarget = -1;
        private int Hits;
        private float OrbitAngle;
        private float OrbitSpeed;
        private float OrbitDistance;

        public override string Texture => "Terraria/Images/MagicPixel";

        public override void SetStaticDefaults()
        {
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 10;
        }

        public override void SetDefaults()
        {
            Projectile.width = 8;
            Projectile.height = 8;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 30 * 60;
            Projectile.hide = true;
        }

        public override void OnSpawn(IEntitySource source)
        {
            OrbitAngle = Main.rand.NextFloat(MathHelper.TwoPi);
            OrbitSpeed = Variant switch
            {
                StringVariant.Cotton => Main.rand.NextFloat(1f, 2f),
                StringVariant.PolyCotton => Main.rand.NextFloat(2f, 3f),
                StringVariant.Polyester => Main.rand.NextFloat(3f, 5f),
                _ => 2f
            };
            OrbitDistance = Variant switch
            {
                StringVariant.Cotton => Main.rand.NextFloat(55f, 95f),
                StringVariant.PolyCotton => Main.rand.NextFloat(220f, 290f),
                StringVariant.Polyester => Main.rand.NextFloat(140f, 210f),
                _ => 100f
            };
            Projectile.timeLeft = Variant == StringVariant.PolyCotton
                ? 25 * 60
                : Main.rand.Next(15, 36) * 60;
            LastTarget = -1;
            Hits = 0;
            links.Clear();
        }

        public override bool PreAI()
        {
            Player owner = Main.player[Projectile.owner];
            if (!owner.active || owner.dead)
            {
                Projectile.Kill();
                return false;
            }

            OrbitAngle += MathHelper.ToRadians(OrbitSpeed * 4f * WildStringBase.EffectiveSpinMult());
            Projectile.Center = owner.Center + OrbitAngle.ToRotationVector2() * OrbitDistance;

            for (int i = links.Count - 1; i >= 0; i--)
            {
                Link link = links[i];
                link.TimeLeft--;
                if (!TryGetNPC(link.A, out NPC a) || !TryGetNPC(link.B, out NPC b) || link.TimeLeft <= 0)
                {
                    links.RemoveAt(i);
                    continue;
                }

                Vector2 delta = b.Center - a.Center;
                float length = delta.Length();
                float maxLength = Variant == StringVariant.PolyCotton ? 170f : 140f;
                if (length > maxLength && length > 0.01f)
                {
                    Vector2 pull = delta / length * 0.35f;
                    // A training dummy is damageable but immovable: pulling it
                    // would slide its hitbox away from the fixed sprite. Pull
                    // only the mobile partner if the other end is a dummy.
                    bool pinA = DummySafety.Pin(a);
                    bool pinB = DummySafety.Pin(b);
                    if (!pinA)
                        a.velocity += pull;
                    if (!pinB)
                        b.velocity -= pull;
                    // Sync at 20-tick cadence: per-tick netUpdate on two
                    // NPCs is pure multiplayer spam for a smooth pull.
                    if (Main.GameUpdateCount % 20 == 0)
                    {
                        if (!pinA)
                            a.netUpdate = true;
                        if (!pinB)
                            b.netUpdate = true;
                    }
                }
            }

            return false;
        }

        private static bool TryGetNPC(int index, out NPC npc)
        {
            npc = null;
            if (index < 0 || index >= Main.maxNPCs)
                return false;
            npc = Main.npc[index];
            return npc.active && !npc.friendly && !npc.dontTakeDamage;
        }

        public override bool? CanHitNPC(NPC target)
        {
            if (!target.CanBeChasedBy())
                return false;
            if (Variant == StringVariant.PolyCotton && target.boss)
                return false;
            return base.CanHitNPC(target);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            Hits++;

            if (Variant == StringVariant.Cotton)
            {
                target.AddBuff(BuffID.Slow, 180);
                Projectile.Kill();
                return;
            }

            if (Variant == StringVariant.PolyCotton)
            {
                if (LastTarget >= 0 && LastTarget != target.whoAmI)
                {
                    links.Add(new Link { A = LastTarget, B = target.whoAmI, TimeLeft = 600 });
                    Projectile.netUpdate = true;
                }
                LastTarget = target.whoAmI;
                return;
            }

            if (Variant == StringVariant.Polyester)
            {
                target.GetGlobalNPC<WildStringDefenseGlobalNPC>().AddReduction(5);
                if (Hits >= 5)
                    Projectile.Kill();
            }
        }

        public override void SendExtraAI(BinaryWriter writer)
        {
            writer.Write(LastTarget);
            writer.Write(links.Count);
            foreach (Link link in links)
            {
                writer.Write(link.A);
                writer.Write(link.B);
                writer.Write(link.TimeLeft);
            }
        }

        public override void ReceiveExtraAI(BinaryReader reader)
        {
            LastTarget = reader.ReadInt32();
            links.Clear();
            int count = Math.Clamp(reader.ReadInt32(), 0, 32);
            for (int i = 0; i < count; i++)
            {
                links.Add(new Link
                {
                    A = reader.ReadInt32(),
                    B = reader.ReadInt32(),
                    TimeLeft = reader.ReadInt32()
                });
            }
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Player owner = Main.player[Projectile.owner];
            DrawLine(owner.Center, Projectile.Center, Color.White);

            foreach (Link link in links)
            {
                if (TryGetNPC(link.A, out NPC a) && TryGetNPC(link.B, out NPC b))
                    DrawLine(a.Center, b.Center, Color.White);
            }

            return false;
        }

        private static void DrawLine(Vector2 start, Vector2 end, Color color)
        {
            Vector2 delta = end - start;
            float length = delta.Length();
            if (length <= 1f)
                return;

            Texture2D tex = Terraria.GameContent.TextureAssets.MagicPixel.Value;
            Main.EntitySpriteDraw(tex, start, new Rectangle(0, 0, 1, 1), color,
                delta.ToRotation(), new Vector2(0f, 0.5f), new Vector2(length, 1f),
                SpriteEffects.None, 0f);
        }
    }

    public class WildStringDefenseGlobalNPC : GlobalNPC
    {
        public override bool InstancePerEntity => true;

        private int reduction;
        private int baseDefense;

        public override void SetDefaults(NPC npc)
        {
            baseDefense = npc.defense;
            reduction = 0;
        }

        public override bool PreAI(NPC npc)
        {
            if (reduction <= 0)
                return true; // Untouched NPCs keep vanilla stats, zero writes.
            npc.defense = Math.Max(0, baseDefense - reduction);
            return true;
        }

        public void AddReduction(int amount)
        {
            reduction += amount;
        }
    }
}

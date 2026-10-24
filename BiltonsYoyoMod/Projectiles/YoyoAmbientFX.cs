using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace BiltonsYoyoMod.Projectiles
{
    // Location-reactive ambient FX for the 13 mini-boss yoyos (mains only):
    // water skip spray, lava wake under Obsidian Skin, shimmer-dip iridescence,
    // and depth-echo sonar for specials. All visual, pooled/dust, MP-safe.
    public class YoyoAmbientFX : GlobalProjectile
    {
        public override bool InstancePerEntity => true;

        private bool wasWet;
        private int skipCooldown;
        private int shimmerTimer;
        private int weightCooldown;

        public override bool AppliesToEntity(Projectile entity, bool lateInstantiation)
        {
            return entity.aiStyle == ProjAIStyleID.Yoyo
                && YoyoStringHookSystem.TryGetStringIndex(entity.type, out _);
        }

        public override void AI(Projectile projectile)
        {
            int tx = (int)(projectile.Center.X / 16f);
            int ty = (int)(projectile.Center.Y / 16f);
            bool inLiquid = false;
            int liquidType = 0;
            if (tx >= 0 && tx < Main.maxTilesX && ty >= 0 && ty < Main.maxTilesY)
            {
                Tile tile = Main.tile[tx, ty];
                if (tile != null && tile.LiquidAmount > 0)
                {
                    inLiquid = true;
                    liquidType = tile.LiquidType;
                }
            }

            // Water skip: fast surface crossing kicks spray + ring, no physics change.
            if (skipCooldown > 0)
                skipCooldown--;
            if (!wasWet && inLiquid && liquidType == LiquidID.Water
                && projectile.velocity.LengthSquared() > 64f && skipCooldown <= 0)
            {
                skipCooldown = 30;
                for (int i = 0; i < 5; i++)
                {
                    Dust spray = Dust.NewDustPerfect(projectile.Center, DustID.Rain, new Vector2(Main.rand.NextFloat(-3f, 3f), Main.rand.NextFloat(-4f, -1f)), 100, default, 1f);
                    spray.noGravity = false;
                }
                YoyoParticles.SpawnRing(projectile.Center, 8f, 26f, 18, new Color(170, 220, 255), 2f);
                Lighting.AddLight(projectile.Center, 0.3f, 0.5f, 0.7f);
            }
            wasWet = inLiquid;

            // Lava wake: Obsidian Skin turns the yoyo into a firebrand.
            if (inLiquid && liquidType == LiquidID.Lava && projectile.owner >= 0 && projectile.owner < Main.maxPlayers)
            {
                Player owner = Main.player[projectile.owner];
                if (owner != null && owner.active && owner.HasBuff(BuffID.ObsidianSkin))
                {
                    Lighting.AddLight(projectile.Center, 1.2f, 0.5f, 0.1f);
                    if (Main.GameUpdateCount % 3 == 0)
                    {
                        Dust flame = Dust.NewDustPerfect(projectile.Center + Main.rand.NextVector2Circular(6f, 6f), DustID.Torch, new Vector2(0f, -2f), 100, default, 1f);
                        flame.noGravity = true;
                    }
                }
            }

            // Yoyo Bag bonus weight: one family-tinted orbiter while deployed.
            if (weightCooldown > 0)
                weightCooldown--;
            else
            {
                weightCooldown = 30;
                if (Main.myPlayer == projectile.owner && projectile.owner >= 0 && projectile.owner < Main.maxPlayers)
                {
                    Player o = Main.player[projectile.owner];
                    int bonusType = ModContent.ProjectileType<YoyoBonusWeightProj>();
                    // Yoyo Bag equipped (accessory scan: no yoyoBag field exists).
                    bool hasBag = false;
                    for (int a = 3; a < 10 && !hasBag; a++)
                    {
                        Item acc = o.armor[a];
                        if (acc != null && acc.type == Terraria.ID.ItemID.YoyoBag)
                            hasBag = true;
                    }
                    if (o != null && o.active && hasBag && o.ownedProjectileCounts[bonusType] == 0
                        && YoyoThrowMarquee.YoyoMarqueeNames(projectile.type, out _, out Color family))
                    {
                        int packed = (family.R << 16) | (family.G << 8) | family.B;
                        int idx = Projectile.NewProjectile(projectile.GetSource_FromThis(), projectile.Center, Vector2.Zero,
                            bonusType, System.Math.Max(1, projectile.damage / 4), 2f, projectile.owner, packed, 0f);
                        if (idx >= 0 && idx < Main.maxProjectiles)
                            Main.projectile[idx].netUpdate = true;
                    }
                }
            }

            // Shimmer dip: 10s of iridescence after touching shimmer.
            if (inLiquid && liquidType == LiquidID.Shimmer)
                shimmerTimer = 600;
            if (shimmerTimer > 0)
            {
                shimmerTimer--;
                Color disco = new Color(Main.DiscoR, Main.DiscoG, Main.DiscoB);
                Lighting.AddLight(projectile.Center, disco.R / 255f * 0.5f, disco.G / 255f * 0.5f, disco.B / 255f * 0.5f);
                if (Main.GameUpdateCount % 4 == 0)
                {
                    Dust shimmer = Dust.NewDustPerfect(projectile.Center + Main.rand.NextVector2Circular(8f, 8f), DustID.RainbowMk2, new Vector2(0f, -1f), 100, default, 0.9f);
                    shimmer.noGravity = true;
                }
            }

            // Depth echo (specials): slow deep yoyos ping sonar off the dark.
            if (YoyoRoster.IsSpecial(projectile.type) && projectile.Center.Y > Main.rockLayer * 16f
                && projectile.velocity.LengthSquared() < 9f
                && (Main.GameUpdateCount + projectile.whoAmI * 37) % 90 == 0)
            {
                YoyoParticles.SpawnRing(projectile.Center, 10f, 60f, 30, new Color(120, 140, 200), 1.5f);
                Lighting.AddLight(projectile.Center, 0.3f, 0.35f, 0.5f);
            }
        }
    }
}

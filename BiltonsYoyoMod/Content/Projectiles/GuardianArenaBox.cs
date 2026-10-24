using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.GameContent;
using Terraria.Graphics.Effects;
using BiltonsYoyoMod.Content.NPCs.Bosses;
using BiltonsYoyoMod.Content.Utils;

namespace BiltonsYoyoMod.Content.Projectiles
{
    public class GuardianArenaBox : ModProjectile
    {
        private const int BoxSize = 3000;
        private const int HalfBox = BoxSize / 2;
        private const int BorderSize = 8;
        private const int LightStep = 16;

        // Static fight state (shared across box instances and sides):
        // open-air light points keyed by arena center (rebuilt only when the
        // home moves), plus the last stable home so a silently killed box
        // restores at the same spot instead of chasing the boss.
        private static System.Collections.Generic.HashSet<int> openAirTiles;
        private static bool lightCacheValid;
        private static Vector2 lightCacheCenter;
        private static Vector2? lastHome;
        internal static uint LastBoxSeenTick;

        private static int guardianType = -1;

        // SPEC: stationary arena. Spawned ONCE centered on the boss
        // (see EnsureExists), never moved after. Only players are confined;
        // the boss roams free. PurpleBorder is backed by ai[1] so the P3
        // purple flips on MP clients via normal ai[] sync (plus ExtraAI).
        public bool PurpleBorder
        {
            get => Projectile.ai[1] == 1f;
            set => Projectile.ai[1] = value ? 1f : 0f;
        }

        public override string Texture => "Terraria/Images/MagicPixel";

        public override void SetStaticDefaults()
        {
            ProjectileID.Sets.DontAttachHideToAlpha[Type] = true;
        }

        public override void SetDefaults()
        {
            // The projectile itself stays at the original spawn position.
            // The large hitbox prevents the renderer from culling it
            // when the player reaches the arena corners.
            Projectile.width = BoxSize;
            Projectile.height = BoxSize;

            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 2;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
			Projectile.aiStyle = -1;

			// hide MUST stay TRUE: per the DrawBehind docs AND the official
			// ExampleBehindTilesProjectile, behindNPCsAndTiles is drawn BEFORE
			// tiles and NPCs only in conjunction with hide=true. hide=false
			// ALSO draws this projectile in the normal front pass (which is
			// what painted black over tiles/NPCs/boss in older builds).
			// With hide=true, PreDraw (fill) + PostDraw (border) below run
			// ONLY in the behind-tiles pass: behind tiles, NPCs, dummies,
			// platforms and campfires, in front of the vanilla background.
			Projectile.hide = true;
			Projectile.netImportant = true;
        }

        public override void AI()
        {
            Projectile.timeLeft = 2;
            // Stationary lock: never follow the boss. Zero any drift every tick.
            Projectile.velocity = Vector2.Zero;
            LastBoxSeenTick = Main.GameUpdateCount;

            // Local visuals run on every side (including MP clients, whose
            // copy never reaches the server-only logic below).
            Rectangle arena = GetArenaRectangle();
            AddArenaLight(arena);

            if (Main.netMode == NetmodeID.MultiplayerClient)
                return;

            // Find the real Guardian live instead of trusting ai[0] alone:
            // if the spawn parameter ever arrives wrong, the box killed
            // itself every tick (invisible arena, free exit, zero warnings
            // because every respawn looked healthy). ai[0] is still written
            // and used by EnsureExists for attach/adopt; this is the backup.
            NPC guardian = FindRealGuardian();
            if (guardian == null)
            {
                ForgetHome();
                Projectile.Kill();
                return;
            }

            // Stationary by design: spawned once centered on the Guardian
            // (see EnsureExists) and NEVER moved after. Home is recorded on
            // the first server tick and re-asserted below, so nothing — not
            // boss movement, slot churn, or net corrections — can drag the
            // box along. Only players are confined; the boss roams free.
            if (Projectile.ai[2] == 0f)
            {
                Projectile.localAI[0] = Projectile.Center.X;
                Projectile.localAI[1] = Projectile.Center.Y;
                Projectile.ai[2] = 1f;
                Projectile.netUpdate = true;
                lastHome = Projectile.Center;
            }
            else
            {
                Vector2 home = new Vector2(Projectile.localAI[0], Projectile.localAI[1]);
                if (Projectile.Center != home)
                {
                    Projectile.Center = home;
                    Projectile.velocity = Vector2.Zero;
                    Projectile.netUpdate = true;
                }
            }
            for (int i = 0; i < Main.maxPlayers; i++)
            {
                Player player = Main.player[i];

                if (!player.active || player.dead)
                    continue;

                KeepPlayerInside(player, arena);
            }
        }

		// ROOT-CAUSE FIX for the follow-bug (kill/respawn churn every tick):
		// the old code required the live box's ai[0] binding to match the
		// calling guardian, and SILENTLY CLEARED + RESPAWNED whenever it
		// didn't. Any binding miss (stale fight, slot churn) became a
		// per-tick kill/spawn loop at the boss's feet — which reads exactly
		// like "the arena follows the boss" — and every fresh instance
		// restarted the light cache, so lighting never built and tiles
		// stayed pitch black ("covering everything"). New rules: a live box
		// is NEVER killed here — it is adopted and rebound. Spawning happens
		// only when zero live boxes exist. Churn is structurally impossible.
		public static int EnsureExists(NPC guardian, ref int cachedIndex)
		{
			int boxType = ModContent.ProjectileType<GuardianArenaBox>();

			// 1. Cached slot: accept any live box of our type, rebind it.
			if (cachedIndex >= 0 && cachedIndex < Main.maxProjectiles)
			{
				Projectile existing = Main.projectile[cachedIndex];

				if (existing.active && existing.type == boxType)
				{
					Rebind(existing, guardian);
					return cachedIndex;
				}
			}

			// 2. Adopt the first live box anywhere (old ai[0] binding ignored,
			// then rebound below). Extra live boxes beyond the first are
			// retired silently, once — never in a loop (cached afterwards).
			int adopted = -1;
			for (int i = 0; i < Main.maxProjectiles; i++) {
				Projectile q = Main.projectile[i];
				if (q.active && q.type == boxType) {
					if (adopted < 0) {
						adopted = i;
					}
					else {
						q.active = false;
						q.netUpdate = true;
					}
				}
			}
			if (adopted >= 0)
			{
				Rebind(Main.projectile[adopted], guardian);
				cachedIndex = adopted;
				return cachedIndex;
			}

			if (Main.netMode == NetmodeID.MultiplayerClient)
				return cachedIndex;

			// First spawn of a fight centers on the boss; a box replacing a
			// silently killed one restores the remembered home (never chases).
			bool restore = lastHome.HasValue;
			Vector2 home = restore ? lastHome.Value : guardian.Center;
			// NewProjectile takes a TOP-LEFT position: offset by half the box so
			// the arena is actually centered instead of hanging 1500px down-right.
			Vector2 topLeft = home - new Vector2(HalfBox, HalfBox);
			int p = Projectile.NewProjectile(
				guardian.GetSource_FromAI(),
				topLeft,
				Vector2.Zero,
				ModContent.ProjectileType<GuardianArenaBox>(),
				0,
				0f,
				255,
				guardian.whoAmI
			);

            if (p >= 0 && p < Main.maxProjectiles)
            {
                // Absolute centering: Center-set accounts for hitbox size no
                // matter how the spawn position was interpreted.
                Main.projectile[p].Center = home;
                Main.projectile[p].netUpdate = true;
                cachedIndex = p;
            }

            return cachedIndex;
        }

        // Adopt-and-rebind: any live box becomes this fight's box. The stale
        // ai[0] binding is overwritten (never a reason to kill). The box is
        // NEVER moved here — spawn-once/stationary is absolute; the AI
        // home-snap below is the only position authority.
        private static void Rebind(Projectile box, NPC guardian)
        {
            // The live box IS the home: remember it, so any replacement
            // restores here even if this instance is silently killed.
            lastHome = box.Center;
            if ((int)box.ai[0] != guardian.whoAmI)
            {
                box.ai[0] = guardian.whoAmI;
                box.netUpdate = true;
            }
        }

        internal static void ForgetHome() { lastHome = null; }

        private Rectangle GetArenaRectangle()
        {
            return new Rectangle(
                (int)Projectile.Center.X - HalfBox,
                (int)Projectile.Center.Y - HalfBox,
                BoxSize,
                BoxSize
            );
        }

        internal static bool AnyLiveBox()
        {
            int boxType = ModContent.ProjectileType<GuardianArenaBox>();
            for (int i = 0; i < Main.maxProjectiles; i++)
            {
                Projectile q = Main.projectile[i];
                if (q.active && q.type == boxType)
                    return true;
            }
            return false;
        }

        internal static bool TryGetArenaScreenRect(out Rectangle screenRect)
        {
            screenRect = default;
            int boxType = ModContent.ProjectileType<GuardianArenaBox>();
            for (int i = 0; i < Main.maxProjectiles; i++)
            {
                Projectile q = Main.projectile[i];
                if (!q.active || q.type != boxType)
                    continue;
                screenRect = new Rectangle(
                    (int)q.Center.X - HalfBox - (int)Main.screenPosition.X,
                    (int)q.Center.Y - HalfBox - (int)Main.screenPosition.Y,
                    BoxSize,
                    BoxSize);
                return true;
            }
            return false;
        }

        private static NPC FindRealGuardian()
        {
            if (guardianType < 0)
                guardianType = ModContent.NPCType<YoyoGuardian>();
            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC npc = Main.npc[i];
                if (!npc.active || npc.type != guardianType)
                    continue;
                if (npc.ModNPC is YoyoGuardian guardian && guardian.IsRealBoss)
                    return npc;
            }
            return null;
        }

        private void AddArenaLight(Rectangle arena)
        {
            // Whole arena off screen: nothing visible can need light.
            float cull = 64f;
            if (arena.Right < Main.screenPosition.X - cull || arena.Left > Main.screenPosition.X + Main.screenWidth + cull ||
                arena.Bottom < Main.screenPosition.Y - cull || arena.Top > Main.screenPosition.Y + Main.screenHeight + cull)
                return;

            // Shared one-time build per home: every 16px grid point, skipping
            // points inside solid tiles (checked once here, never per-tick).
            // A restored box reuses the cache — no rebuild hitch, no darkness.
            Vector2 arenaCore = new Vector2(arena.X + HalfBox, arena.Y + HalfBox);
            if (!lightCacheValid || Vector2.DistanceSquared(arenaCore, lightCacheCenter) > 65536f)
            {
                lightCacheValid = true;
                lightCacheCenter = arenaCore;
                openAirTiles = new System.Collections.Generic.HashSet<int>();
                for (int x = arena.Left / LightStep; x <= arena.Right / LightStep; x++)
                {
                    for (int y = arena.Top / LightStep; y <= arena.Bottom / LightStep; y++)
                    {
                        if (x < 0 || y < 0 || x >= Main.maxTilesX || y >= Main.maxTilesY)
                            continue;
                        Tile tile = Main.tile[x, y];
                        if (tile != null && tile.HasTile && Main.tileSolid[tile.TileType] && !tile.IsActuated)
                            continue;
                        openAirTiles.Add(x * 8192 + y);
                    }
                }
            }
            if (openAirTiles == null || openAirTiles.Count == 0)
                return;

            // Visible screen tiles only: hash lookup + a little light each,
            // no tile reads, no refreshes.
            float margin = 64f;
            int left = (int)((Main.screenPosition.X - margin) / LightStep);
            int right = (int)((Main.screenPosition.X + Main.screenWidth + margin) / LightStep);
            int top = (int)((Main.screenPosition.Y - margin) / LightStep);
            int bottom = (int)((Main.screenPosition.Y + Main.screenHeight + margin) / LightStep);
            for (int x = left; x <= right; x++)
            {
                for (int y = top; y <= bottom; y++)
                {
                    if (x < 0 || y < 0 || x >= Main.maxTilesX || y >= Main.maxTilesY)
                        continue;
                    if (!openAirTiles.Contains(x * 8192 + y))
                        continue;
                    Lighting.AddLight(new Vector2(x * LightStep, y * LightStep), 0.5f, 0.5f, 0.5f);
                }
            }
        }

        private void KeepPlayerInside(Player player, Rectangle arena)
        {
            float minX = arena.Left;
            float maxX = arena.Right - player.width;

            float minY = arena.Top;
            float maxY = arena.Bottom - player.height;

            bool changed = false;

            if (player.position.X < minX)
            {
                player.position.X = minX;
                player.velocity.X = 0f;
                changed = true;
            }
            else if (player.position.X > maxX)
            {
                player.position.X = maxX;
                player.velocity.X = 0f;
                changed = true;
            }

            if (player.position.Y < minY)
            {
                player.position.Y = minY;
                player.velocity.Y = 0f;
                changed = true;
            }
            else if (player.position.Y > maxY)
            {
                player.position.Y = maxY;
                player.velocity.Y = 0f;
                changed = true;
            }

            if (changed && Main.netMode == NetmodeID.Server)
            {
                NetMessage.SendData(
                    MessageID.SyncPlayer,
                    -1,
                    -1,
                    null,
                    player.whoAmI
                );
            }
        }

        public override void SendExtraAI(System.IO.BinaryWriter writer) => writer.Write(Projectile.ai[1]);

        public override void ReceiveExtraAI(System.IO.BinaryReader reader) => Projectile.ai[1] = reader.ReadSingle();

 		// SPEC: black fill sits BEHIND tiles/NPCs (campfires, dummies included)
 		// but IN FRONT of the Terraria background, so it stays visible.
 		// behindNPCsAndTiles is exactly that layer. Both PreDraw (black fill)
 		// and PostDraw (border) run in this pass — nothing is ever drawn in
 		// front of tiles by this projectile.
 		public override void DrawBehind(int index, System.Collections.Generic.List<int> behindNPCsAndTiles, System.Collections.Generic.List<int> behindNPCs, System.Collections.Generic.List<int> behindProjectiles, System.Collections.Generic.List<int> overPlayers, System.Collections.Generic.List<int> overWiresUI)
 		{
 			behindNPCsAndTiles.Add(index);
 		}

		public override bool PreDraw(ref Color lightColor)
		{
			// One big opaque square (single draw call): the behind-tiles pass
			// draws it before tiles/NPCs, so solid tiles, platforms, campfires,
			// dummies, NPCs and the boss all paint over it while the vanilla
			// background stays buried underneath. False skips the default draw.
			SpriteBatch spriteBatch = Main.spriteBatch;
			Texture2D pixel = TextureAssets.MagicPixel.Value;
			Rectangle worldArena = GetArenaRectangle();
			Rectangle arena = new Rectangle(
				worldArena.X - (int)Main.screenPosition.X,
				worldArena.Y - (int)Main.screenPosition.Y,
				worldArena.Width,
				worldArena.Height
			);
			spriteBatch.Draw(pixel, arena, Color.Black);
			return false;
		}

        public override void PostDraw(Color lightColor)
        {
            SpriteBatch spriteBatch = Main.spriteBatch;
            Texture2D pixel = TextureAssets.MagicPixel.Value;

            Rectangle worldArena = GetArenaRectangle();

            Rectangle arena = new Rectangle(
                worldArena.X - (int)Main.screenPosition.X,
                worldArena.Y - (int)Main.screenPosition.Y,
                worldArena.Width,
                worldArena.Height
            );

            // Addition #6: purple border in Phase 3, same white border everywhere else.
            Color borderColor = PurpleBorder ? new Color(150, 60, 230) : Color.White;

            spriteBatch.Draw(pixel, new Rectangle(arena.Left, arena.Top, arena.Width, BorderSize), borderColor);
            spriteBatch.Draw(pixel, new Rectangle(arena.Left, arena.Bottom - BorderSize, arena.Width, BorderSize), borderColor);
            spriteBatch.Draw(pixel, new Rectangle(arena.Left, arena.Top, BorderSize, arena.Height), borderColor);
            spriteBatch.Draw(pixel, new Rectangle(arena.Right - BorderSize, arena.Top, BorderSize, arena.Height), borderColor);
        }
    }
}

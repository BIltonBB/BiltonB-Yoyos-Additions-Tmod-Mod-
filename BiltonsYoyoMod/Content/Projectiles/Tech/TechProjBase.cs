using System;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace BiltonsYoyoMod.Content.Projectiles.Tech
{
	/// <summary>
	/// Base for all tech shells. Carries the source yoyo item + enhancement ids
	/// across the network (SendExtraAI) so remotes match the owner.
	/// Tech shells are NOT aiStyle Yoyo: movement is fully custom, and yoyo
	/// bonuses are re-applied explicitly via YoyoTechCommon.
	/// </summary>
	public abstract class TechProjBase : ModProjectile
	{
		public int SourceItemType;
		public int SourceProjType;
		public int ModId = -1;
		public int SpellId = -1;

		protected int deployTicks;
		protected int echoTimer;
		protected int boltTimer;

		public void Init(int sourceItemType, int modId, int spellId)
		{
			SourceItemType = sourceItemType;
			ModId = modId;
			SpellId = spellId;
		}

		public override void SendExtraAI(BinaryWriter writer)
		{
			writer.Write(SourceItemType);
			writer.Write(SourceProjType);
			writer.Write((short)ModId);
			writer.Write((short)SpellId);
		}

		public override void ReceiveExtraAI(BinaryReader reader)
		{
			SourceItemType = reader.ReadInt32();
			SourceProjType = reader.ReadInt32();
			ModId = reader.ReadInt16();
			SpellId = reader.ReadInt16();
		}

		protected override bool CloneNewInstances => true;

		protected Player OwnerPlayer()
		{
			if (Projectile.owner < 0 || Projectile.owner >= Main.maxPlayers)
				return null;
			Player p = Main.player[Projectile.owner];
			return (p != null && p.active) ? p : null;
		}

		/// <summary>Draw the source yoyo's sprite (or this shell's) under proper lighting.
		///
		/// NEVER DRAW NOTHING. A shell whose source sprite cannot be resolved -
		/// a vanilla yoyo whose projectile art is not where the mod expects it,
		/// a yoyo from an unloaded mod, a stale synced source type - used to
		/// resolve to a null texture and then simply vanish, so the orbit looked
		/// EMPTY and the report was "why are vanilla yoyos invisible in the
		/// orbiter". The chain of fallbacks is therefore: the source yoyo's own
		/// texture, then this shell's texture, then a soft glow bead. There is
		/// always something drawn in the slot.</summary>
		/// <summary>
		/// Resolve the sprite an orbiter should wear, plus the frame and the
		/// scale that keep it the size of a yoyo.
		///
		/// The SOURCE YOYO'S PROJECTILE SPRITE is the art here - an orbiter is
		/// a shell of that projectile, not a copy of the inventory icon.
		///
		/// A modded yoyo may ship that projectile as a sprite SHEET (or a huge
		/// glow plate). Drawing the whole texture in an orbit slot produced a
		/// big mostly-transparent SQUARE with a tiny yoyo somewhere inside it.
		/// So: crop the first animation frame when the sheet declares frames,
		/// and cap the longest side to about one and a half tiles. Orbiters must
		/// wear the source yoyo's PROJECTILE sprite; item icons are never used.
		/// </summary>
		protected bool TryGetSourceArt(out Texture2D tex, out Rectangle? frame, out float scale)
		{
			tex = null;
			frame = null;
			scale = Projectile.scale;
			if (scale <= 0.01f || float.IsNaN(scale))
				scale = 1f;

			// Some shells are created by a path that never wrote ai[1] (or a
			// remote copy arrives before the source id is synced). Deriving the
			// projectile from the synced SOURCE ITEM closes that hole: a vanilla
			// yoyo whose projectile id is momentarily 0 then still wears the
			// right art instead of falling through to a near-invisible fallback.
			int sourceProj = SourceProjType;
			if (sourceProj <= ProjectileID.None && SourceItemType > ItemID.None
				&& ContentSamples.ItemsByType.TryGetValue(SourceItemType, out Item sample)
				&& sample != null && sample.shoot > ProjectileID.None) {
				sourceProj = sample.shoot;
			}

			if (sourceProj > ProjectileID.None && sourceProj < TextureAssets.Projectile.Length) {
				try {
					// tML preloads MODDED textures, but vanilla projectile
					// textures are lazy. Asset<Texture2D>.Value returns a 1x1
					// TRANSPARENT texture while unloaded (not null), so reading it
					// before Main.LoadProjectile has run produced the vanilla-only
					// "the orbiter has no body" report. Load the vanilla source
					// exactly like the vanilla projectile renderer does, then
					// check IsLoaded before trusting the texture.
					var sourceAsset = TextureAssets.Projectile[sourceProj];
					if (!sourceAsset.IsLoaded && sourceProj < ProjectileID.Count)
						Main.instance.LoadProjectile(sourceProj);
					if (sourceAsset.IsLoaded) {
						tex = sourceAsset.Value;
						int frames = sourceProj < Main.projFrames.Length
							? Math.Max(1, Main.projFrames[sourceProj]) : 1;
						if (frames > 1 && tex.Height % frames == 0)
							frame = new Rectangle(0, 0, tex.Width, tex.Height / frames);
					}
				}
				catch { tex = null; }
			}

			// Do NOT fall back to the item icon. An orbiter must wear the source
			// yoyo projectile's sprite; if that texture cannot be resolved the
			// shell texture below is the safe visible fallback.

			if (tex == null) {
				try {
					var shellAsset = TextureAssets.Projectile[Type];
					if (shellAsset.IsLoaded)
						tex = shellAsset.Value;
				}
				catch { tex = null; }
			}

			if (tex == null)
				return false;

			Vector2 size = frame.HasValue ? frame.Value.Size() : tex.Size();
			float longest = Math.Max(size.X, size.Y);
			// Vanilla and modded yoyo projectile sprites are the same size, so
			// there is no vanilla upscale. Only cap oversized sheets/glow plates
			// at about one and a half tiles; small yoyos draw at their own size.
			scale *= Math.Min(1f, 44f / Math.Max(1f, longest));
			return true;
		}

		protected void DrawSourceSprite(Color lightColor, float alpha = 1f)
		{
			if (!TryGetSourceArt(out Texture2D tex, out Rectangle? frame, out float scale)) {
				global::BiltonsYoyoMod.Projectiles.MiniYoyoFx.DrawGlowAdditive(
					Projectile.Center, 34f * Projectile.scale, Color.White, 0.45f * alpha);
				return;
			}
			Vector2 size = frame.HasValue ? frame.Value.Size() : tex.Size();
			Vector2 origin = size * 0.5f;
			Main.EntitySpriteDraw(tex, Projectile.Center - Main.screenPosition, frame,
				lightColor * alpha, Projectile.rotation, origin, scale, SpriteEffects.None, 0);
		}

		/// <summary>
		/// Generic "shooting preservation": every ~2s the shell fires a bolt toward
		/// the nearest enemy at 50% damage. Complex yoyo-specific patterns cannot
		/// run without their original AI; this keeps every tech shell shooting.
		/// </summary>
		protected void GenericEchoShot(int damageMultPercent = 50, int interval = 120)
		{
			if (Projectile.owner != Main.myPlayer)
				return;
			boltTimer++;
			if (boltTimer < interval)
				return;
			boltTimer = 0;
			NPC target = global::BiltonsYoyoMod.Content.Tech.YoyoTechCommon.FindTarget(Projectile.Center, 700f);
			if (target == null)
				return;
			Vector2 dir = (target.Center - Projectile.Center).SafeNormalize(Vector2.Zero);
			int dmg = System.Math.Max(1, Projectile.damage * damageMultPercent / 100);
			Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, dir * 14f,
				ModContent.ProjectileType<TechBoltProj>(), dmg, 1f, Projectile.owner);
			Terraria.Audio.SoundEngine.PlaySound(SoundID.Item11, Projectile.Center);
			// Muzzle flash so every shot pops.
			for (int i = 0; i < 4; i++) {
				Dust d = Dust.NewDustDirect(Projectile.Center - new Vector2(4, 4), 8, 8,
					DustID.MagicMirror, dir.X * 2f + Main.rand.NextFloat(-1f, 1f), dir.Y * 2f + Main.rand.NextFloat(-1f, 1f), 120, default, 0.8f);
				d.noGravity = true;
			}
		}

		protected void ApplySpellDebuff(NPC target)
		{
			global::BiltonsYoyoMod.Content.Tech.YoyoTechCommon.ApplySpellOnHit(target, SpellId);
		}

		/// <summary>HIT echo channel: every tech shell forwards hits.</summary>
		protected void EchoHit(NPC target)
		{
			global::BiltonsYoyoMod.Content.Tech.YoyoTechEcho.OnHit(this, target);
		}

		/// <summary>PASSIVE echo channel: summon + magical shells only, reduced.</summary>
		protected void EchoPassive(int shellTicks)
		{
			global::BiltonsYoyoMod.Content.Tech.YoyoTechEcho.Passive(this, shellTicks);
		}

		protected void TechStringFx(Player owner)
		{
			global::BiltonsYoyoMod.Content.Tech.YoyoTechCommon.StringDust(owner.Center, Projectile.Center, owner);
			global::BiltonsYoyoMod.Content.Tech.YoyoTechCommon.AncientMagnet(Projectile, owner);
		}
	}

	/// <summary>Small generic bolt fired by tech shells (preserves "shooting" everywhere).</summary>
	public class TechBoltProj : ModProjectile
	{
		public override void SetDefaults()
		{
			Projectile.width = 10;
			Projectile.height = 10;
			Projectile.friendly = true;
			Projectile.hostile = false;
			Projectile.DamageType = DamageClass.MeleeNoSpeed;
			Projectile.penetrate = 1;
			Projectile.timeLeft = 180;
			Projectile.tileCollide = true;
			Projectile.ignoreWater = true;
			Projectile.extraUpdates = 1;
		}

		public override void AI()
		{
			Projectile.rotation += 0.4f;
			Lighting.AddLight(Projectile.Center, 0.4f, 0.5f, 0.7f);
			if (Main.rand.NextBool(3)) {
				Dust d = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height,
					DustID.MagicMirror, 0f, 0f, 120, default, 0.8f);
				d.noGravity = true;
			}
		}
	}
}

using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Graphics.Shaders;
using Terraria.ID;
using Terraria.ModLoader;

namespace BiltonsYoyoMod.Players
{
	// Flags for the black-market and relic accessories.
	public class MarketPlayer : ModPlayer
	{
		public bool GoldenCounterweight;
		public bool CelestialBlessing;
		public bool AncientCounterweight;
		public bool GoldenString;
		public bool AncientString;
		public bool AncientGlove;
		public bool AncientRange;
		public bool AncientRelic;
		private int counterweightCooldown;

		public override void ResetEffects()
		{
			GoldenCounterweight = false;
			CelestialBlessing = false;
			AncientCounterweight = false;
			GoldenString = false;
			AncientString = false;
			AncientGlove = false;
			AncientRange = false;
			AncientRelic = false;
		}

		public override void PostUpdateEquips()
		{
			// Deterministic string-colour priority so multiple string
			// accessories never fight: Barbed > Tangled > Golden > Ancient.
			// Runs after every UpdateAccessory, so stored (case) strings and
			// normal-slot strings resolve identically.
			RevampPlayer revamp = Player.GetModPlayer<RevampPlayer>();
			if (revamp.Barbed || revamp.Tangled)
				return;
			if (GoldenString) {
				Player.yoyoString = true;
				Player.stringColor = Content.NPCs.Town.YoyoTownShared.YellowStringShader;
			}
			else if (AncientString) {
				Player.yoyoString = true;
				Player.stringColor = Content.NPCs.Town.YoyoTownShared.PurpleStringShader;
			}
		}

		public override void PostUpdate()
		{
			if (counterweightCooldown > 0)
				counterweightCooldown--;
			ApplyStringLighting();
			EnforceStringColor();
			// Hurl a counterweight toward the cursor while a yoyo is deployed.
			if (Main.myPlayer != Player.whoAmI)
				return;
			int tier = AncientCounterweight ? 1 : GoldenCounterweight ? 0 : -1;
			if (tier < 0 || counterweightCooldown > 0)
				return;
			if (!HasActiveYoyo())
				return;
			for (int i = 0; i < Main.maxProjectiles; i++) {
				Projectile proj = Main.projectile[i];
				if (proj.active && proj.owner == Player.whoAmI && proj.ModProjectile is Content.Projectiles.CounterweightProjBase)
					return; // Already flying.
			}
			Vector2 dir = (Main.MouseWorld - Player.Center).SafeNormalize(Vector2.UnitX);
			float speed = tier == 1 ? 16f : 14f;
			// Scale with the player's yoyo damage bonuses like a vanilla throw.
			int damage = (int)Player.GetDamage(DamageClass.MeleeNoSpeed).ApplyTo(tier == 1 ? 28 : 15);
			int cwType = tier == 1
				? ModContent.ProjectileType<Content.Projectiles.AncientCounterweightProj>()
				: ModContent.ProjectileType<Content.Projectiles.GoldenCounterweightProj>();
			int idx = Projectile.NewProjectile(Player.GetSource_Accessory(new Item()), Player.Center, dir * speed,
				cwType, damage, 4f, Player.whoAmI);
			if (idx >= 0 && tier == 1)
				Main.projectile[idx].scale = 1.15f;
			counterweightCooldown = 300; // Rethrow every 5s while the yoyo stays out.
		}

		// String colors read straight off the equipped items every tick, so
		// no flag, ordering or storage quirk can ever blank them. Priority:
		// Barbed > Tangled > Golden > Ancient > worn vanilla > stored.
		private void EnforceStringColor()
		{
			int bestRank = 0;
			int bestShader = 0;
			foreach (Item acc in Player.armor) {
				if (acc == null || acc.IsAir)
					continue;
				string tn = acc.ModItem?.GetType().Name ?? string.Empty;
				int rank = 0;
				int shader = 0;
				if (tn == "BarbedStrings") {
					rank = 5;
					shader = Content.NPCs.Town.YoyoTownShared.BlackStringShader;
				}
				else if (tn == "TangledStrings") {
					rank = 4;
					shader = Content.NPCs.Town.YoyoTownShared.SilverStringShader;
				}
				else if (tn == "GoldenString") {
					rank = 3;
					shader = Content.NPCs.Town.YoyoTownShared.YellowStringShader;
				}
				else if (tn == "AncientString") {
					rank = 2;
					shader = Content.NPCs.Town.YoyoTownShared.PurpleStringShader;
				}
				else if (acc.accessory && acc.stringColor != 0) {
					rank = 1;
					shader = acc.stringColor;
				}
				if (rank > bestRank) {
					bestRank = rank;
					bestShader = shader;
				}
			}
			if (bestRank == 0) {
				// No worn string: a stored one still colors the line.
				YoyoStoragePlayer storage = Player.GetModPlayer<YoyoStoragePlayer>();
				foreach (int index in storage.VisibleSlots) {
					if (index < 0 || index >= YoyoStoragePlayer.SlotTypes.Length)
						continue;
					if (YoyoStoragePlayer.SlotTypes[index] != YoyoStorageSlotType.Strings)
						continue;
					Item stored = storage.Storage[index];
					if (stored != null && !stored.IsAir && stored.stringColor != 0) {
						bestRank = 1;
						bestShader = stored.stringColor;
						break;
					}
				}
			}
			if (bestRank > 0) {
				Player.yoyoString = true;
				Player.stringColor = bestShader;
			}
		}

		public void ApplyStringLighting()
		{			// Called every tick: golden/ancient strings glow along the line.
			if (Main.myPlayer != Player.whoAmI)
				return;
			if (!GoldenString && !AncientString)
				return;
			Vector3 tint = GoldenString
				? new Vector3(1f, 0.8f, 0.35f)
				: new Vector3(0.4f, 0.7f, 1f);
			for (int i = 0; i < Main.maxProjectiles; i++) {
				Projectile proj = Main.projectile[i];
				if (!proj.active || proj.owner != Player.whoAmI)
					continue;
				// Tech shells count: they replaced the yoyo, so strings glow on them too.
				if (proj.aiStyle != ProjAIStyleID.Yoyo
					&& proj.ModProjectile is not Content.Projectiles.Tech.TechProjBase)
					continue;
				Lighting.AddLight(proj.Center, tint * 0.6f);
				Lighting.AddLight((Player.Center + proj.Center) * 0.5f, tint * 0.45f);
			}
		}

		private bool HasActiveYoyo()
		{
			for (int i = 0; i < Main.maxProjectiles; i++) {
				Projectile proj = Main.projectile[i];
				if (!proj.active || proj.owner != Player.whoAmI)
					continue;
				// Tech shells count as deployed yoyos so counterweights keep throwing.
				if (proj.aiStyle == ProjAIStyleID.Yoyo
					|| proj.ModProjectile is Content.Projectiles.Tech.TechProjBase)
					return true;
			}
			return false;
		}
	}
}

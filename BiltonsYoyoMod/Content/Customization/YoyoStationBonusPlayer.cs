using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using BiltonsYoyoMod.Common;
using BiltonsYoyoMod.Tiles;

namespace BiltonsYoyoMod.Content.Customization
{
	// Station proximity bonuses (temporary, recalculated each tick, never permanent).
	// Stacking rule: different stations stack; two copies of one station grant it once.
	// Every station also runs a unique proximity ritual AND a unique perk beyond the
	// headline stat, so standing near each machine feels different:
	//  Adjustment: +3% yoyo damage, +2 defense (workbench cover), reforge ticker.
	//  Tinkering: +5% yoyo velocity, +5% move speed, trajectory predictor line.
	//  Specialized Adjustment: +3% yoyo crit, +20 max mana (attuned mind), focus meter.
	//  Calibrating: +5% yoyo range, danger sense (calibrated lenses), range ring.
	//  Specialized Tinkering: +5% velocity and range, treasure sense, orbiting wisp.
	//  Magical Adjustment: +5% damage, +3% crit, +2 life regen, arcane aura.
	public class YoyoStationBonusPlayer : ModPlayer
	{
		public bool NearAdjustment;
		public bool NearTinkering;
		public bool NearSpecAdjustment;
		public bool NearCalibrating;
		public bool NearSpecTinkering;
		public bool NearMagical;

		private bool wasAdjustment;
		private bool wasTinkering;
		private bool wasSpecAdjustment;
		private bool wasCalibrating;
		private bool wasSpecTinkering;
		private bool wasMagical;

		private int fxTimer;

		public override void ResetEffects()
		{
			NearAdjustment = false;
			NearTinkering = false;
			NearSpecAdjustment = false;
			NearCalibrating = false;
			NearSpecTinkering = false;
			NearMagical = false;
		}

		public override void UpdateEquips()
		{
			ScanStations();
			// Damage / crit apply through the yoyo damage class (MeleeNoSpeed shape).
			if (NearAdjustment) {
				Player.GetDamage(DamageClass.MeleeNoSpeed) += 0.03f;
				Player.statDefense += 2;
			}
			if (NearTinkering)
				Player.moveSpeed += 0.05f;
			if (NearMagical) {
				Player.GetDamage(DamageClass.MeleeNoSpeed) += 0.05f;
				Player.lifeRegen += 2;
			}
			if (NearSpecAdjustment)
				Player.statManaMax2 += 20;
			if (NearSpecAdjustment || NearMagical)
				Player.GetCritChance(DamageClass.MeleeNoSpeed) += 3f;
			if (NearCalibrating)
				Player.dangerSense = true;
			if (NearSpecTinkering)
				Player.findTreasure = true;
			// Velocity / range apply to the held yoyo's projectile type via static boosts
			// (reset every tick by YoyoBoostSystem, so they never accumulate).
			Item held = Player.HeldItem;
			if (held != null && !held.IsAir && held.shoot > ProjectileID.None
				&& held.shoot < ProjectileID.Sets.YoyosMaximumRange.Length
				&& ProjectileID.Sets.YoyosMaximumRange[held.shoot] > 0f) {
				if (NearTinkering)
					YoyoStaticBoosts.BoostTopSpeed(held.shoot, 1.05f);
				if (NearSpecTinkering) {
					YoyoStaticBoosts.BoostTopSpeed(held.shoot, 1.05f);
					YoyoStaticBoosts.BoostRange(held.shoot, 16f);
				}
				if (NearCalibrating)
					YoyoStaticBoosts.BoostRange(held.shoot, 16f);
			}
		}

		public override void PostUpdate()
		{
			fxTimer++;
			if (fxTimer % 300 == 0)
				RunUniqueRitual();
			// Latch proximity AFTER the ritual read it, so CombatText below
			// only fires on the entering tick, never as a 5-second loop.
			wasAdjustment = NearAdjustment;
			wasTinkering = NearTinkering;
			wasSpecAdjustment = NearSpecAdjustment;
			wasCalibrating = NearCalibrating;
			wasSpecTinkering = NearSpecTinkering;
			wasMagical = NearMagical;
			// Spec Tinkering wisp: a charge mote orbits the player while near.
			if (NearSpecTinkering && fxTimer % 6 == 0) {
				Vector2 pos = Player.Center + new Vector2(56f, 0f).RotatedBy(fxTimer * 0.09f);
				Dust d = Dust.NewDustDirect(pos - new Vector2(2, 2), 4, 4, DustID.MagicMirror, 0f, 0f, 120, default, 0.8f);
				d.noGravity = true;
				d.velocity = Vector2.Zero;
			}
		}

		private void ScanStations()
		{
			const int radiusTiles = 10;
			Point pt = Player.Center.ToTileCoordinates();
			for (int x = pt.X - radiusTiles; x <= pt.X + radiusTiles; x++) {
				for (int y = pt.Y - radiusTiles; y <= pt.Y + radiusTiles; y++) {
					if (!WorldGen.InWorld(x, y))
						continue;
					Tile t = Framing.GetTileSafely(x, y);
					if (!t.HasTile)
						continue;
					int type = t.TileType;
					if (type == ModContent.TileType<AdjustmentMachineTile>()) NearAdjustment = true;
					else if (type == ModContent.TileType<TinkeringMachineTile>()) NearTinkering = true;
					else if (type == ModContent.TileType<SpecializedAdjustmentMachineTile>()) NearSpecAdjustment = true;
					else if (type == ModContent.TileType<CalibratingMachineTile>()) NearCalibrating = true;
					else if (type == ModContent.TileType<SpecializedTinkeringMachineTile>()) NearSpecTinkering = true;
					else if (type == ModContent.TileType<MagicalAdjustmentMachineTile>()) NearMagical = true;
				}
			}
		}

		private void RunUniqueRitual()
		{
			// CombatText fires ONLY on the entering tick (Near && !was).
			// Dust stays periodic ambience. No more 5-second text loops.
			if (NearAdjustment) {
				Item held = Player.HeldItem.IsAir ? new Item(ItemID.WoodYoyo) : Player.HeldItem;
				if (!wasAdjustment) {
					int price = YoyoCustomizationSystem.GetReforgePrice(held);
					CombatText.NewText(Player.getRect(), Color.Gold, $"Reforge today: {price / 10000}g {(price / 100) % 100}s (+2 def near bench)");
				}
				for (int i = 0; i < 6; i++) {
					Dust d = Dust.NewDustDirect(Player.position, Player.width, Player.height, DustID.Torch, 0f, -1f, 100, default, 0.8f);
					d.noGravity = true;
				}
			}
			if (NearTinkering && Content.NPCs.Town.YoyoTownShared.HasActiveYoyo(Player)) {
				// Trajectory predictor: dust line toward the deployed yoyo.
				Projectile yoyo = null;
				for (int i = 0; i < Main.maxProjectiles; i++) {
					Projectile p = Main.projectile[i];
					if (p.active && p.owner == Player.whoAmI && p.aiStyle == ProjAIStyleID.Yoyo) { yoyo = p; break; }
				}
				if (yoyo != null) {
					for (int s = 1; s <= 8; s++) {
						Vector2 pos = Vector2.Lerp(Player.Center, yoyo.Center, s / 9f);
						Dust d = Dust.NewDustDirect(pos - new Vector2(2, 2), 4, 4, DustID.Electric, 0f, 0f, 120, default, 0.7f);
						d.noGravity = true;
					}
				}
			}
			if (NearSpecAdjustment) {
				float crit = Player.GetCritChance(DamageClass.MeleeNoSpeed);
				if (!wasSpecAdjustment)
					CombatText.NewText(Player.getRect(), Color.Orange, $"Focus: {crit:0}% crit (+20 mana)");
			}
			if (NearCalibrating) {
				// Range ring at held yoyo max range.
				Item held = Player.HeldItem;
				if (held != null && !held.IsAir && held.shoot > ProjectileID.None && held.shoot < ProjectileID.Sets.YoyosMaximumRange.Length) {
					float range = ProjectileID.Sets.YoyosMaximumRange[held.shoot];
					for (int i = 0; i < 24; i++) {
						Vector2 pos = Player.Center + MathHelper.TwoPi.ToRotationVector2().RotatedBy(i / 24f * MathHelper.TwoPi) * range;
						Dust d = Dust.NewDustDirect(pos - new Vector2(2, 2), 4, 4, DustID.Grass, 0f, 0f, 120, default, 0.7f);
						d.noGravity = true;
					}
					if (!wasCalibrating)
						CombatText.NewText(Player.getRect(), Color.LightGreen, "Lenses hum: hazards revealed");
				}
			}
			if (NearSpecTinkering) {
				for (int i = 0; i < 10; i++) {
					Dust d = Dust.NewDustDirect(Player.position, Player.width, Player.height, DustID.MagicMirror, Main.rand.NextFloat(-2f, 2f), Main.rand.NextFloat(-2f, 2f), 100, default, 0.9f);
					d.noGravity = true;
				}
				if (!wasSpecTinkering)
					CombatText.NewText(Player.getRect(), Color.Cyan, "Sensors tuned: treasure glimmers");
			}
			if (NearMagical) {
				Lighting.AddLight(Player.Center, 0.4f, 0.2f, 0.5f);
				for (int i = 0; i < 6; i++) {
					Dust d = Dust.NewDustDirect(Player.position, Player.width, Player.height, DustID.Shadowflame, 0f, -1f, 100, default, 0.9f);
					d.noGravity = true;
				}
				if (!wasMagical)
					CombatText.NewText(Player.getRect(), new Color(200, 170, 255), "The fold mends you (+regen)");
			}
		}
	}
}

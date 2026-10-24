using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace BiltonsYoyoMod.Content.Developer
{
	// Per-player cosmetic yoyo effects toggled from the Shiny Ball panel.
	// Client-side visual only (no networking needed).
	public class ShinyBallPlayer : ModPlayer
	{
		public bool YoyoEffects = true;
		public int EffectStyle; // 0 Rainbow, 1 Gold, 2 Shadow

		public static string StyleName(int s) => s switch {
			1 => "Gold",
			2 => "Shadow",
			_ => "Rainbow",
		};

		public override void SaveData(TagCompound tag)
		{
			tag["shinyFx"] = YoyoEffects;
			tag["shinyStyle"] = EffectStyle;
		}

		public override void LoadData(TagCompound tag)
		{
			YoyoEffects = !tag.ContainsKey("shinyFx") || tag.GetBool("shinyFx");
			EffectStyle = tag.ContainsKey("shinyStyle") ? System.Math.Clamp(tag.GetInt("shinyStyle"), 0, 2) : 0;
		}
	}

	// Cosmetic dust on friendly yoyos while effects are on.
	public class ShinyBallGlobalProjectile : GlobalProjectile
	{
		public override void PostAI(Projectile projectile)
		{
			if (Main.dedServ || projectile.aiStyle != ProjAIStyleID.Yoyo)
				return;
			if (projectile.owner < 0 || projectile.owner >= Main.maxPlayers)
				return;
			Player owner = Main.player[projectile.owner];
			if (owner == null || !owner.active)
				return;
			var mp = owner.GetModPlayer<ShinyBallPlayer>();
			if (!mp.YoyoEffects || Main.GameUpdateCount % 3 != 0)
				return;
			int dustType = mp.EffectStyle switch {
				1 => DustID.GoldCoin,
				2 => DustID.Shadowflame,
				_ => DustID.RainbowMk2,
			};
			Dust d = Dust.NewDustDirect(projectile.position, projectile.width, projectile.height, dustType, 0f, 0f, 150, default, 0.8f);
			d.noGravity = true;
			d.velocity *= 0.3f;
		}
	}

	// Dev-only grant: /shinyball gives the block (no recipe/shop/drop).
	public class ShinyBallCommand : ModCommand
	{
		public override CommandType Type => CommandType.Chat;
		public override string Command => "shinyball";
		public override string Usage => "/shinyball";
		public override string Description => "Grants the developer Shiny Ball block.";

		public override void Action(CommandCaller caller, string input, string[] args)
		{
			Player player = caller.Player;
			int type = ModContent.ItemType<Tiles.Developer.ShinyBallDevItem>();
			player.QuickSpawnItem(player.GetSource_Misc("shinyball"), type);
			caller.Reply("A shiny ball rolls into your pocket. Shhh.", Color.Gold);
		}
	}
}

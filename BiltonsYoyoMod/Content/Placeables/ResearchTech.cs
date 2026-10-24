using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace BiltonsYoyoMod.Content.Placeables
{
	// Research tech tree: per-block research bought with coins, existing
	// materials, or (capstone) a sacrificed held yoyo. Drag the canvas
	// with left mouse held; click a node (no drag) to research it.
	// Client pays locally (like upgrades); the server owns unlock state
	// and rebroadcasts via WorldData. All applies are prereq-checked.
	public class ResearchTechNode
	{
		public string Id;
		public string Name;
		public int X;
		public int Y;
		public Color Color;
		public string Desc;
		public int Coins;
		public int Mat;
		public int MatCount;
		public bool CostsYoyo; // capstone: consumes the held yoyo instead of mats
		public string Prereq; // single required node id, or null
		public int ReqCount; // minimum owned techs (capstone gate), 0 = none
	}

	public static class ResearchTech
	{
		public const string StyleOver = "style_over";
		public const string OverEff = "over_eff";
		public const string OverFlow = "over_flow";
		public const string StyleSurge = "style_surge";
		public const string SurgeEff = "surge_eff";
		public const string ImpDmg1 = "imp_dmg1";
		public const string ImpDmg2 = "imp_dmg2";
		public const string ImpKb = "imp_kb";
		public const string ImpCrit = "imp_crit";
		public const string ImpVolatile = "imp_volatile";
		public const string ImpSing = "imp_sing";
		public const string BurstRad = "burst_rad";
		public const string BurstDmg = "burst_dmg";
		public const string BurstKb = "burst_kb";
		public const string BurstAfter = "burst_after";
		public const string BurstVac = "burst_vac";
		public const string MomDur = "mom_dur";
		public const string MomMag = "mom_mag";
		public const string MomRedline = "mom_redline";
		public const string LaunchKb = "launch_kb";
		public const string LaunchDmg = "launch_dmg";
		public const string LaunchGilded = "launch_gilded";
		public const string ResearchSpd1 = "research_spd1";
		public const string ResearchSpd2 = "research_spd2";
		public const string DiscMat1 = "disc_mat1";
		public const string DiscMat2 = "disc_mat2";
		public const string DiscCoin = "disc_coin";
		public const string ParBench2 = "par_bench2";
		public const string ParBench3 = "par_bench3";
		public const string AutoQ1 = "auto_q1";
		public const string AutoQ2 = "auto_q2";
		public const string AutoQ3 = "auto_q3";
		public const string AutoQ4 = "auto_q4";
		public const string AutoQ5 = "auto_q5";
		public const string AutoQ6 = "auto_q6";
		public const string YieldG1 = "yield_g1";
		public const string YieldG2 = "yield_g2";
		public const string YieldS1 = "yield_s1";
		public const string YieldS2 = "yield_s2";
		public const string YieldC1 = "yield_c1";
		public const string YieldT1 = "yield_t1";
		public const string NetUp = "net_up";
		public const string NetRes = "net_res";
		public const string NetSyn = "net_syn";
		public const string NetFar = "net_far";
		public const string NetFail = "net_fail";
		public const string EvSig = "ev_sig";
		public const string EvOmen = "ev_omen";
		public const string EvCharm = "ev_charm";
		public const string EvChase = "ev_chase";
		public const string EvEye = "ev_eye";
		public const string FieldCombat = "field_combat";
		public const string FieldSurvey = "field_survey";
		public const string FieldCatalogue = "field_catalogue";
		public const string FieldPilot = "field_pilot";
		public const string FieldAngler = "field_angler";
		public const string FieldMaster = "field_master";
		public const string Salvage = "salvage";
		public const string SalvEff = "salv_eff";
		public const string SalvWide = "salv_wide";
		public const string SalvBulk = "salv_bulk";
		public const string SalvRec = "salv_rec";
		public const string ExpBoard = "exp_board";
		public const string ExpStakes = "exp_stakes";
		public const string ExpIntel = "exp_intel";
		public const string ExpFresh = "exp_fresh";
		public const string ExpDice = "exp_dice";
		public const string StNet1 = "stnet_1";
		public const string StNet2 = "stnet_2";
		public const string StNet3 = "stnet_3";
		public const string StNet4 = "stnet_4";
		public const string StNet5 = "stnet_5";
		public const string StNet6 = "stnet_6";
		public const string StNet7 = "stnet_7";
		// Aegis Mesh (defensive branch): same upgrade shape as Local Mesh but
		// larger projection (15 tiles per tech, 90 max) and defensive-only
		// unlocks. Triple-range node gates the 3x defensive trap range.
		public const string Aegis1 = "aegis_1";
		public const string Aegis2 = "aegis_2";
		public const string Aegis3 = "aegis_3";
		public const string Aegis4 = "aegis_4";
		public const string Aegis5 = "aegis_5";
		public const string Aegis6 = "aegis_6";
		public const string Aegis7 = "aegis_7";
		public const string AegisFar = "aegis_far";
		public const string SaveTech = "save_tech";
		public const string SaveFull = "save_full";
		public const string SaveVault = "save_vault";
		public const string ChargeCoils = "charge_coils";
		public const string ChargeWind = "charge_wind";
		public const string ChargeCells = "charge_cells";
		public const string ChargeInsul = "charge_insul";
		public const string ChargeCond = "charge_cond";
		public const string ChargeTrick = "charge_trick";
		public const string Overdrive = "overdrive";

		private static int Scraps() => ModContent.ItemType<global::BiltonsYoyoMod.Items.Materials.YoyoScraps>();
		private static int Parts() => ModContent.ItemType<global::BiltonsYoyoMod.Items.Materials.YoyoParts>();
		private static int Adv() => ModContent.ItemType<global::BiltonsYoyoMod.Items.Materials.AdvancedYoyoParts>();

		public static readonly System.Collections.Generic.List<ResearchTechNode> All = new() {
			new ResearchTechNode { Id = StyleOver, Name = "Over Time", X = 170, Y = -100, Color = new Color(170, 140, 255), Desc = "Unlocks Over Time release: charge drips out per hit at reduced power.", Coins = Item.buyPrice(gold: 8), Mat = Parts(), MatCount = 10 },
			new ResearchTechNode { Id = OverEff, Name = "Efficient Burn", X = 340, Y = -100, Color = new Color(170, 140, 255), Desc = "Over Time effectiveness 25% -> 40%.", Coins = Item.buyPrice(gold: 12), Mat = Adv(), MatCount = 6, Prereq = StyleOver },
			new ResearchTechNode { Id = OverFlow, Name = "Deep Spool", X = 510, Y = -100, Color = new Color(170, 140, 255), Desc = "Over Time burns 8 charge per hit instead of 10 (more ticks).", Coins = Item.buyPrice(gold: 15), Mat = Adv(), MatCount = 8, Prereq = OverEff },
			new ResearchTechNode { Id = StyleSurge, Name = "Surge Release", X = 680, Y = -100, Color = new Color(170, 140, 255), Desc = "Unlocks Surge release: big 25-charge gulps at strong power.", Coins = Item.buyPrice(gold: 18), Mat = Adv(), MatCount = 8, Prereq = OverFlow },
			new ResearchTechNode { Id = SurgeEff, Name = "Surge Tuning", X = 850, Y = -100, Color = new Color(170, 140, 255), Desc = "Surge power 60% -> 75%.", Coins = Item.buyPrice(gold: 25), Mat = Adv(), MatCount = 10, Prereq = StyleSurge },
			new ResearchTechNode { Id = ImpDmg1, Name = "Heavy Head", X = 170, Y = -200, Color = new Color(230, 120, 100), Desc = "Impact +150% damage.", Coins = Item.buyPrice(gold: 10), Mat = Adv(), MatCount = 6 },
			new ResearchTechNode { Id = ImpDmg2, Name = "Dense Core", X = 340, Y = -200, Color = new Color(230, 120, 100), Desc = "Impact +150% damage more.", Coins = Item.buyPrice(gold: 20), Mat = Adv(), MatCount = 8, Prereq = ImpDmg1 },
			new ResearchTechNode { Id = ImpKb, Name = "Seismic Strike", X = 510, Y = -200, Color = new Color(230, 120, 100), Desc = "Impact knockback 18 -> 28.", Coins = Item.buyPrice(gold: 15), Mat = Adv(), MatCount = 5, Prereq = ImpDmg2 },
			new ResearchTechNode { Id = ImpCrit, Name = "Deadeye Coil", X = 680, Y = -200, Color = new Color(230, 120, 100), Desc = "Impact crit chance +20%.", Coins = Item.buyPrice(gold: 18), Mat = Adv(), MatCount = 8, Prereq = ImpKb },
			new ResearchTechNode { Id = ImpVolatile, Name = "Volatile Mix", X = 850, Y = -200, Color = new Color(230, 120, 100), Desc = "Impact detonates a 40% burst on top.", Coins = Item.buyPrice(gold: 30), Mat = Adv(), MatCount = 12, Prereq = ImpCrit },
			new ResearchTechNode { Id = ImpSing, Name = "Singularity", X = 1020, Y = -200, Color = new Color(230, 120, 100), Desc = "Impact drags enemies within 150px inward.", Coins = Item.buyPrice(gold: 40), Mat = Adv(), MatCount = 15, Prereq = ImpVolatile },
			new ResearchTechNode { Id = BurstRad, Name = "Wide Blast", X = 170, Y = 0, Color = new Color(230, 160, 90), Desc = "Burst radius +40px.", Coins = Item.buyPrice(gold: 10), Mat = Adv(), MatCount = 6 },
			new ResearchTechNode { Id = BurstDmg, Name = "Shaped Charge", X = 340, Y = 0, Color = new Color(230, 160, 90), Desc = "Burst +100% damage.", Coins = Item.buyPrice(gold: 20), Mat = Adv(), MatCount = 8, Prereq = BurstRad },
			new ResearchTechNode { Id = BurstKb, Name = "Heavy Shove", X = 510, Y = 0, Color = new Color(230, 160, 90), Desc = "Burst knockback 15 -> 25.", Coins = Item.buyPrice(gold: 15), Mat = Adv(), MatCount = 5, Prereq = BurstDmg },
			new ResearchTechNode { Id = BurstAfter, Name = "Aftershock", X = 680, Y = 0, Color = new Color(230, 160, 90), Desc = "Bursts inflict Slow (2s) and Bleeding (4s).", Coins = Item.buyPrice(gold: 18), Mat = Adv(), MatCount = 8, Prereq = BurstKb },
			new ResearchTechNode { Id = BurstVac, Name = "Event Horizon", X = 850, Y = 0, Color = new Color(230, 160, 90), Desc = "Bursts drag enemies inward first.", Coins = Item.buyPrice(gold: 25), Mat = Adv(), MatCount = 10, Prereq = BurstAfter },
			new ResearchTechNode { Id = MomDur, Name = "Long Spin", X = 170, Y = 100, Color = new Color(120, 200, 230), Desc = "Momentum lasts 50% longer.", Coins = Item.buyPrice(gold: 8), Mat = Parts(), MatCount = 8 },
			new ResearchTechNode { Id = MomMag, Name = "Tailwind", X = 340, Y = 100, Color = new Color(120, 200, 230), Desc = "Momentum +10% speed.", Coins = Item.buyPrice(gold: 15), Mat = Adv(), MatCount = 5, Prereq = MomDur },
			new ResearchTechNode { Id = MomRedline, Name = "Redline Spin", X = 510, Y = 100, Color = new Color(120, 200, 230), Desc = "Momentum +15% strength, -40% duration. Live fast.", Coins = Item.buyPrice(gold: 18), Mat = Adv(), MatCount = 8, Prereq = MomMag },
			new ResearchTechNode { Id = LaunchKb, Name = "Railgun Friendship", X = 170, Y = 200, Color = new Color(150, 230, 120), Desc = "Launch knockback 22 -> 35. Goodbye.", Coins = Item.buyPrice(gold: 5), Mat = Scraps(), MatCount = 10 },
			new ResearchTechNode { Id = LaunchDmg, Name = "Mercy Valve", X = 340, Y = 200, Color = new Color(150, 230, 120), Desc = "Launch damage -90% -> -50%. Slightly less funny.", Coins = Item.buyPrice(gold: 8), Mat = Parts(), MatCount = 6, Prereq = LaunchKb },
			new ResearchTechNode { Id = LaunchGilded, Name = "Gilded Vent", X = 510, Y = 200, Color = new Color(240, 200, 90), Desc = "Full releases shower 2-5 gold coins. Stonks.", Coins = Item.buyPrice(platinum: 2), Mat = Adv(), MatCount = 10, Prereq = LaunchDmg },
			new ResearchTechNode { Id = ResearchSpd1, Name = "Quick Study", X = 170, Y = 300, Color = new Color(100, 220, 200), Desc = "All research completes 25% faster.", Coins = Item.buyPrice(gold: 8), Mat = Parts(), MatCount = 8, ReqCount = 2 },
			new ResearchTechNode { Id = ResearchSpd2, Name = "Cram Session", X = 340, Y = 300, Color = new Color(100, 220, 200), Desc = "Research another 25% faster (stacks).", Coins = Item.buyPrice(gold: 15), Mat = Adv(), MatCount = 6, Prereq = ResearchSpd1 },
			new ResearchTechNode { Id = ParBench2, Name = "Second Bay", X = 170, Y = 500, Color = new Color(220, 160, 190), Desc = "+1 concurrent research slot (2 at once).", Coins = Item.buyPrice(gold: 12), Mat = Parts(), MatCount = 10, ReqCount = 3 },
			new ResearchTechNode { Id = ParBench3, Name = "Third Bay", X = 340, Y = 500, Color = new Color(220, 160, 190), Desc = "+1 concurrent research slot (3 at once).", Coins = Item.buyPrice(gold: 25), Mat = Adv(), MatCount = 10, Prereq = ParBench2 },
			new ResearchTechNode { Id = DiscMat1, Name = "Bulk Order", X = 170, Y = 400, Color = new Color(220, 200, 140), Desc = "Research needs 15% fewer materials.", Coins = Item.buyPrice(gold: 10), Mat = Parts(), MatCount = 10, ReqCount = 2 },
			new ResearchTechNode { Id = DiscMat2, Name = "Wholesale", X = 340, Y = 400, Color = new Color(220, 200, 140), Desc = "Another 15% off research materials.", Coins = Item.buyPrice(gold: 18), Mat = Adv(), MatCount = 8, Prereq = DiscMat1 },
			new ResearchTechNode { Id = DiscCoin, Name = "Haggler", X = 510, Y = 400, Color = new Color(220, 200, 140), Desc = "20% off research coin costs.", Coins = Item.buyPrice(gold: 15), Mat = Parts(), MatCount = 8, Prereq = DiscMat1 },
			new ResearchTechNode { Id = Overdrive, Name = "Overdrive Core", X = 680, Y = 100, Color = new Color(255, 210, 120), Desc = "Sacrifice your HELD yoyo: +50% ALL release damage, forever. Requires 6 researched techs.", Coins = Item.buyPrice(gold: 20), CostsYoyo = true, ReqCount = 6 },
			new ResearchTechNode { Id = AutoQ1, Name = "Task Queue", X = 170, Y = 600, Color = new Color(130, 150, 190), Desc = "Queue 1 research (paid upfront): it auto-starts when a bay frees.", Coins = Item.buyPrice(gold: 10), Mat = Parts(), MatCount = 10, ReqCount = 3 },
			new ResearchTechNode { Id = AutoQ2, Name = "Batch Line", X = 340, Y = 600, Color = new Color(130, 150, 190), Desc = "+1 queue slot (2 total).", Coins = Item.buyPrice(gold: 12), Mat = Parts(), MatCount = 12, Prereq = AutoQ1 },
			new ResearchTechNode { Id = AutoQ3, Name = "Assembly Line", X = 510, Y = 600, Color = new Color(130, 150, 190), Desc = "+1 queue slot (3 total).", Coins = Item.buyPrice(gold: 15), Mat = Adv(), MatCount = 8, Prereq = AutoQ2 },
			new ResearchTechNode { Id = AutoQ4, Name = "Auto Scheduler", X = 680, Y = 600, Color = new Color(130, 150, 190), Desc = "+1 queue slot (4 total).", Coins = Item.buyPrice(gold: 18), Mat = Adv(), MatCount = 10, Prereq = AutoQ3 },
			new ResearchTechNode { Id = AutoQ5, Name = "Night Shift", X = 850, Y = 600, Color = new Color(130, 150, 190), Desc = "+1 queue slot (5 total).", Coins = Item.buyPrice(gold: 25), Mat = Adv(), MatCount = 12, Prereq = AutoQ4 },
			new ResearchTechNode { Id = AutoQ6, Name = "Full Automation", X = 1020, Y = 600, Color = new Color(130, 150, 190), Desc = "+1 queue slot (6 total). The block runs itself.", Coins = Item.buyPrice(gold: 30), Mat = Adv(), MatCount = 15, Prereq = AutoQ5 },
			new ResearchTechNode { Id = YieldG1, Name = "Compound Interest", X = 170, Y = 700, Color = new Color(235, 190, 90), Desc = "Completed research grants +25% points.", Coins = Item.buyPrice(gold: 12), Mat = Parts(), MatCount = 10, ReqCount = 4 },
			new ResearchTechNode { Id = YieldG2, Name = "Rich Veins", X = 340, Y = 700, Color = new Color(235, 190, 90), Desc = "Completed research grants +25% more points (stacks).", Coins = Item.buyPrice(gold: 16), Mat = Adv(), MatCount = 8, Prereq = YieldG1 },
			new ResearchTechNode { Id = YieldS1, Name = "Fast Study", X = 510, Y = 700, Color = new Color(235, 190, 90), Desc = "Passive study trickle +2 points per cycle.", Coins = Item.buyPrice(gold: 18), Mat = Adv(), MatCount = 8, Prereq = YieldG2 },
			new ResearchTechNode { Id = YieldS2, Name = "Overclocked Study", X = 680, Y = 700, Color = new Color(235, 190, 90), Desc = "Passive study trickle +2 more points per cycle.", Coins = Item.buyPrice(gold: 22), Mat = Adv(), MatCount = 10, Prereq = YieldS1 },
			new ResearchTechNode { Id = YieldC1, Name = "Efficiency", X = 850, Y = 700, Color = new Color(235, 190, 90), Desc = "Rushing research costs 25 -> 18 points.", Coins = Item.buyPrice(gold: 24), Mat = Adv(), MatCount = 10, Prereq = YieldS2 },
			new ResearchTechNode { Id = YieldT1, Name = "Deep Vaults", X = 1020, Y = 700, Color = new Color(235, 190, 90), Desc = "+500 points storage on this block.", Coins = Item.buyPrice(gold: 28), Mat = Adv(), MatCount = 12, Prereq = YieldC1 },
			new ResearchTechNode { Id = NetUp, Name = "Uplink", X = 170, Y = 800, Color = new Color(200, 210, 225), Desc = "Link up to 2 engines: they share all research. See the Network tab.", Coins = Item.buyPrice(gold: 15), Mat = Adv(), MatCount = 8, ReqCount = 5 },
			new ResearchTechNode { Id = NetRes, Name = "Resonant Study", X = 340, Y = 800, Color = new Color(200, 210, 225), Desc = "+10% research speed while linked.", Coins = Item.buyPrice(gold: 20), Mat = Adv(), MatCount = 10, Prereq = NetUp },
			new ResearchTechNode { Id = NetSyn, Name = "Station Synergy", X = 510, Y = 800, Color = new Color(200, 210, 225), Desc = "Nearby station bonuses doubled.", Coins = Item.buyPrice(gold: 22), Mat = Adv(), MatCount = 10, Prereq = NetRes },
			new ResearchTechNode { Id = NetFar, Name = "Long Range Uplink", X = 680, Y = 800, Color = new Color(200, 210, 225), Desc = "Link range 150 -> 300 tiles.", Coins = Item.buyPrice(gold: 25), Mat = Adv(), MatCount = 10, Prereq = NetSyn },
			new ResearchTechNode { Id = NetFail, Name = "Failover", X = 850, Y = 800, Color = new Color(200, 210, 225), Desc = "Linked survivor inherits points + queue when its partner is destroyed.", Coins = Item.buyPrice(gold: 30), Mat = Adv(), MatCount = 12, Prereq = NetFar },
			new ResearchTechNode { Id = EvSig, Name = "Strange Signals", X = 170, Y = 900, Color = new Color(170, 200, 90), Desc = "The engine picks up strange events, good or bad. Who knows why.", Coins = Item.buyPrice(gold: 15), Mat = Adv(), MatCount = 8, ReqCount = 5 },
			new ResearchTechNode { Id = EvOmen, Name = "Good Omens", X = 340, Y = 900, Color = new Color(170, 200, 90), Desc = "Good events become twice as likely.", Coins = Item.buyPrice(gold: 20), Mat = Adv(), MatCount = 10, Prereq = EvSig },
			new ResearchTechNode { Id = EvCharm, Name = "Warding Charms", X = 510, Y = 900, Color = new Color(170, 200, 90), Desc = "Bad events hit half as hard.", Coins = Item.buyPrice(gold: 22), Mat = Adv(), MatCount = 10, Prereq = EvOmen },
			new ResearchTechNode { Id = EvChase, Name = "Storm Chasing", X = 680, Y = 900, Color = new Color(170, 200, 90), Desc = "Events roll twice as often.", Coins = Item.buyPrice(gold: 25), Mat = Adv(), MatCount = 10, Prereq = EvCharm },
			new ResearchTechNode { Id = EvEye, Name = "Eye of the Storm", X = 850, Y = 900, Color = new Color(170, 200, 90), Desc = "Bad timed events expire twice as fast.", Coins = Item.buyPrice(gold: 30), Mat = Adv(), MatCount = 12, Prereq = EvChase },
			new ResearchTechNode { Id = FieldCombat, Name = "Combat Logs", X = 170, Y = 1000, Color = new Color(150, 110, 70), Desc = "Hunts file +1 pt each, bosses +25.", Coins = Item.buyPrice(gold: 6), Mat = Parts(), MatCount = 8, ReqCount = 2 },
			new ResearchTechNode { Id = FieldSurvey, Name = "Survey Kit", X = 340, Y = 1000, Color = new Color(150, 110, 70), Desc = "New biomes +10, new ores +3.", Coins = Item.buyPrice(gold: 8), Mat = Parts(), MatCount = 8, Prereq = FieldCombat },
			new ResearchTechNode { Id = FieldCatalogue, Name = "Catalogue", X = 510, Y = 1000, Color = new Color(150, 110, 70), Desc = "New items filed +2 each.", Coins = Item.buyPrice(gold: 10), Mat = Parts(), MatCount = 10, Prereq = FieldSurvey },
			new ResearchTechNode { Id = FieldPilot, Name = "Test Pilot", X = 680, Y = 1000, Color = new Color(150, 110, 70), Desc = "First throw of each yoyo +5.", Coins = Item.buyPrice(gold: 10), Mat = Parts(), MatCount = 10, Prereq = FieldCatalogue },
			new ResearchTechNode { Id = FieldAngler, Name = "Angler's Log", X = 850, Y = 1000, Color = new Color(150, 110, 70), Desc = "Each fish caught +3.", Coins = Item.buyPrice(gold: 12), Mat = Parts(), MatCount = 12, Prereq = FieldPilot },
			new ResearchTechNode { Id = FieldMaster, Name = "Seasoned Researcher", X = 1020, Y = 1000, Color = new Color(150, 110, 70), Desc = "All field gains doubled.", Coins = Item.buyPrice(gold: 25), Mat = Adv(), MatCount = 12, Prereq = FieldAngler },
			new ResearchTechNode { Id = Salvage, Name = "Scrap Salvage", X = 170, Y = 1100, Color = new Color(190, 130, 80), Desc = "Salvage scraps/parts into points (Research tab).", Coins = Item.buyPrice(gold: 8), Mat = Parts(), MatCount = 8, ReqCount = 3 },
			new ResearchTechNode { Id = SalvEff, Name = "Efficient Salvage", X = 340, Y = 1100, Color = new Color(190, 130, 80), Desc = "+50% salvage points.", Coins = Item.buyPrice(gold: 12), Mat = Parts(), MatCount = 10, Prereq = Salvage },
			new ResearchTechNode { Id = SalvWide, Name = "Wide Nets", X = 510, Y = 1100, Color = new Color(190, 130, 80), Desc = "Accepts ancient scraps, illegal parts, guardian cores.", Coins = Item.buyPrice(gold: 15), Mat = Adv(), MatCount = 8, Prereq = SalvEff },
			new ResearchTechNode { Id = SalvBulk, Name = "Bulk Processing", X = 680, Y = 1100, Color = new Color(190, 130, 80), Desc = "Salvage up to 99 units at once.", Coins = Item.buyPrice(gold: 18), Mat = Adv(), MatCount = 10, Prereq = SalvWide },
			new ResearchTechNode { Id = SalvRec, Name = "Matter Reclamation", X = 850, Y = 1100, Color = new Color(190, 130, 80), Desc = "+100% more salvage points.", Coins = Item.buyPrice(gold: 25), Mat = Adv(), MatCount = 12, Prereq = SalvBulk },
			new ResearchTechNode { Id = ExpBoard, Name = "Trial Board", X = 170, Y = 1200, Color = new Color(140, 90, 200), Desc = "Daily experiment offers appear in the Trials tab.", Coins = Item.buyPrice(gold: 15), Mat = Adv(), MatCount = 8, ReqCount = 5 },
			new ResearchTechNode { Id = ExpStakes, Name = "High Stakes", X = 340, Y = 1200, Color = new Color(140, 90, 200), Desc = "Trial amounts doubled.", Coins = Item.buyPrice(gold: 20), Mat = Adv(), MatCount = 10, Prereq = ExpBoard },
			new ResearchTechNode { Id = ExpIntel, Name = "Insider Info", X = 510, Y = 1200, Color = new Color(140, 90, 200), Desc = "Mystery contracts show their odds.", Coins = Item.buyPrice(gold: 22), Mat = Adv(), MatCount = 10, Prereq = ExpStakes },
			new ResearchTechNode { Id = ExpFresh, Name = "Fresh Leads", X = 680, Y = 1200, Color = new Color(140, 90, 200), Desc = "Re-roll today's offers for 50 pts.", Coins = Item.buyPrice(gold: 25), Mat = Adv(), MatCount = 10, Prereq = ExpIntel },
			new ResearchTechNode { Id = ExpDice, Name = "Loaded Dice", X = 850, Y = 1200, Color = new Color(140, 90, 200), Desc = "Mystery good outcomes likelier.", Coins = Item.buyPrice(gold: 30), Mat = Adv(), MatCount = 12, Prereq = ExpFresh },
			new ResearchTechNode { Id = StNet1, Name = "Local Mesh", X = 170, Y = 1300, Color = new Color(100, 180, 255), Desc = "Stations within 5 tiles gain a network bonus.", Coins = Item.buyPrice(gold: 10), Mat = Parts(), MatCount = 8, ReqCount = 4 },
			new ResearchTechNode { Id = StNet2, Name = "Mesh Reach II", X = 340, Y = 1300, Color = new Color(100, 180, 255), Desc = "Network range 5 -> 10 tiles.", Coins = Item.buyPrice(gold: 14), Mat = Adv(), MatCount = 6, Prereq = StNet1 },
			new ResearchTechNode { Id = StNet3, Name = "Mesh Reach III", X = 510, Y = 1300, Color = new Color(100, 180, 255), Desc = "Network range 10 -> 15 tiles.", Coins = Item.buyPrice(gold: 18), Mat = Adv(), MatCount = 8, Prereq = StNet2 },
			new ResearchTechNode { Id = StNet4, Name = "Mesh Reach IV", X = 680, Y = 1300, Color = new Color(100, 180, 255), Desc = "Network range 15 -> 20 tiles.", Coins = Item.buyPrice(gold: 22), Mat = Adv(), MatCount = 10, Prereq = StNet3 },
			new ResearchTechNode { Id = StNet5, Name = "Mesh Reach V", X = 850, Y = 1300, Color = new Color(100, 180, 255), Desc = "Network range 20 -> 25 tiles.", Coins = Item.buyPrice(gold: 25), Mat = Adv(), MatCount = 12, Prereq = StNet4 },
			new ResearchTechNode { Id = StNet6, Name = "Wide Mesh", X = 1020, Y = 1300, Color = new Color(100, 180, 255), Desc = "Network range 25 -> 30 tiles.", Coins = Item.buyPrice(gold: 28), Mat = Adv(), MatCount = 12, Prereq = StNet5 },
			new ResearchTechNode { Id = StNet7, Name = "Overdrive Grid", X = 1190, Y = 1300, Color = new Color(100, 180, 255), Desc = "Toggle: meshed stations burn this engine's charge for doubled mesh output.", Coins = Item.buyPrice(gold: 35), Mat = Adv(), MatCount = 15, Prereq = StNet6 },
			new ResearchTechNode { Id = Aegis1, Name = "Aegis Mesh", X = 170, Y = 1600, Color = new Color(120, 220, 190), Desc = "Defensive traps within 15 tiles gain Aegis cover (Sentry/Link/Tripwire only).", Coins = Item.buyPrice(gold: 10), Mat = Parts(), MatCount = 8, ReqCount = 4 },
			new ResearchTechNode { Id = Aegis2, Name = "Aegis Reach II", X = 340, Y = 1600, Color = new Color(120, 220, 190), Desc = "Aegis range 15 -> 30 tiles (defensive only).", Coins = Item.buyPrice(gold: 14), Mat = Adv(), MatCount = 6, Prereq = Aegis1 },
			new ResearchTechNode { Id = Aegis3, Name = "Aegis Reach III", X = 510, Y = 1600, Color = new Color(120, 220, 190), Desc = "Aegis range 30 -> 45 tiles (defensive only).", Coins = Item.buyPrice(gold: 18), Mat = Adv(), MatCount = 8, Prereq = Aegis2 },
			new ResearchTechNode { Id = Aegis4, Name = "Aegis Reach IV", X = 680, Y = 1600, Color = new Color(120, 220, 190), Desc = "Aegis range 45 -> 60 tiles (defensive only).", Coins = Item.buyPrice(gold: 22), Mat = Adv(), MatCount = 10, Prereq = Aegis3 },
			new ResearchTechNode { Id = Aegis5, Name = "Aegis Reach V", X = 850, Y = 1600, Color = new Color(120, 220, 190), Desc = "Aegis range 60 -> 75 tiles (defensive only).", Coins = Item.buyPrice(gold: 25), Mat = Adv(), MatCount = 12, Prereq = Aegis4 },
			new ResearchTechNode { Id = Aegis6, Name = "Wide Aegis", X = 1020, Y = 1600, Color = new Color(120, 220, 190), Desc = "Aegis range 75 -> 90 tiles (defensive only).", Coins = Item.buyPrice(gold: 28), Mat = Adv(), MatCount = 12, Prereq = Aegis5 },
			new ResearchTechNode { Id = AegisFar, Name = "Far Aegis", X = 1190, Y = 1600, Color = new Color(120, 220, 190), Desc = "Aegis-covered defensive traps gain TRIPLE detection/pair range (cap 60/90).", Coins = Item.buyPrice(gold: 32), Mat = Adv(), MatCount = 12, Prereq = Aegis6 },
			new ResearchTechNode { Id = Aegis7, Name = "Aegis Overdrive", X = 1360, Y = 1600, Color = new Color(120, 220, 190), Desc = "Toggle: Aegis-covered traps burn this engine's charge for doubled output.", Coins = Item.buyPrice(gold: 35), Mat = Adv(), MatCount = 15, Prereq = AegisFar },
			new ResearchTechNode { Id = SaveTech, Name = "Tech Saving", X = 170, Y = 1400, Color = new Color(110, 200, 160), Desc = "Breaking saves researched tech to your backup.", Coins = Item.buyPrice(gold: 12), Mat = Parts(), MatCount = 10, ReqCount = 4 },
			new ResearchTechNode { Id = SaveFull, Name = "Full Backup", X = 340, Y = 1400, Color = new Color(110, 200, 160), Desc = "Backup also saves points, storage, queue and jobs.", Coins = Item.buyPrice(gold: 18), Mat = Adv(), MatCount = 10, Prereq = SaveTech },
			new ResearchTechNode { Id = SaveVault, Name = "Legacy Vault", X = 510, Y = 1400, Color = new Color(110, 200, 160), Desc = "Backup also saves charge and full config.", Coins = Item.buyPrice(gold: 22), Mat = Adv(), MatCount = 12, Prereq = SaveFull },
			new ResearchTechNode { Id = ChargeCoils, Name = "Kinetic Coils", X = 170, Y = 1500, Color = new Color(255, 170, 60), Desc = "Yoyo hits charge the engine. Without this, hits do nothing.", Coins = Item.buyPrice(gold: 3), Mat = Scraps(), MatCount = 10 },
			new ResearchTechNode { Id = ChargeWind, Name = "Fast Windings", X = 340, Y = 1500, Color = new Color(255, 170, 60), Desc = "+1 charge per hit.", Coins = Item.buyPrice(gold: 6), Mat = Parts(), MatCount = 8, Prereq = ChargeCoils },
			new ResearchTechNode { Id = ChargeCells, Name = "Deep Cells", X = 510, Y = 1500, Color = new Color(255, 170, 60), Desc = "Max charge 100 -> 150.", Coins = Item.buyPrice(gold: 10), Mat = Parts(), MatCount = 10, Prereq = ChargeWind },
			new ResearchTechNode { Id = ChargeInsul, Name = "Insulation", X = 680, Y = 1500, Color = new Color(255, 170, 60), Desc = "Idle charge decay twice as slow.", Coins = Item.buyPrice(gold: 14), Mat = Adv(), MatCount = 6, Prereq = ChargeCells },
			new ResearchTechNode { Id = ChargeCond, Name = "Conductors", X = 850, Y = 1500, Color = new Color(255, 170, 60), Desc = "+1 more charge per hit.", Coins = Item.buyPrice(gold: 18), Mat = Adv(), MatCount = 8, Prereq = ChargeInsul },
			new ResearchTechNode { Id = ChargeTrick, Name = "Trickle Charge", X = 1020, Y = 1500, Color = new Color(255, 170, 60), Desc = "Engine sips +1 charge per 10s up to 25, no hits needed.", Coins = Item.buyPrice(gold: 22), Mat = Adv(), MatCount = 10, Prereq = ChargeCond },
		};

		// Lane bands for the canvas (label + center Y). Purely visual.
		public static readonly (string label, int rowY, Color tint)[] Lanes = new[] {
			("IMPACT", -200, new Color(230, 120, 100)),
			("FLOW", -100, new Color(170, 140, 255)),
			("BURST", 0, new Color(230, 160, 90)),
			("TUNE", 100, new Color(120, 200, 230)),
			("LAUNCH", 200, new Color(150, 230, 120)),
			("SPEED", 300, new Color(100, 220, 200)),
			("TRADE", 400, new Color(220, 200, 140)),
			("BAYS", 500, new Color(220, 160, 190)),
			("AUTO", 600, new Color(130, 150, 190)),
			("YIELD", 700, new Color(235, 190, 90)),
			("NET", 800, new Color(200, 210, 225)),
			("EVENT", 900, new Color(170, 200, 90)),
			("FIELD", 1000, new Color(150, 110, 70)),
			("SALVAGE", 1100, new Color(190, 130, 80)),
			("TRIALS", 1200, new Color(140, 90, 200)),
			("STATION", 1300, new Color(100, 180, 255)),
			("SAVE", 1400, new Color(110, 200, 160)),
			("CHARGE", 1500, new Color(255, 170, 60)),
			("AEGIS", 1600, new Color(120, 220, 190)),
		};

		// Researched AND switched on (Loadout tab can disable owned tech).
		public static bool TechOn(ResearchData data, string id)
		{
			if (data == null || !data.HasTech(id))
				return false;
			return data.OffTech == null || !data.OffTech.Contains(id);
		}

		public static void ToggleOff(Point tile, string id)
		{
			if (!ResearchEngineSystem.IsTileStillEngine(tile))
				return;
			var data = ResearchEngineSystem.GetOrCreate(tile);
			if (!data.HasTech(id))
				return;
			data.OffTech ??= new System.Collections.Generic.List<string>();
			if (data.OffTech.Contains(id))
				data.OffTech.Remove(id);
			else
				data.OffTech.Add(id);
			ResearchEngineSystem.SyncWorld();
		}

		public static ResearchTechNode ById(string id)
		{
			foreach (var n in All) {
				if (n.Id == id)
					return n;
			}
			return null;
		}

		public static bool IsNode(string id) => ById(id) != null;

		public static bool PrereqMet(ResearchData data, ResearchTechNode node, out string why)
		{
			why = "";
			if (data == null) {
				why = "No data.";
				return false;
			}
			if (data.HasTech(node.Id)) {
				why = "Already researched.";
				return false;
			}
			if (!string.IsNullOrEmpty(node.Prereq) && !data.HasTech(node.Prereq)) {
				var pre = ById(node.Prereq);
				why = $"Requires {pre?.Name ?? node.Prereq}.";
				return false;
			}
			if (node.ReqCount > 0 && data.TechCount < node.ReqCount) {
				why = $"Requires {node.ReqCount} researched techs (have {data.TechCount}).";
				return false;
			}
			return true;
		}

		public static string CostText(ResearchData data, ResearchTechNode node)
		{
			int secs = ResearchSecs(node);
			int pts = data != null && data.TechCount == 0 ? 0 : ResearchPoints.AwardFor(node);
			string ptsText = pts > 0 ? $" + {pts} pts" : "";
			if (node.CostsYoyo)
				return $"{Content.UI.YoyoNPC.YoyoNPCPanel.FormatCoins(EffectiveCoins(data, node))} + HELD yoyo (consumed!){ptsText} + {secs}s";
			Item tmp = new Item();
			tmp.SetDefaults(node.Mat);
			return $"{Content.UI.YoyoNPC.YoyoNPCPanel.FormatCoins(EffectiveCoins(data, node))} + {EffectiveMats(data, node)}x {tmp.Name}{ptsText} + {secs}s";
		}

		// Research time in seconds, tiered by price (doubled for weight:
		// big choices should feel big). Runs even with the UI closed;
		// the Loadout tab shows the live countdown.
		public static int ResearchSecs(ResearchTechNode node)
		{
			if (node == null)
				return 120;
			if (node.CostsYoyo)
				return 600;
			if (node.Coins >= Item.buyPrice(gold: 20))
				return 360;
			if (node.Coins >= Item.buyPrice(gold: 12))
				return 240;
			if (node.Coins >= Item.buyPrice(gold: 8))
				return 120;
			return 60;
		}

		// Concurrent research slots: 1 base, +1 per Bay tech (max 3).
		public static int ResearchSlots(ResearchData data)
		{
			int slots = 1;
			if (TechOn(data, ParBench2))
				slots++;
			if (TechOn(data, ParBench3))
				slots++;
			return slots;
		}

		// Queue slots (#4): +1 per owned automation tech (max 6). Queued
		// research is paid upfront and auto-starts when a bay frees.
		public static int QueueSlots(ResearchData data)
		{
			int slots = 0;
			if (TechOn(data, AutoQ1))
				slots++;
			if (TechOn(data, AutoQ2))
				slots++;
			if (TechOn(data, AutoQ3))
				slots++;
			if (TechOn(data, AutoQ4))
				slots++;
			if (TechOn(data, AutoQ5))
				slots++;
			if (TechOn(data, AutoQ6))
				slots++;
			return slots;
		}

		public static int QueueCount(ResearchData data) => data?.Queue?.Count ?? 0;

		public static int QueuePosition(ResearchData data, string id)
		{
			if (data?.Queue == null || string.IsNullOrEmpty(id))
				return -1;
			return data.Queue.IndexOf(id);
		}

		public static ResearchJob FindJob(ResearchData data, string id)
		{
			if (data?.Research == null)
				return null;
			foreach (var job in data.Research) {
				if (job.Id == id)
					return job;
			}
			return null;
		}

		public static float ResearchRate(ResearchData data)
		{
			float rate = 1f;
			if (TechOn(data, ResearchSpd1))
				rate += 0.25f;
			if (TechOn(data, ResearchSpd2))
				rate += 0.25f;
			rate *= ScalingMult(data);
			return rate;
		}

		// Research scaling (#14): every researched tech slightly lifts
		// research aspects. Very low on purpose: +0.2% per tech. Counts
		// owned tech (toggles keep what you have; respec reduces it).
		public static float ScalingMult(ResearchData data)
		{
			return 1f + 0.002f * (data?.TechCount ?? 0);
		}

		public static int EffectiveCoins(ResearchData data, ResearchTechNode node)
		{
			if (TechOn(data, DiscCoin))
				return (int)(node.Coins * 0.8f);
			return node.Coins;
		}

		public static int EffectiveMats(ResearchData data, ResearchTechNode node)
		{
			float mult = 1f;
			if (TechOn(data, DiscMat1))
				mult -= 0.15f;
			if (TechOn(data, DiscMat2))
				mult -= 0.15f;
			return System.Math.Max(1, (int)(node.MatCount * mult));
		}

		// Client-side purchase: pays locally (discounted), starts research
		// optimistically, syncs the start. Research ticks down everywhere,
		// UI open or not.
		public static void BuyNode(Player player, Point tile, string id)
		{
			var node = ById(id);
			if (node == null)
				return;
			if (!ResearchEngineSystem.IsTileStillEngine(tile)) {
				ResearchEngineUISystem.SetMessage("Block is gone.");
				return;
			}
			var data = ResearchEngineSystem.GetOrCreate(tile);
			// Clicking a queued node cancels that job (full refund).
			if (FindJob(data, id) != null) {
				CancelResearch(player, tile, id);
				return;
			}
			// Clicking a queued-for-later node dequeues it (full refund).
			if (QueuePosition(data, id) >= 0) {
				CancelResearch(player, tile, id);
				return;
			}
			if (!PrereqMet(data, node, out string why)) {
				ResearchEngineUISystem.SetMessage(why);
				Terraria.Audio.SoundEngine.PlaySound(SoundID.MenuTick, player.Center);
				return;
			}
			if (data.Research != null && data.Research.Count >= ResearchSlots(data) && QueueCount(data) >= QueueSlots(data)) {
				ResearchEngineUISystem.SetMessage(QueueSlots(data) <= 0 ? "Research bays busy. Research automation (Tech tab) to queue." : "Bays and queue full. Cancel something first.");
				Terraria.Audio.SoundEngine.PlaySound(SoundID.MenuTick, player.Center);
				return;
			}
			int costCoins = EffectiveCoins(data, node);
			int costMats = EffectiveMats(data, node);
			// Points are part of every research price (#2): scaled by the
			// research itself, free for the very first tech so the engine
			// can bootstrap (trickle needs 1 tech to start).
			int costPts = data.TechCount == 0 ? 0 : ResearchPoints.AwardFor(node);
			if (!player.CanAfford(costCoins)) {
				ResearchEngineUISystem.SetMessage($"Needs {Content.UI.YoyoNPC.YoyoNPCPanel.FormatCoins(costCoins)}.");
				Terraria.Audio.SoundEngine.PlaySound(SoundID.MenuTick, player.Center);
				return;
			}
			if (node.CostsYoyo) {
				Item held = player.HeldItem;
				if (held == null || held.IsAir || !Content.NPCs.Town.YoyoTownShared.IsYoyoShape(held)) {
					ResearchEngineUISystem.SetMessage("Hold a yoyo in your hand to sacrifice it.");
					Terraria.Audio.SoundEngine.PlaySound(SoundID.MenuTick, player.Center);
					return;
				}
			}
			else {
				int have = Content.NPCs.Town.YoyoTownShared.CountInInventory(player, node.Mat);
				if (have < costMats) {
					Item tmp = new Item();
					tmp.SetDefaults(node.Mat);
					ResearchEngineUISystem.SetMessage($"Needs {costMats}x {tmp.Name} (have {have}).");
					Terraria.Audio.SoundEngine.PlaySound(SoundID.MenuTick, player.Center);
					return;
				}
			}
			if (data.Points < costPts) {
				ResearchEngineUISystem.SetMessage($"Needs {costPts} research points (banked {data.Points}). Earn them in the Research tab.");
				Terraria.Audio.SoundEngine.PlaySound(SoundID.MenuTick, player.Center);
				return;
			}
			// Pay. Deduct only on success (all gates passed above).
			player.PayCurrency(costCoins);
			if (costPts > 0) {
				data.Points -= costPts;
				ResearchEngineSystem.SyncWorld();
			}
			if (node.CostsYoyo) {
				Item held = player.HeldItem;
				if (held.stack > 1)
					held.stack--;
				else
					held.TurnToAir();
			}
			else {
				Content.NPCs.Town.YoyoTownShared.TryConsumeFromInventory(player, node.Mat, costMats);
			}
			StartOrQueue(tile, id);
			if (Main.netMode == NetmodeID.MultiplayerClient)
				PlaceableNet.SendResearchTech(tile, id, costPts);
			Terraria.Audio.SoundEngine.PlaySound(SoundID.CoinPickup, player.Center);
			int qp = QueuePosition(ResearchEngineSystem.GetOrCreate(tile), id);
			if (qp >= 0)
				ResearchEngineUISystem.SetMessage($"Queued {node.Name} (#{qp + 1}). Paid upfront — starts when a bay frees.");
			else
				ResearchEngineUISystem.SetMessage($"Researching {node.Name} ({ResearchSecs(node)}s)...");
		}

		// Server-authoritative research start (bounds + prereq + free bay).
		public static void StartResearch(Point tile, string id)
		{
			var node = ById(id);
			if (node == null || !ResearchEngineSystem.IsTileStillEngine(tile))
				return;
			var data = ResearchEngineSystem.GetOrCreate(tile);
			if (!PrereqMet(data, node, out _))
				return;
			if (FindJob(data, id) != null)
				return;
			data.Research ??= new System.Collections.Generic.List<ResearchJob>();
			if (data.Research.Count >= ResearchSlots(data))
				return;
			data.Research.Add(new ResearchJob { Id = id, Left = ResearchSecs(node) * 60, Acc = 0f });
			ResearchEngineSystem.SyncWorld();
		}

		// Shared start-or-queue (#4): a free bay starts now, otherwise the
		// job queues when automation allows. Returns "job", "queue" or "".
		public static string StartOrQueue(Point tile, string id)
		{
			var node = ById(id);
			if (node == null || !ResearchEngineSystem.IsTileStillEngine(tile))
				return "";
			var data = ResearchEngineSystem.GetOrCreate(tile);
			if (data.HasTech(id) || FindJob(data, id) != null || QueuePosition(data, id) >= 0)
				return "";
			if (!PrereqMet(data, node, out _))
				return "";
			data.Research ??= new System.Collections.Generic.List<ResearchJob>();
			data.Queue ??= new System.Collections.Generic.List<string>();
			if (data.Research.Count < ResearchSlots(data)) {
				data.Research.Add(new ResearchJob { Id = id, Left = ResearchSecs(node) * 60, Acc = 0f });
				ResearchEngineSystem.SyncWorld();
				return "job";
			}
			if (data.Queue.Count < QueueSlots(data)) {
				data.Queue.Add(id);
				ResearchEngineSystem.SyncWorld();
				return "queue";
			}
			return "";
		}

		// Promotes queued research while bays are free (called from the
		// world tick). Stale entries (unknown node, already owned) drop
		// silently; respec already refunds the queue before clearing tech.
		// Returns true when anything moved.
		public static bool PromoteQueue(Point tile, ResearchData data)
		{
			if (data == null || !ResearchEngineSystem.IsTileStillEngine(tile))
				return false;
			bool moved = false;
			data.Research ??= new System.Collections.Generic.List<ResearchJob>();
			data.Queue ??= new System.Collections.Generic.List<string>();
			while (data.Research.Count < ResearchSlots(data) && data.Queue.Count > 0) {
				string qid = data.Queue[0];
				data.Queue.RemoveAt(0);
				var qn = ById(qid);
				if (qn == null || data.HasTech(qid) || FindJob(data, qid) != null)
					continue;
				if (!PrereqMet(data, qn, out _))
					continue;
				data.Research.Add(new ResearchJob { Id = qid, Left = ResearchSecs(qn) * 60, Acc = 0f });
				moved = true;
			}
			if (moved)
				ResearchEngineSystem.SyncWorld();
			return moved;
		}

		// Server-side removal without refund (payment happened client-side).
		public static void DropJobOrQueued(Point tile, string id)
		{
			if (!ResearchEngineSystem.IsTileStillEngine(tile))
				return;
			var data = ResearchEngineSystem.GetOrCreate(tile);
			var job = FindJob(data, id);
			if (job != null) {
				data.Research.Remove(job);
				ResearchEngineSystem.SyncWorld();
				return;
			}
			if (data.Queue != null && data.Queue.Contains(id)) {
				data.Queue.Remove(id);
				ResearchEngineSystem.SyncWorld();
			}
		}

		// Full refund of what was paid for one node (coins + mats +
		// points, never the sacrificed yoyo). Shared by active-job and
		// queued cancels. Points mirror the buy rule (first tech free).
		public static void Refund(Player player, ResearchData data, ResearchTechNode node)
		{
			var source = player.GetSource_Misc("ResearchCancel");
			int rc = EffectiveCoins(data, node);
			while (rc >= 1000000) {
				player.QuickSpawnItem(source, ItemID.PlatinumCoin, 1);
				rc -= 1000000;
			}
			while (rc >= 10000) {
				player.QuickSpawnItem(source, ItemID.GoldCoin, 1);
				rc -= 10000;
			}
			while (rc >= 100) {
				player.QuickSpawnItem(source, ItemID.SilverCoin, 1);
				rc -= 100;
			}
			while (rc >= 1) {
				player.QuickSpawnItem(source, ItemID.CopperCoin, 1);
				rc -= 1;
			}
			if (!node.CostsYoyo) {
				int rm = EffectiveMats(data, node);
				if (rm > 0)
					player.QuickSpawnItem(source, node.Mat, rm);
			}
			if (data != null && data.TechCount > 0)
				ResearchPoints.Add(data, ResearchPoints.AwardFor(node));
		}

		// Cancel one active job or queued entry with a full refund.
		public static void CancelResearch(Player player, Point tile, string id)
		{
			if (!ResearchEngineSystem.IsTileStillEngine(tile)) {
				ResearchEngineUISystem.SetMessage("Block is gone.");
				return;
			}
			var data = ResearchEngineSystem.GetOrCreate(tile);
			var job = FindJob(data, id);
			var node = ById(id);
			if (node == null) {
				ResearchEngineUISystem.SetMessage("Nothing researching.");
				return;
			}
			if (job == null) {
				// Queued (paid upfront) -> dequeue with the same refund.
				if (data.Queue == null || !data.Queue.Contains(id)) {
					ResearchEngineUISystem.SetMessage("Nothing researching.");
					return;
				}
				Refund(player, data, node);
				if (node.CostsYoyo)
					ResearchEngineUISystem.SetMessage("Dequeued. The sacrificed yoyo is already gone — coins refunded.");
				else
					ResearchEngineUISystem.SetMessage($"Dequeued {node.Name}, fully refunded.");
				data.Queue.Remove(id);
				ResearchEngineSystem.SyncWorld();
				if (Main.netMode == NetmodeID.MultiplayerClient)
					PlaceableNet.SendResearchCancel(tile, id);
				Terraria.Audio.SoundEngine.PlaySound(SoundID.MenuTick, player.Center);
				return;
			}
			Refund(player, data, node);
			if (node.CostsYoyo) {
				ResearchEngineUISystem.SetMessage("Cancelled. The sacrificed yoyo is already gone — coins refunded.");
			}
			else {
				ResearchEngineUISystem.SetMessage($"Cancelled {node.Name}, fully refunded.");
			}
			data.Research?.Remove(job);
			ResearchEngineSystem.SyncWorld();
			if (Main.netMode == NetmodeID.MultiplayerClient)
				PlaceableNet.SendResearchCancel(tile, id);
			Terraria.Audio.SoundEngine.PlaySound(SoundID.MenuTick, player.Center);
		}

		// Server-authoritative unlock (bounds + prereq checked, tile valid).
		public static void Unlock(Point tile, string id)
		{
			var node = ById(id);
			if (node == null || !ResearchEngineSystem.IsTileStillEngine(tile))
				return;
			var data = ResearchEngineSystem.GetOrCreate(tile);
			if (!PrereqMet(data, node, out _))
				return;
			data.Tech.Add(id);
			ResearchEngineSystem.SyncWorld();
		}

		// Respec: refunds every researched node at 70% (coins + mats, never
		// the sacrificed yoyo) and clears the tree. Client pays itself back
		// locally like a downgrade; the server clears authoritatively.
		public static void Respec(Player player, Point tile)
		{
			if (!ResearchEngineSystem.IsTileStillEngine(tile)) {
				ResearchEngineUISystem.SetMessage("Block is gone.");
				return;
			}
			var data = ResearchEngineSystem.GetOrCreate(tile);
			if (data.TechCount == 0 && (data.Research == null || data.Research.Count == 0) && (data.Queue == null || data.Queue.Count == 0)) {
				ResearchEngineUISystem.SetMessage("Nothing to respec.");
				return;
			}
			// Queued research cancels first at full effective price.
			foreach (var job in new System.Collections.Generic.List<ResearchJob>(data.Research ?? new System.Collections.Generic.List<ResearchJob>()))
				CancelResearch(player, tile, job.Id);
			foreach (var qid in new System.Collections.Generic.List<string>(data.Queue ?? new System.Collections.Generic.List<string>()))
				CancelResearch(player, tile, qid);
			var source = player.GetSource_Misc("ResearchRespec");
			foreach (string id in new System.Collections.Generic.List<string>(data.Tech)) {
				var node = ById(id);
				if (node == null)
					continue;
				// 70% of what was actually paid (discounted price, like cancels).
				int rc = (int)(EffectiveCoins(data, node) * 0.7f);
				while (rc >= 1000000) {
					player.QuickSpawnItem(source, ItemID.PlatinumCoin, 1);
					rc -= 1000000;
				}
				while (rc >= 10000) {
					player.QuickSpawnItem(source, ItemID.GoldCoin, 1);
					rc -= 10000;
				}
				while (rc >= 100) {
					player.QuickSpawnItem(source, ItemID.SilverCoin, 1);
					rc -= 100;
				}
				while (rc >= 1) {
					player.QuickSpawnItem(source, ItemID.CopperCoin, 1);
					rc -= 1;
				}
				if (!node.CostsYoyo) {
					int rm = (int)(EffectiveMats(data, node) * 0.7f);
					if (rm > 0)
						player.QuickSpawnItem(source, node.Mat, rm);
				}
				ResearchPoints.Add(data, (int)(ResearchPoints.AwardFor(node) * 0.7f));
			}
			ClearAll(tile);
			if (Main.netMode == NetmodeID.MultiplayerClient)
				PlaceableNet.SendResearchRespec(tile);
			Terraria.Audio.SoundEngine.PlaySound(SoundID.CoinPickup, player.Center);
			ResearchEngineUISystem.SetMessage("Respecced at 70% each. The yoyo stays sacrificed.");
		}

		public static void ClearAll(Point tile)
		{
			if (!ResearchEngineSystem.IsTileStillEngine(tile))
				return;
			var data = ResearchEngineSystem.GetOrCreate(tile);
			data.Tech.Clear();
			data.OffTech?.Clear();
			// A toggled draw without its node must not linger on.
			data.OverdriveOn = false;
			// A style whose tech is gone falls back to One Hit.
			if (data.Style == 1 && !data.HasTech(StyleOver))
				data.Style = 0;
			if (data.Style == 2 && !data.HasTech(StyleSurge))
				data.Style = 0;
			// Respec calms the storm: no orphaned event conditions (#8).
			data.ActiveEventId = "";
			data.EventLeft = 0;
			data.EventIn = 0;
			ResearchEngineSystem.SyncWorld();
		}
	}
}

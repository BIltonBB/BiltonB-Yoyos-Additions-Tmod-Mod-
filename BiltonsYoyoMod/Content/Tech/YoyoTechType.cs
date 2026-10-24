namespace BiltonsYoyoMod.Content.Tech
{
	/// <summary>
	/// Which yoyo-tech conversion is active. Only one may be active at a time.
	/// None = vanilla cursor yoyo behaviour.
	/// </summary>
	public enum YoyoTechType : byte
	{
		None = 0,
		Throwable = 1,
		Swing = 2,
		Magical = 3,
		Summon = 4,
	}

	public static class YoyoTechTypeExt
	{
		public static float DamageMultiplier(this YoyoTechType tech)
		{
			return tech switch {
				YoyoTechType.Throwable => 1.5f,
				YoyoTechType.Swing => 2.0f,
				YoyoTechType.Magical => 0.5f,
				YoyoTechType.Summon => 0.6f,
				_ => 1f,
			};
		}

		public static string TechName(this YoyoTechType tech)
		{
			return tech switch {
				YoyoTechType.Throwable => "Throwable Yoyo Tech",
				YoyoTechType.Swing => "Swing Yoyo Tech",
				YoyoTechType.Magical => "Magical Yoyo Tech",
				YoyoTechType.Summon => "Summon Yoyo Tech",
				_ => "No Tech",
			};
		}
	}
}

using System.Collections.Generic;

namespace BiltonsYoyoMod.Content.Customization
{
	// Calibration evaluates geometry: direction, timing, impact, distance, trajectory structure.
	// Normal Calibration (0-9) and Extended Calibration (100-107) share one slot.
	public class YoyoCalibrationDef
	{
		public int Id;
		public string Name;
		public string Description;
		public bool Extended;
		public string Tier => Extended ? "Extended Calibration" : "Calibration";
	}

	public static class YoyoCalibrationDefs
	{
		public const int ExtendedOffset = 100;

		public static readonly List<YoyoCalibrationDef> All = new()
		{
			new YoyoCalibrationDef { Id = 0, Name = "Collision Theory", Description = "Fly straight AT enemies for +25% crush hits. Sideswipe grazes still get +10%." },
			new YoyoCalibrationDef { Id = 1, Name = "Crosscut", Description = "Hit an enemy, then hit it again from a clearly different side for +45%." },
			new YoyoCalibrationDef { Id = 2, Name = "Offbeat", Description = "Land hits with a short pause between them to build rhythm stages. Window keeps shrinking." },
			new YoyoCalibrationDef { Id = 3, Name = "Precession", Description = "Loop the yoyo fully around yourself to tilt the next fling sideways with bonus." },
			new YoyoCalibrationDef { Id = 4, Name = "Parallax", Description = "Run ~10 blocks from where you started to empower your next hit (+30%)." },
			new YoyoCalibrationDef { Id = 5, Name = "Reacquisition", Description = "Go 5 seconds with no hit, then bounce and land one: the comeback hit is empowered." },
			new YoyoCalibrationDef { Id = 6, Name = "Capacitor", Description = "Hits bank charge. Long straight runs cash out big; curled runs add a speed burst." },
			new YoyoCalibrationDef { Id = 7, Name = "Threadline", Description = "Hit 4+ different spots or enemies in a row to fire a bonus strike along the thread." },
			new YoyoCalibrationDef { Id = 8, Name = "Deadband", Description = "About every 5 seconds a brief window makes hits +50%. Missing it costs nothing." },
			new YoyoCalibrationDef { Id = 9, Name = "Differential", Description = "Keep changing flight lines. Repeating the same line stalls the bonus." },
			new YoyoCalibrationDef { Id = 100, Extended = true, Name = "Redline", Description = "Fast varied flying builds pressure, then vents speed and damage. Overholding backfires." },
			new YoyoCalibrationDef { Id = 101, Extended = true, Name = "Closed Orbit", Description = "It remembers each route's start. Fly back through that spot to seal a bonus hit." },
			new YoyoCalibrationDef { Id = 102, Extended = true, Name = "Triangulation", Description = "Hit one enemy from 3 clearly different sides to trigger a convergence strike." },
			new YoyoCalibrationDef { Id = 103, Extended = true, Name = "Impulse Bridge", Description = "Motion banks energy that quietly magnifies your hits while the tank is full." },
			new YoyoCalibrationDef { Id = 104, Extended = true, Name = "Harmonic Lock", Description = "Land rebounds with even spacing. Five in a row pays a bonus hit." },
			new YoyoCalibrationDef { Id = 105, Extended = true, Name = "Escape Geometry", Description = "Bouncing right after a miss adapts: the recovery hit lands harder." },
			new YoyoCalibrationDef { Id = 106, Extended = true, Name = "Overpressure", Description = "Pushes speed and damage past the limit while active. Release before it backfires." },
			new YoyoCalibrationDef { Id = 107, Extended = true, Name = "Fault Recovery", Description = "A failed line opens a window; a corrective rebound salvages bonus charge." },
		};

		public static YoyoCalibrationDef ById(int id)
		{
			foreach (var d in All) {
				if (d.Id == id)
					return d;
			}
			return null;
		}

		public static bool IsExtendedId(int id) => id >= ExtendedOffset;
	}
}

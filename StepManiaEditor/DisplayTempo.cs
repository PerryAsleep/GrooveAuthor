using Fumen.Converters;
using Fumen;
using static Fumen.Converters.SMCommon;

namespace StepManiaEditor
{
	internal enum DisplayTempoMode
	{
		Random,
		Specified,
		Actual
	}

	internal sealed class DisplayTempo
	{
		public DisplayTempoMode Mode;
		public double SpecifiedTempoMin;
		public double SpecifiedTempoMax;

		// Not serialized. Used for UI controls to avoid having to enter both a min and a max
		// when just wanted one tempo.
		public bool ShouldAllowEditsOfMax = true;

		public DisplayTempo()
		{
			Mode = DisplayTempoMode.Actual;
			SpecifiedTempoMin = 0.0;
			SpecifiedTempoMax = 0.0;
		}

		public DisplayTempo(DisplayTempoMode mode, double min, double max)
		{
			Mode = mode;
			SpecifiedTempoMin = min;
			SpecifiedTempoMax = max;
			ShouldAllowEditsOfMax = !SpecifiedTempoMin.DoubleEquals(SpecifiedTempoMax);
		}

		public DisplayTempo(DisplayTempo other)
		{
			Mode = other.Mode;
			SpecifiedTempoMin = other.SpecifiedTempoMin;
			SpecifiedTempoMax = other.SpecifiedTempoMax;
			ShouldAllowEditsOfMax = other.ShouldAllowEditsOfMax;
		}

		public void FromString(string displayTempoString)
		{
			Mode = DisplayTempoMode.Actual;
			SpecifiedTempoMin = 0.0;
			SpecifiedTempoMax = 0.0;

			if (string.IsNullOrEmpty(displayTempoString))
				return;

			var parsed = false;
			if (displayTempoString == "*")
			{
				parsed = true;
				Mode = DisplayTempoMode.Random;
			}
			else
			{
				var parts = displayTempoString.Split(MSDFile.ParamMarker);
				if (parts.Length == 1)
				{
					if (double.TryParse(parts[0], out SpecifiedTempoMin))
					{
						parsed = true;
						SpecifiedTempoMax = SpecifiedTempoMin;
						Mode = DisplayTempoMode.Specified;
						ShouldAllowEditsOfMax = false;
					}
				}
				else if (parts.Length == 2)
				{
					if (double.TryParse(parts[0], out SpecifiedTempoMin) && double.TryParse(parts[1], out SpecifiedTempoMax))
					{
						parsed = true;
						Mode = DisplayTempoMode.Specified;
						ShouldAllowEditsOfMax = !SpecifiedTempoMin.DoubleEquals(SpecifiedTempoMax);
					}
				}
			}

			if (!parsed)
			{
				Logger.Warn($"Failed to parse {TagDisplayBPM} value: '{displayTempoString}'.");
			}
		}

		public override string ToString()
		{
			switch (Mode)
			{
				case DisplayTempoMode.Random:
					return "*";
				case DisplayTempoMode.Specified:
					if (!SpecifiedTempoMin.DoubleEquals(SpecifiedTempoMax))
					{
						var min = SpecifiedTempoMin.ToString(SMDoubleFormat);
						var max = SpecifiedTempoMax.ToString(SMDoubleFormat);
						return $"{min}:{max}";
					}
					return SpecifiedTempoMin.ToString(SMDoubleFormat);
				case DisplayTempoMode.Actual:
					return "";
			}
			return "";
		}
	}
}

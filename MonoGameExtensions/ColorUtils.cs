using Microsoft.Xna.Framework;

namespace MonoGameExtensions;

public class ColorUtils
{
	public static ushort ToBGR565(Color c)
	{
		return Fumen.ColorUtils.ToBGR565((float)c.R / byte.MaxValue, (float)c.G / byte.MaxValue, (float)c.B / byte.MaxValue);
	}

	public static Color Interpolate(uint startColor, uint endColor, float percent)
	{
		return new Color(Fumen.ColorUtils.ColorRGBAInterpolate(startColor, endColor, percent));
	}

	public static Color Interpolate(Color startColor, Color endColor, float percent)
	{
		return new Color(Fumen.ColorUtils.ColorRGBAInterpolate(startColor.PackedValue, endColor.PackedValue, percent));
	}
}

using Microsoft.Xna.Framework;

namespace MonoGameExtensions;

public class ColorUtils
{
	public static ushort ToBGR565(Color c)
	{
		return Fumen.ColorUtils.ToBGR565((float)c.R / byte.MaxValue, (float)c.G / byte.MaxValue, (float)c.B / byte.MaxValue);
	}
}

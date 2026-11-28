// 261127_code
// 261127_documentation

using Microsoft.Xna.Framework;

namespace EchoesOfTheShardveil.Desktop;

/// <summary>Shape tests used for hit testing.</summary>
internal static class Geometry
{
    /// <summary>Determines whether a point is on the circle inscribed in a square.</summary>
    /// <param name="bounds">The square around the circle.</param>
    /// <param name="point">The point to test.</param>
    /// <returns><see langword="true"/> if the point is inside the circle or on its rim.</returns>
    public static bool IsInCircle(Rectangle bounds, Point point)
    {
        var delta = point - bounds.Center;
        return delta.X * delta.X + delta.Y * delta.Y <= bounds.Width * bounds.Width / 4;
    }
}

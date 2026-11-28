// 261127_code
// 261127_documentation

using Microsoft.Xna.Framework;

namespace EchoesOfTheShardveil.Desktop;

/// <summary>Calculates the reveal and backlight for the existing wordmark pixels.</summary>
internal sealed class TitleBurn
{
    /// <summary>The length of the reveal in animation seconds.</summary>
    public const float Duration = 4.4f;

    private readonly (int Index, Color Color, float Ignition, float Fill)[] glyphPixels;

    /// <summary>Works out when each visible pixel of the wordmark catches light.</summary>
    /// <param name="width">The wordmark width in pixels.</param>
    /// <param name="height">The wordmark height in pixels.</param>
    /// <param name="wordmark">The wordmark pixels in row-major order, with premultiplied alpha.</param>
    public TitleBurn(int width, int height, Color[] wordmark)
    {
        Face = new Color[wordmark.Length];
        Light = new Color[wordmark.Length];
        var pixels = new List<(int, Color, float, float)>();
        for (var i = 0; i < wordmark.Length; i++)
        {
            var color = wordmark[i];
            if (color.A == 0)
            {
                continue;
            }

            var x = (i % width + 0.5f) / width;
            var y = (i / width + 0.5f) / height;
            var leftTear = Vector2.Distance(new(x * 1.4f, y * 0.7f), new(0.56f, 0.39f));
            var rightTear = Vector2.Distance(new(x * 1.4f, y * 0.7f), new(0.93f, 0.47f));
            var grain = MathF.Sin(x * 129f + y * 74f) * MathF.Sin(x * 83f - y * 137f);
            var wobble = 0.066f * MathF.Sin(x * 31f + y * 14f) + 0.027f * grain;
            var ignition = Math.Clamp(MathF.Min(leftTear, rightTear + 0.19f) * 1.43f + wobble, 0.03f, 0.78f);
            // Premultiplied white fill casts light in proportion to pixel coverage.
            var fill = color.R / 255f;
            pixels.Add((i, color, ignition, fill));
        }
        glyphPixels = [.. pixels];
    }

    /// <summary>Gets the wordmark as revealed by the last <see cref="Render"/>.</summary>
    public Color[] Face { get; }

    /// <summary>Gets the light leaking through the wordmark as of the last <see cref="Render"/>.</summary>
    public Color[] Light { get; }

    /// <summary>Updates <see cref="Face"/> and <see cref="Light"/> for a moment of the reveal.</summary>
    /// <param name="time">Animation seconds since the reveal began.</param>
    public void Render(float time)
    {
        var progress = Math.Clamp((time - 0.16f) / 2.75f, 0f, 1f);
        var afterglow = 1f - Ramp(1.65f, Duration, time);
        var shimmer = 0.90f + 0.06f * MathF.Sin(time * 16f) + 0.04f * MathF.Sin(time * 29f);
        foreach (var (index, original, ignition, fill) in glyphPixels)
        {
            var age = progress - ignition;
            var visible = Ramp(-0.025f, 0.055f, age);
            var edge = visible * (1f - Ramp(0.012f, 0.20f, age));
            var heat = Math.Clamp((edge * 0.77f + visible * 0.33f) * afterglow * shimmer * fill, 0f, 1f);
            Face[index] = Color.Lerp(original * visible, new Color(221, 243, 255), heat);

            var leakage = Math.Clamp((edge * 0.83f + visible * 0.44f) * afterglow * shimmer * fill, 0f, 1f);
            Light[index] = new Color(255, 255, 255, (int)MathF.Round(leakage * 255f));
        }
    }

    /// <summary>Eases from 0 to 1 as a value moves between two points.</summary>
    /// <param name="start">Where the result is 0.</param>
    /// <param name="end">Where the result is 1.</param>
    /// <param name="value">The value to evaluate.</param>
    /// <returns>A smooth step clamped to the range 0 to 1.</returns>
    public static float Ramp(float start, float end, float value) =>
        MathHelper.SmoothStep(0f, 1f, Math.Clamp((value - start) / (end - start), 0f, 1f));
}

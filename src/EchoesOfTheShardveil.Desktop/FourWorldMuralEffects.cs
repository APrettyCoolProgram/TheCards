// 261127_code
// 261127_documentation

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace EchoesOfTheShardveil.Desktop;

/// <summary>Animates details in the four-world menu painting in its original pixel coordinates.</summary>
internal sealed class FourWorldMuralEffects : IDisposable
{
    private static readonly Vector2[] Torches = [new(189, 280), new(320, 307)];
    private static readonly Point[] Windows =
    [
        new(1005, 244), new(1080, 199), new(1154, 280), new(1167, 277),
        new(1304, 153), new(1336, 204), new(1395, 142), new(1488, 160),
        new(1521, 84), new(1535, 114), new(1556, 123), new(1627, 198),
        new(1505, 689), new(1560, 680), new(1616, 667), new(1647, 688)
    ];
    private static readonly Vector2 LeftPalm = new(273, 531);
    private static readonly Vector2 RightPalm = new(471, 521);
    private static readonly (Vector2 Socket, Vector2 Mouth, Vector2 Palm)[] Necromancy =
    [
        (new(274, 619), new(273, 630), LeftPalm),
        (new(263, 670), new(261, 680), LeftPalm),
        (new(315, 733), new(317, 746), LeftPalm),
        (new(412, 710), new(403, 725), RightPalm)
    ];
    private static readonly Point[] Bolt =
        [new(1199, 117), new(1185, 126), new(1180, 147), new(1173, 156), new(1183, 173),
         new(1178, 193), new(1192, 209), new(1195, 225), new(1206, 238)];

    private readonly SpriteBatch spriteBatch;
    private readonly Texture2D pixel;
    private readonly Texture2D glow;
    private readonly Texture2D mural;
    private readonly (Texture2D Texture, Rectangle Bounds, float Motion)[] clothPatches;
    private readonly Texture2D groundFog;
    private readonly Texture2D torchFlame;

    /// <summary>Samples soft-edged patches of cloth and hair from the painting for small, localized motion.</summary>
    /// <param name="spriteBatch">The menu sprite batch.</param>
    /// <param name="pixel">The shared solid pixel used for thin lines and particles.</param>
    /// <param name="glow">The shared soft light texture.</param>
    /// <param name="mural">The four-world painting.</param>
    public FourWorldMuralEffects(SpriteBatch spriteBatch, Texture2D pixel, Texture2D glow, Texture2D mural)
    {
        this.spriteBatch = spriteBatch;
        this.pixel = pixel;
        this.glow = glow;
        this.mural = mural;
        clothPatches =
        [
            CreateClothPatch(new(494, 410, 34, 158), 4),
            CreateClothPatch(new(968, 475, 120, 172), 7),
            CreateClothPatch(new(1191, 550, 162, 144), 8),
            CreateClothPatch(new(1218, 327, 112, 141), 7)
        ];
        groundFog = EmbeddedAssets.LoadPremultipliedTexture(mural.GraphicsDevice, "MainUI-GroundFog.png");
        torchFlame = EmbeddedAssets.LoadPremultipliedTexture(mural.GraphicsDevice, "MainUI-TorchFlame.png");
    }

    /// <summary>Creates a soft light sprite shared by all mural effects.</summary>
    /// <param name="device">The graphics device.</param>
    /// <returns>The sprite, which the caller must dispose.</returns>
    public static Texture2D CreateGlow(GraphicsDevice device)
    {
        const int size = 64;
        var pixels = new Color[size * size];
        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                var offset = new Vector2(x + 0.5f, y + 0.5f) / (size / 2f) - Vector2.One;
                var falloff = MathF.Max(0, 1 - offset.LengthSquared());
                pixels[y * size + x] = new Color(1f, 1f, 1f, falloff * falloff);
            }
        }

        return TextureFactory.FromPixels(device, size, size, pixels);
    }

    /// <summary>Draws the animation layers over the painting.</summary>
    /// <param name="time">Seconds since the menu opened.</param>
    /// <param name="transform">The transform from painting pixels to the viewport.</param>
    public void Draw(float time, Matrix transform)
    {
        spriteBatch.Begin(samplerState: SamplerState.LinearClamp, transformMatrix: transform);
        for (var i = 0; i < clothPatches.Length; i++)
        {
            var (texture, bounds, motion) = clothPatches[i];
            for (var y = 0; y < bounds.Height; y += 4)
            {
                var height = Math.Min(4, bounds.Height - y);
                var shift = motion * MathF.Sin(time * 3f + i - y * 0.055f) * y / bounds.Height;
                spriteBatch.Draw(texture, new Vector2(bounds.X + shift, bounds.Y + y),
                    new Rectangle(0, y, bounds.Width, height), Color.White);
            }
        }

        DrawTorchFlames(time);
        DrawAtmosphere(time);
        DrawWindows(time);
        spriteBatch.End();

        spriteBatch.Begin(blendState: BlendState.Additive, samplerState: SamplerState.LinearClamp, transformMatrix: transform);
        DrawLights(time);
        DrawMagic(time);
        spriteBatch.End();
    }

    /// <summary>Draws the drifting fog, dark clouds and blown embers.</summary>
    /// <param name="time">Seconds since the menu opened.</param>
    private void DrawAtmosphere(float time)
    {
        for (var i = 0; i < 4; i++)
        {
            var age = LoopPhase(time * (0.056f + i * 0.009f) + i * 0.25f);
            var position = new Vector2(-32 + 382 * age, 807 + 19 * i);
            var size = new Vector2(217 + 24 * i, 35 + 8 * i);
            DrawFog(position, size, 0.51f * FadeInOut(age), i);
        }

        for (var i = 0; i < 3; i++)
        {
            var age = LoopPhase(time * (0.067f + i * 0.012f) + i / 3f);
            var position = new Vector2(233 + 157 * age, 770 + i * 18);
            DrawFog(position, new Vector2(165 + 29 * i, 29 + 9 * i), 0.62f * FadeInOut(age), i);
        }

        for (var i = 0; i < 3; i++)
        {
            var age = LoopPhase(time * 0.085f + i / 3f);
            DrawGlow(new(849 + 325 * age, 103 + i * 18), new(178, 22 + 6 * i),
                new Color(39, 49, 68), 0.56f * FadeInOut(age), -0.21f);
        }

        for (var i = 0; i < 13; i++)
        {
            var age = LoopPhase(time * (0.21f + i % 3 * 0.035f) + i * 0.618f);
            var position = new Vector2(823 + 426 * age, 836 + 7 * i - 18 * MathF.Sin(age * 12 + i));
            var rotation = age * MathHelper.TwoPi * 2 + i;
            DrawGlow(position, new(12, 2 + 4 * MathF.Abs(MathF.Sin(rotation))),
                new Color(169, 73, 38), 0.94f * FadeInOut(age), rotation);
        }
    }

    /// <summary>Draws one soft patch of ground fog.</summary>
    /// <param name="position">The center in painting pixels.</param>
    /// <param name="size">The size in painting pixels.</param>
    /// <param name="opacity">The opacity from 0 to 1.</param>
    /// <param name="layer">The patch index. Odd patches are mirrored.</param>
    private void DrawFog(Vector2 position, Vector2 size, float opacity, int layer)
    {
        var textureSize = new Vector2(groundFog.Width, groundFog.Height);
        spriteBatch.Draw(groundFog, position, null, Color.White * opacity, -0.045f, textureSize / 2,
            size / textureSize, layer % 2 == 0 ? SpriteEffects.None : SpriteEffects.FlipHorizontally, 0);
    }

    /// <summary>Briefly darkens each lit window in turn.</summary>
    /// <param name="time">Seconds since the menu opened.</param>
    private void DrawWindows(float time)
    {
        for (var i = 0; i < Windows.Length; i++)
        {
            var pane = Windows[i];
            var age = LoopPhase(time / (3.9f + i * 0.31f) + i * 0.382f);
            var amount = Math.Max(0, 1 - MathF.Abs(age - 0.1f) * 9);
            var source = new Rectangle(pane.X - 1, pane.Y - 3, 3, 6);
            spriteBatch.Draw(mural, source, source, new Color(0.28f, 0.31f, 0.38f) * amount);
        }
    }

    /// <summary>Draws the additive fire light, sparks and lightning.</summary>
    /// <param name="time">Seconds since the menu opened.</param>
    private void DrawLights(float time)
    {
        var amber = new Color(255, 127, 36);
        for (var i = 0; i < Torches.Length; i++)
        {
            var basePosition = Torches[i];
            var clock = time + i * 1.73f;
            DrawGlow(basePosition - new Vector2(0, 14), new(51, 76), amber, 0.12f + 0.26f * TorchFlicker(clock));
            for (var spark = 0; spark < 6; spark++)
            {
                var age = LoopPhase(clock * (0.92f + spark * 0.13f) + spark * 0.37f);
                var point = new Vector2(basePosition.X + MathF.Sin(age * 5f + spark) * (3 + 9 * age),
                    basePosition.Y - 10 - age * (37 + spark * 3));
                DrawGlow(point, new(3, 5), Color.Gold, 0.78f * FadeInOut(age) * (1 - age));
            }
        }

        for (var i = 0; i < 24; i++)
        {
            var age = LoopPhase(time * (0.59f + i % 4 * 0.14f) + i * 0.618f);
            var locus = Vector2.Lerp(new(625, 329), new(632, 444), age);
            locus.X += MathF.Sin(age * 12 + i * 7) * (4 + i % 9 * 4) * age;
            DrawGlow(locus, new(5 + i % 5, 11), i % 2 == 0 ? amber : Color.Yellow, 0.64f * FadeInOut(age), -0.43f);
        }

        DrawGlow(new(699, 162), new(82, 108), amber, 0.12f + 0.1f * MathF.Sin(time * 7));
        DrawGlow(new(564, 490), new(43, 81), amber, 0.16f + 0.13f * MathF.Sin(time * 9));
        var strike = (time + 2) % 8.7f;
        var flash = MathF.Max(0, 1 - strike * 4.4f) + MathF.Max(0, 0.62f - MathF.Abs(strike - 0.32f) * 9);
        if (flash > 0)
        {
            var lightning = new Color(170, 174, 255);
            DrawGlow(new(1176, 175), new(240, 238), lightning, 0.31f * flash);
            for (var i = 1; i < Bolt.Length; i++)
            {
                var start = Bolt[i - 1].ToVector2();
                var direction = Bolt[i].ToVector2() - start;
                spriteBatch.Draw(pixel, start, null, Color.White * (0.74f * flash),
                    MathF.Atan2(direction.Y, direction.X), Vector2.Zero, new Vector2(direction.Length(), 1.4f),
                    SpriteEffects.None, 0);
            }
        }

        DrawGlow(new(1320, 291), new(100, 75), Color.Gold, 0.29f + 0.19f * MathF.Sin(time * 7));
        for (var i = 0; i < 14; i++)
        {
            var age = LoopPhase(time * 1.7f + i * 0.618f);
            var nozzle = i % 2 == 0 ? new Vector2(1409, 593) : new Vector2(1534, 561);
            var jet = nozzle + new Vector2(-30 * age + i % 4 * 3, age * 68);
            DrawGlow(jet, new(9, 28), i % 3 == 0 ? Color.LightYellow : amber, 0.54f * FadeInOut(age), 0.35f);
        }
    }

    /// <summary>Draws the additive spell effects.</summary>
    /// <param name="time">Seconds since the menu opened.</param>
    private void DrawMagic(float time)
    {
        var blue = new Color(75, 154, 255);
        var gold = new Color(255, 206, 73);
        var purple = new Color(172, 91, 255);
        var orb = new Vector2(81, 379);
        for (var i = 0; i < 8; i++)
        {
            var angle = time * 1.65f + i * MathHelper.TwoPi / 8;
            var direction = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
            var reach = 35 + i % 3 * 8;
            var tint = i % 2 == 0 ? blue : gold;
            DrawGlow(orb + direction * (reach * 0.51f), new(reach, 9), tint, 0.76f, angle);
            DrawGlow(orb + direction * (reach * 0.44f), new(reach * 0.61f, 3), Color.White, 0.49f, angle);
        }

        foreach (var (socket, mouth, palm) in Necromancy)
        {
            DrawWisp(socket, palm, purple, time, -1);
            DrawWisp(mouth, palm, purple, time, 1);
        }

        for (var i = 0; i < 13; i++)
        {
            var age = LoopPhase(time * 0.7f + i * 0.618f);
            var glove = i % 2 == 0 ? new Vector2(1481, 385) : new Vector2(1657, 430);
            var point = glove + new Vector2(-58 * age, -47 * age + 16 * MathF.Sin(age * 8 + i));
            DrawGlow(point, new(34 + 33 * age, 22 + 23 * age),
                new Color(128, 236, 69), 0.24f * FadeInOut(age), -0.43f);
        }
    }

    /// <summary>Draws the flickering flame on each torch.</summary>
    /// <param name="time">Seconds since the menu opened.</param>
    private void DrawTorchFlames(float time)
    {
        var textureSize = new Vector2(torchFlame.Width, torchFlame.Height);
        var root = textureSize * new Vector2(0.5f, 0.92f);
        for (var i = 0; i < Torches.Length; i++)
        {
            var clock = time + i * 1.73f;
            var flicker = TorchFlicker(clock);
            var size = new Vector2(20 + 4 * flicker, 31 + 17 * flicker);
            var sway = 0.13f * MathF.Sin(clock * 9.1f) + 0.065f * MathF.Sin(clock * 17.6f);
            spriteBatch.Draw(torchFlame, Torches[i], null, Color.White, sway, root,
                size / textureSize, SpriteEffects.None, 0);

            // The smaller tongues grow and fade independently of the flame's core.
            for (var tongue = 0; tongue < 2; tongue++)
            {
                var age = LoopPhase(clock * (2.2f + 0.48f * tongue) + 0.43f * tongue);
                var side = tongue == 0 ? -1 : 1;
                var position = Torches[i] + new Vector2(side * (1 + 3 * age), -11 * age);
                var scale = size * new Vector2(0.45f, 0.42f + 0.41f * age) / textureSize;
                spriteBatch.Draw(torchFlame, position, null, Color.White * (0.76f * FadeInOut(age)),
                    sway + side * (0.13f + 0.29f * age), root, scale,
                    tongue == 0 ? SpriteEffects.FlipHorizontally : SpriteEffects.None, 0);
            }
        }
    }

    /// <summary>Draws a stream of motes drifting from a point toward a hand.</summary>
    /// <param name="source">Where the stream starts.</param>
    /// <param name="palm">Where the stream ends.</param>
    /// <param name="color">The glow color.</param>
    /// <param name="time">Seconds since the menu opened.</param>
    /// <param name="curl">1 or -1, the side the stream bends toward.</param>
    private void DrawWisp(Vector2 source, Vector2 palm, Color color, float time, int curl)
    {
        var delta = palm - source;
        var normal = Vector2.Normalize(new Vector2(-delta.Y, delta.X));
        const int wisps = 15;
        for (var i = 0; i < wisps; i++)
        {
            var age = LoopPhase(time * 0.68f + i / (float)wisps + source.X * 0.007f);
            var wave = FadeInOut(age) * (11 * curl + 8 * MathF.Sin(age * 10 - time * 3 + source.Y));
            var point = source + delta * age + normal * wave;
            var opacity = 0.43f * MathF.Sin(MathF.PI * (0.12f + 0.82f * age));
            var angle = MathF.Atan2(delta.Y, delta.X);
            DrawGlow(point, new(23, 8), color, opacity, angle);
            DrawGlow(point, new(14, 2.5f), Color.Plum, opacity * 0.78f, angle);
        }
    }

    /// <summary>Gets the brightness of a torch at a moment.</summary>
    /// <param name="clock">The torch's own time in seconds.</param>
    /// <returns>A value from 0 to 1.</returns>
    private static float TorchFlicker(float clock) => 0.5f + 0.24f * MathF.Sin(clock * 15.7f)
        + 0.16f * MathF.Sin(clock * 26.9f) + 0.1f * MathF.Sin(clock * 39.1f);

    /// <summary>Cuts a moving patch out of the painting.</summary>
    /// <param name="bounds">The area in painting pixels.</param>
    /// <param name="motion">The largest sideways shift in pixels.</param>
    /// <returns>The patch texture with its area and motion.</returns>
    private (Texture2D Texture, Rectangle Bounds, float Motion) CreateClothPatch(Rectangle bounds, float motion) =>
        (CreateFeatheredPatch(bounds), bounds, motion);

    /// <summary>Copies part of the painting with feathered edges.</summary>
    /// <param name="bounds">The area in painting pixels.</param>
    /// <returns>A texture the caller must dispose.</returns>
    private Texture2D CreateFeatheredPatch(Rectangle bounds)
    {
        var pixels = new Color[bounds.Width * bounds.Height];
        mural.GetData(0, bounds, pixels, 0, pixels.Length);
        for (var y = 0; y < bounds.Height; y++)
        {
            for (var x = 0; x < bounds.Width; x++)
            {
                var offset = new Vector2(2f * x / bounds.Width - 1, 2f * y / bounds.Height - 1);
                var feather = MathHelper.SmoothStep(0, 1, Math.Clamp((1 - offset.LengthSquared()) * 2.6f, 0, 1));
                pixels[y * bounds.Width + x] *= feather * 0.86f;
            }
        }
        return TextureFactory.FromPixels(mural.GraphicsDevice, bounds.Width, bounds.Height, pixels);
    }

    /// <summary>Draws the soft light sprite stretched and rotated around a point.</summary>
    /// <param name="point">The center in painting pixels.</param>
    /// <param name="size">The size in painting pixels.</param>
    /// <param name="color">The tint.</param>
    /// <param name="opacity">The strength of the tint.</param>
    /// <param name="rotation">The rotation in radians.</param>
    private void DrawGlow(Vector2 point, Vector2 size, Color color, float opacity, float rotation = 0f) =>
        spriteBatch.Draw(glow, point, null, color * opacity, rotation, new Vector2(glow.Width, glow.Height) / 2,
            size / new Vector2(glow.Width, glow.Height), SpriteEffects.None, 0);

    /// <summary>Gets the fractional part of a clock, for repeating animations.</summary>
    /// <param name="clock">The elapsed cycles.</param>
    /// <returns>The position within the current cycle, from 0 up to 1.</returns>
    private static float LoopPhase(float clock) => clock - MathF.Floor(clock);

    /// <summary>Fades a repeating animation in and out.</summary>
    /// <param name="phase">The position within the cycle, from 0 to 1.</param>
    /// <returns>0 at both ends of the cycle and 1 in the middle.</returns>
    private static float FadeInOut(float phase) => MathF.Sin(MathF.PI * phase);

    /// <summary>Releases the animation textures owned by the mural.</summary>
    public void Dispose()
    {
        foreach (var (texture, _, _) in clothPatches)
        {
            texture.Dispose();
        }
        groundFog.Dispose();
        torchFlame.Dispose();
    }
}

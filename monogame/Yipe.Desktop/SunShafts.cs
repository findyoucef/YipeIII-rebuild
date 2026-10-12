using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Yipe.Core;

namespace Yipe.Desktop;

/// <summary>World-anchored sunlight and coordinated cloud shade at native pixel resolution.</summary>
internal sealed class SunShafts : IDisposable
{
    private const int Period = 512;
    private readonly Texture2D mask, cloudMask;
    private readonly RenderTarget2D cloudSunMask;
    private readonly Color[] maskPixels, cloudPixels;
    private readonly Rectangle viewport;
    private Map? actorMap;
    private Vector2 previousFeet;
    private double actorTime, glintTime = -10;
    private float heroLight, stepDistance;
    private int actorStrength, actorCloudStrength, footfall;
    private bool wasMoving;
    public float Daylight { get; set; } = 1;
    private readonly List<Dust> dust = new(12);
    private readonly record struct Dust(Vector2 Position, Vector2 Velocity, double Born);
    private Texture2D? heroAtlas, heroRim;
    private Point[] glints = [];
    private readonly BlendState illuminate = new()
    {
        ColorSourceBlend = Blend.DestinationColor, ColorDestinationBlend = Blend.One,
        AlphaSourceBlend = Blend.Zero, AlphaDestinationBlend = Blend.One,
    };
    private readonly BlendState occlude = new()
    {
        ColorSourceBlend = Blend.Zero, ColorDestinationBlend = Blend.InverseSourceAlpha,
        AlphaSourceBlend = Blend.Zero, AlphaDestinationBlend = Blend.One,
    };

    public SunShafts(GraphicsDevice device, int width, int height)
    {
        viewport = new(0, 0, width, height);
        mask = new Texture2D(device, Period, Map.Height * 32);
        cloudMask = new Texture2D(device, Period, Period);
        cloudSunMask = new RenderTarget2D(device, width, height);
        maskPixels = new Color[mask.Width * mask.Height];
        cloudPixels = new Color[Period * Period];
        // Unequal cloud openings produce a few broad shafts with narrow companions.
        (float Center, float Width, float Strength)[] openings =
            [(40, 30, 1), (87, 9, .5f), (250, 20, .75f), (290, 7, .5f), (433, 30, .9f)];
        for (var y = 0; y < mask.Height; y++)
            for (var x = 0; x < mask.Width; x++)
            {
                var across = x - y * .48f;
                var light = 0f;
                foreach (var opening in openings)
                {
                    var distance = MathF.Abs(across - opening.Center);
                    distance %= Period;
                    distance = Math.Min(distance, Period - distance);
                    var spread = opening.Width * (1 + y * .001f);
                    var falloff = Math.Clamp(1 - distance / spread, 0, 1);
                    light = Math.Max(light, falloff * opening.Strength * (1 - y * .0008f));
                }
                var level = MathF.Floor(light * 4) / 4;
                maskPixels[y * mask.Width + x] = new Color(48, 38, 20) * level;
            }
        // Connected puffs give each small cloud a distinct crown and rounded underside.
        // Build only the shadow mask here: terrain and sprite pixels are never filtered.
        (float X, float Y, float Radius)[][] silhouettes =
        [
            [(-23, 6, 7), (-13, 0, 10), (0, -5, 14), (16, 2, 10), (24, 7, 7)],
            [(-24, 6, 7), (-12, -1, 12), (9, -6, 14), (23, 5, 8)],
            [(-20, 6, 9), (-9, 0, 11), (4, -9, 13), (18, 3, 10)],
            [(-24, 5, 7), (-13, -5, 11), (2, 0, 11), (16, -2, 10), (24, 7, 7)],
        ];
        var cloudRandom = new Random(17);
        var centers = new List<Point>(20);
        for (var attempt = 0; attempt < 400 && centers.Count < 20; attempt++)
        {
            var center = new Point(cloudRandom.Next(Period), cloudRandom.Next(Period));
            // Leave clear air between silhouettes, including across the wrapped edges.
            if (centers.Any(other => Math.Min(Math.Abs(center.X - other.X), Period - Math.Abs(center.X - other.X)) < 78
                && Math.Min(Math.Abs(center.Y - other.Y), Period - Math.Abs(center.Y - other.Y)) < 60)) continue;
            centers.Add(center);
            var puffs = silhouettes[cloudRandom.Next(silhouettes.Length)];
            var scale = .75f + cloudRandom.Next(6) * .05f;
            var flip = cloudRandom.Next(2) == 0 ? -1 : 1;
            var density = .76f + cloudRandom.Next(5) * .05f;
            for (var dy = -26; dy <= 23; dy++)
                for (var dx = -34; dx <= 34; dx++)
                {
                    var x = dx / scale * flip; var y = dy / scale;
                    // A shallow oval joins the crown to one calm, curved base.
                    var covered = x * x / (25 * 25) + (y - 7) * (y - 7) / (8 * 8) <= 1;
                    foreach (var puff in puffs)
                        covered |= (x - puff.X) * (x - puff.X) + (y - puff.Y) * (y - puff.Y) <= puff.Radius * puff.Radius;
                    if (!covered) continue;
                    // One density envelope keeps the connected puffs from looking like bubbles.
                    var core = Math.Clamp(1 - x * x / (32 * 32) - y * y / (28 * 28), 0, 1);
                    var cover = density * (.78f + .22f * core);
                    var index = ((center.Y + dy + Period) % Period) * Period + (center.X + dx + Period) % Period;
                    cloudPixels[index] = Color.Black * cover;
                }
        }
        // Feather just the shadow mask, once at startup. Weighted horizontal and vertical
        // passes keep the cloud centers defined and fade both sides of each edge, including seams.
        const int featherRadius = 3;
        const int featherWeight = (featherRadius + 1) * (featherRadius + 1);
        var horizontal = new float[cloudPixels.Length];
        for (var y = 0; y < Period; y++)
            for (var x = 0; x < Period; x++)
            {
                var alpha = 0f;
                for (var offset = -featherRadius; offset <= featherRadius; offset++)
                    alpha += cloudPixels[y * Period + (x + offset + Period) % Period].A
                        * (featherRadius + 1 - Math.Abs(offset));
                horizontal[y * Period + x] = alpha / (255f * featherWeight);
            }
        for (var y = 0; y < Period; y++)
            for (var x = 0; x < Period; x++)
            {
                var alpha = 0f;
                for (var offset = -featherRadius; offset <= featherRadius; offset++)
                    alpha += horizontal[(y + offset + Period) % Period * Period + x]
                        * (featherRadius + 1 - Math.Abs(offset));
                cloudPixels[y * Period + x] = Color.Black * (alpha / featherWeight);
            }
        mask.SetData(maskPixels);
        cloudMask.SetData(cloudPixels);
    }

    private static float Amount(double time, int strength) =>
        (strength == 1 ? .55f : 1f) * (.9f + .1f * (float)Math.Sin(time * .18));

    private float LightAt(Vector2 position, double time, int strength)
    {
        var x = ((int)MathF.Round(position.X) + (int)(Math.Floor(time * 2) % Period)) % Period;
        if (x < 0) x += Period;
        var y = Math.Clamp((int)MathF.Round(position.Y), 0, mask.Height - 1);
        var cover = actorCloudStrength == 0 ? 0 : CloudAt(new(MathF.Round(position.X), MathF.Round(position.Y)), time);
        return maskPixels[y * Period + x].R / 48f * Amount(time, strength) * (1 - cover) * Daylight;
    }

    private static Vector2 CloudDrift(double time) => new((float)(time * 2.4 % Period), (float)(time * .55 % Period));

    private float CloudAt(Vector2 position, double time)
    {
        var p = position + CloudDrift(time);
        var x = (int)MathF.Floor(p.X); var y = (int)MathF.Floor(p.Y);
        var fx = p.X - x; var fy = p.Y - y;
        float Sample(int dx, int dy) => cloudPixels[((y + dy) % Period + Period) % Period * Period
            + ((x + dx) % Period + Period) % Period].A / 255f;
        return MathHelper.Lerp(MathHelper.Lerp(Sample(0, 0), Sample(1, 0), fx),
            MathHelper.Lerp(Sample(0, 1), Sample(1, 1), fx), fy);
    }

    public void Prepare(SpriteBatch batch, Vector2 camera, double time, int cloudStrength)
    {
        if (cloudStrength == 0) return;
        mask.GraphicsDevice.SetRenderTarget(cloudSunMask);
        batch.Begin(blendState: BlendState.Opaque, samplerState: SamplerState.PointWrap);
        batch.Draw(mask, viewport, MaskSource(camera, time), Color.White);
        batch.End();
        batch.Begin(blendState: occlude, samplerState: SamplerState.LinearWrap);
        DrawCloudMask(batch, camera, time, Color.White);
        batch.End();
    }

    private void DrawCloudMask(SpriteBatch batch, Vector2 camera, double time, Color color)
    {
        var drift = CloudDrift(time);
        var x = (int)MathF.Floor(drift.X); var y = (int)MathF.Floor(drift.Y);
        // Interpolating the mask smooths travel without resampling the world artwork.
        var position = new Vector2(x, y) - drift;
        var source = new Rectangle((int)MathF.Round(camera.X) + x, (int)MathF.Round(camera.Y) + y,
            viewport.Width + 1, viewport.Height + 1);
        batch.Draw(cloudMask, position, source, color);
    }

    public void Update(Map map, PlayerMotion player, double time, int strength, int cloudStrength)
    {
        var feet = player.VisualPosition * 32 + new Vector2(16, 25);
        var traveled = Vector2.Distance(feet, previousFeet);
        var elapsed = Math.Clamp(time - actorTime, 0, .25);
        // Loading, travel and snaps must not carry a trail or glint into a new scene.
        if (!ReferenceEquals(map, actorMap) || time < actorTime || traveled > 48
            || traveled > 0 && !player.IsMoving && !wasMoving)
        {
            dust.Clear(); heroLight = 0; stepDistance = 0; footfall = 0; glintTime = -10;
            traveled = 0; elapsed = 0;
        }
        actorMap = map; actorTime = time; actorStrength = strength; actorCloudStrength = cloudStrength;
        var direction = traveled > 0 ? (feet - previousFeet) / traveled : Vector2.Zero;
        previousFeet = feet; wasMoving = player.IsMoving;
        if (strength == 0 || Daylight <= 0 || map.Id is < 1 or > 40)
        { dust.Clear(); heroLight = 0; stepDistance = 0; glintTime = -10; return; }

        var light = LightAt(feet - new Vector2(3, 18), time, strength);
        heroLight += Math.Clamp(light - heroLight, -(float)elapsed * 4, (float)elapsed * 4);
        if (player.IsMoving && light > .1f && time - glintTime >= 3)
            glintTime = time;
        dust.RemoveAll(mote => time - mote.Born >= .65);
        if (elapsed <= 0 || traveled == 0) return;
        stepDistance += traveled;
        if (stepDistance < 16) return;
        stepDistance %= 16;
        var side = new Vector2(-direction.Y, direction.X);
        var foot = feet - direction * 3 + side * (++footfall % 2 == 0 ? -4 : 4);
        var cell = new MapPosition((int)foot.X / 32, (int)foot.Y / 32);
        if (!Map.Contains(cell)) return;
        var tile = map.Tile(cell);
        // Dry grass, roads and sand only: no water, bridge decks, fire or platforms.
        if (tile is not (2 or 8 or >= 0x16 and <= 0x1e or 0x2d or 0x2e or 0x42 or 0x57)
            || LightAt(foot, time, strength) <= .1f) return;
        for (var i = 0; i < 2 && dust.Count < 12; i++)
            dust.Add(new(foot + side * i, new Vector2(i == 0 ? -3 : 2, -6 - i * 2), time));
    }

    public void DrawDust(SpriteBatch batch, Texture2D pixel, double time)
    {
        foreach (var mote in dust)
        {
            var age = (float)(time - mote.Born);
            if (age < 0 || age >= .65f) continue;
            var position = mote.Position + mote.Velocity * age;
            var light = LightAt(position, time, actorStrength);
            var alpha = MathF.Ceiling((1 - age / .65f) * 3) / 3 * Math.Min(.65f, light * 1.5f);
            batch.Draw(pixel, new Rectangle((int)MathF.Round(position.X), (int)MathF.Round(position.Y), 1, 1),
                RetroArt.Palette[6] * alpha);
        }
    }

    public void DrawHero(SpriteBatch batch, Texture2D atlas, Rectangle frame, Vector2 position, Texture2D pixel, double time)
    {
        if (heroLight <= 0 || Daylight <= 0) return;
        if (!ReferenceEquals(atlas, heroAtlas)) BuildHeroMask(atlas);
        var amount = MathF.Ceiling(heroLight * 4) / 4 * .65f * Daylight;
        batch.Draw(heroRim!, position, frame, Color.White * amount);
        var age = time - glintTime;
        if (age < 0 || age >= .18) return;
        var glint = glints[frame.Y / 32 * (atlas.Width / 32) + frame.X / 32];
        if (glint.X < 0) return;
        var color = RetroArt.Palette[7] * ((age < .10 ? .9f : .45f) * Daylight);
        batch.Draw(pixel, new Rectangle((int)position.X + glint.X, (int)position.Y + glint.Y, age < .10 ? 2 : 1, 1), color);
    }

    private void BuildHeroMask(Texture2D atlas)
    {
        heroRim?.Dispose();
        heroAtlas = atlas;
        var pixels = new Color[atlas.Width * atlas.Height]; atlas.GetData(pixels);
        var rim = new Color[pixels.Length];
        glints = Enumerable.Repeat(new Point(-1, -1), atlas.Width / 32 * (atlas.Height / 32)).ToArray();
        for (var cy = 0; cy < atlas.Height; cy += 32)
            for (var cx = 0; cx < atlas.Width; cx += 32)
            {
                var index = cy / 32 * (atlas.Width / 32) + cx / 32;
                for (var y = 1; y < 28; y++)
                    for (var x = 1; x < 31; x++)
                    {
                        var i = (cy + y) * atlas.Width + cx + x;
                        var color = pixels[i];
                        bool Dark(Color p) => p.A == 0 || p == RetroArt.Palette[0] || p == RetroArt.Palette[1];
                        if (Dark(color)) continue;
                        if (y < 21 && (Dark(pixels[i - 1]) || Dark(pixels[i - atlas.Width])))
                            rim[i] = RetroArt.Palette[6];
                        // Adjacent cool silver pixels identify an existing equipment highlight.
                        bool Silver(Color p) => p == RetroArt.Palette[4] || p == RetroArt.Palette[5];
                        if (glints[index].X < 0 && Silver(color) && Silver(pixels[i + 1]))
                            glints[index] = new(x, y);
                    }
            }
        heroRim = new Texture2D(atlas.GraphicsDevice, atlas.Width, atlas.Height);
        heroRim.SetData(rim);
    }

    private Rectangle MaskSource(Vector2 camera, double time)
    {
        var drift = (int)(Math.Floor(time * 2) % Period);
        return new Rectangle((int)MathF.Round(camera.X) + drift,
            (int)MathF.Round(camera.Y), viewport.Width, viewport.Height);
    }

    public void DrawClouds(SpriteBatch batch, Map map, Vector2 camera, double time, int strength)
    {
        if (strength == 0 || Daylight <= 0 || map.Id is < 1 or > 40) return;
        batch.Begin(blendState: BlendState.AlphaBlend, samplerState: SamplerState.LinearWrap);
        DrawCloudMask(batch, camera, time, Color.White * ((strength == 1 ? .07f : .14f) * Daylight));
        batch.End();
    }

    public void Draw(SpriteBatch batch, Map map, Vector2 camera, double time, int strength, int cloudStrength)
    {
        if (strength == 0 || Daylight <= 0 || map.Id is < 1 or > 40) return;
        var amount = Amount(time, strength) * Daylight;
        // Destination-color blending preserves black outlines and undiscovered cells.
        batch.Begin(blendState: illuminate, samplerState: SamplerState.PointWrap);
        batch.Draw(cloudStrength == 0 ? mask : cloudSunMask, viewport,
            cloudStrength == 0 ? MaskSource(camera, time) : cloudSunMask.Bounds, Color.White * amount);
        batch.End();
    }

    public void Dispose() { mask.Dispose(); cloudMask.Dispose(); cloudSunMask.Dispose(); heroRim?.Dispose(); illuminate.Dispose(); occlude.Dispose(); }
}

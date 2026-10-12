using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Yipe.Core;

namespace Yipe.Desktop;

internal sealed partial class WorldRenderer
{
    private readonly Dictionary<int, Texture2D> monsterAnimations = new();
    private Texture2D? empoweredGatekeeperAnimation;
    private readonly MonsterMotion roamingMotion = new(), pursuingMotion = new();
    private Map? monsterMap;
    public bool MonstersMoving => roamingMotion.IsMoving || pursuingMotion.IsMoving;

    public (int Roaming, int Pursuer) SynchronizeMonsters(Adventure game, bool animate, Rectangle? view = null)
    {
        animate &= ReferenceEquals(monsterMap, game.Map);
        monsterMap = game.Map;
        return (Sync(roamingMotion, game.Roaming, game.Roaming?.Position ?? default, game.Roaming?.TypeId ?? 0),
            Sync(pursuingMotion, game.Pursuer, game.Pursuer?.Position ?? default, game.Pursuer?.TypeId ?? 0));
        int Sync(MonsterMotion motion, object? actor, MapPosition position, int type)
        {
            var tracked = actor is not null && motion.Tracks(actor);
            var visible = actor is not null && game.Visibility.Contains(position)
                && (!tracked || game.Visibility.Contains(motion.Position));
            if (visible && view is { } viewport)
            {
                var destination = new Point(position.X * 32 + 16, position.Y * 32 + 16);
                var source = new Point((int)(motion.VisualPosition.X * 32 + 16), (int)(motion.VisualPosition.Y * 32 + 16));
                visible = viewport.Contains(destination) && (!tracked || viewport.Contains(source));
            }
            return motion.Synchronize(actor, position, animate, game.Location.Position, visible) ? type : 0;
        }
    }

    public void UpdateMonsters(double seconds)
    {
        roamingMotion.Update(seconds);
        pursuingMotion.Update(seconds);
    }

    private void LoadMonsterAnimations(GraphicsDevice device, string content)
    {
        foreach (var (type, name) in new[]
        {
            (1, "rabbit"), (2, "beetle"), (3, "weasel"), (4, "moose"), (5, "bat"), (6, "bird"),
            (7, "mouth-man"), (8, "golem"), (9, "one-eye"), (10, "baby-gargoyle"), (11, "adult-gargoyle"),
            (12, "wizard"), (13, "fire-bug"), (14, "flame-flier"), (15, "stallion"), (16, "skeleton"), (17, "man-trap"),
            (18, "daemon"), (19, "snow-monster"), (20, "fire-lord"), (21, "air-wave"), (22, "air-thing"), (23, "toad"),
            (24, "squawker"), (25, "water-ghost"), (26, "wisp"), (27, "crab"), (28, "eel"), (29, "merman"),
            (30, "hand-head"), (31, "lump"), (32, "earthworm"), (33, "muscles"), (34, "axe-man"),
            (35, "wormoid"), (36, "stone-king"), (37, "flame-beast"),
            (38, "air-dragon"), (39, "soaked-spike"), (40, "earth-giant"),
            (41, "gatekeeper"),
        })
        {
            var sheet = RetroArt.Load(device, content, $"World/monster-{name}-animation.png", 4, 4, 48);
            var source = new Color[sheet.Width * sheet.Height]; sheet.GetData(source);
            if (type == 8)
            {
                // Replace only the walking row; retain the existing idle/action/defeat art.
                using var walk = RetroArt.Load(device, content, "World/monster-golem-walk.png", 2, 2, 48);
                var pose = new Color[48 * 48];
                for (var frame = 0; frame < 4; frame++)
                {
                    walk.GetData(0, Cell(frame, 2, 48), pose, 0, pose.Length);
                    for (var y = 0; y < 48; y++)
                        Array.Copy(pose, y * 48, source, (48 + y) * sheet.Width + frame * 48, 48);
                }
            }
            var original = monsters[(type - 1) / 16];
            var originalPixels = new Color[original.Width * original.Height]; original.GetData(originalPixels);
            var originalBounds = InkBounds(originalPixels, original.Width, Cell((type - 1) % 16, 4, 48));
            var poseBounds = Enumerable.Range(0, 16).Select(frame => InkBounds(source, sheet.Width, Cell(frame, 4, 48))).ToArray();
            var firstBounds = poseBounds[0];
            var scale = Math.Min(1, originalBounds.Height / (float)firstBounds.Height);
            if (type >= 10)
                scale = Math.Min(originalBounds.Height / (float)firstBounds.Height,
                    Math.Min((monsterBottoms[type - 1] - 2f) / poseBounds.Max(bounds => bounds.Height),
                        44f / poseBounds.Max(bounds => bounds.Width)));
            var aligned = new Color[source.Length];
            // One size correction for the whole sheet, then translation to the original foot anchor.
            // Pose silhouettes can change; their body scale and ground contact stay consistent.
            for (var frame = 0; frame < 16; frame++)
            {
                var bounds = poseBounds[frame];
                var width = Math.Max(2, (int)MathF.Round(bounds.Width * scale));
                var height = Math.Max(2, (int)MathF.Round(bounds.Height * scale));
                var left = 24 - width / 2;
                if (left < 0 || height > monsterBottoms[type - 1] || left + width > 48)
                    throw new InvalidDataException($"Monster {name} pose {frame} exceeds its native cell.");
                var cell = Cell(frame, 4, 48);
                var resized = new Color[width * height];
                for (var y = 0; y < height; y++)
                    for (var x = 0; x < width; x++)
                    {
                        var sx = bounds.X + (int)MathF.Round(x * (bounds.Width - 1f) / (width - 1));
                        var sy = bounds.Y + (int)MathF.Round(y * (bounds.Height - 1f) / (height - 1));
                        resized[y * width + x] = source[sy * sheet.Width + sx];
                    }
                // Downsampling can miss a one-pixel toe. Anchor the actual surviving ink.
                var ink = InkBounds(resized, width, new(0, 0, width, height));
                var top = monsterBottoms[type - 1] - ink.Bottom;
                for (var y = ink.Top; y < ink.Bottom; y++)
                    Array.Copy(resized, y * width, aligned, (cell.Y + top + y) * sheet.Width + cell.X + left, width);
            }
            sheet.SetData(aligned);
            monsterAnimations.Add(type, sheet);
            if (type == 41)
            {
                var empowered = aligned.Select(color => color == RetroArt.Palette[34] ? RetroArt.Palette[35]
                    : color == RetroArt.Palette[35] ? RetroArt.Palette[7]
                    : color == RetroArt.Palette[22] ? RetroArt.Palette[23]
                    : color == RetroArt.Palette[37] ? RetroArt.Palette[22] : color).ToArray();
                empoweredGatekeeperAnimation = new Texture2D(device, sheet.Width, sheet.Height);
                empoweredGatekeeperAnimation.SetData(empowered);
            }
        }
    }

    private static Rectangle InkBounds(Color[] pixels, int width, Rectangle cell)
    {
        var left = cell.Right; var top = cell.Bottom; var right = cell.Left; var bottom = cell.Top;
        for (var y = cell.Top; y < cell.Bottom; y++)
            for (var x = cell.Left; x < cell.Right; x++)
                if (pixels[y * width + x].A != 0)
                { left = Math.Min(left, x); top = Math.Min(top, y); right = Math.Max(right, x + 1); bottom = Math.Max(bottom, y + 1); }
        if (right <= left || bottom <= top) throw new InvalidDataException("Monster animation contains an empty pose.");
        return new(left, top, right - left, bottom - top);
    }

    internal static int MonsterIdleFrame(int type, double time) => type switch
    {
        1 => time % 4 < 2.5 ? 0 : time % 4 < 2.85 ? 1 : time % 4 < 3.05 ? 2 : 3,
        3 => (int)(time * 4) % 4,
        4 => (int)(time * 1.2) % 4,
        5 => (int)(time * 6) % 4,
        6 => (int)(time * 2) % 4,
        7 => (int)(time * 2.5) % 4,
        8 => (int)(time * .9) % 4,
        9 => (int)(time * 1.3) % 4,
        10 => (int)(time * 2.8) % 4,
        11 => (int)(time * 1.4) % 4,
        12 => (int)(time * 1.5) % 4,
        13 => (int)(time * 4) % 4,
        14 => (int)(time * 6) % 4,
        15 => (int)(time * 1.5) % 4,
        16 => (int)(time * 4) % 4,
        17 => (int)(time * 1.8) % 4,
        18 => (int)(time * 3.5) % 4,
        19 => (int)(time * 1.2) % 4,
        20 => (int)(time * 2) % 4,
        21 or 22 => (int)(time * 2) % 4,
        23 => (int)(time * 1.8) % 4,
        24 => (int)(time * 2.5) % 4,
        25 => (int)(time * 2) % 4,
        26 => (int)(time * 3) % 4,
        27 => (int)(time * 2) % 4,
        28 => (int)(time * 3.5) % 4,
        29 => (int)(time * 1.8) % 4,
        30 => (int)(time * 2) % 4,
        31 => (int)(time * 1.5) % 4,
        32 => (int)(time * 1.8) % 4,
        33 => (int)(time * 1.5) % 4,
        34 => (int)(time * 1.4) % 4,
        35 or 36 or 37 => (int)(time * .9) % 4,
        38 => (int)(time * 1.4) % 4,
        39 => (int)(time * 1.1) % 4,
        40 => (int)(time * .8) % 4,
        41 => (int)(time * .9) % 4,
        _ => (int)(time * 3) % 4,
    };

    public bool HasMonsterAnimation(int type) => monsterAnimations.ContainsKey(type);

    public void DrawAnimatedMonster(SpriteBatch batch, int type, Rectangle destination, int frame,
        bool facingLeft = false, Color? tint = null)
    {
        if (!monsterAnimations.TryGetValue(type, out var sheet)) { DrawMonster(batch, type, destination, tint); return; }
        batch.Draw(sheet, destination, Cell(Math.Clamp(frame, 0, 15), 4, 48), tint ?? Color.White,
            0, Vector2.Zero, facingLeft ? SpriteEffects.FlipHorizontally : SpriteEffects.None, 0);
    }

    public void DrawEmpoweredGatekeeper(SpriteBatch batch, Rectangle destination, int frame,
        bool facingLeft = false, Color? tint = null)
    {
        if (empoweredGatekeeperAnimation is null) { DrawAnimatedMonster(batch, 41, destination, frame, facingLeft, tint); return; }
        // Favor the authored wide brace at rest; action, hurt and defeat retain their resolved poses.
        if (frame is >= 0 and < 4 && frame != 1) frame = 2;
        batch.Draw(empoweredGatekeeperAnimation, destination, Cell(Math.Clamp(frame, 0, 15), 4, 48), tint ?? Color.White,
            0, Vector2.Zero, facingLeft ? SpriteEffects.FlipHorizontally : SpriteEffects.None, 0);
    }

    private static bool MonsterHovers(int type) => type is 5 or 14 or 21 or 22 or 25 or 26;

    private static int MonsterLift(int type, double phase, MonsterMotion? motion)
        => MonsterHovers(type) ? 3 + (int)MathF.Round(MathF.Sin((float)phase * (type is 5 or 14 ? 6 : 2)))
            : type is 1 or 6 && motion is { IsMoving: true }
                ? (int)MathF.Round(MathF.Sin(motion.Progress * MathF.PI) * (type == 6 ? 3 : 4))
            : type is 10 or 11 or 18 or 23 or 24 or 38 && motion is { IsMoving: true }
                ? (int)MathF.Round(MathF.Sin(motion.Progress * MathF.PI) * 3) : 0;

    private void DrawMonsterActor(SpriteBatch batch, int type, Vector2 position, int scale, double time,
        bool facingLeft, MonsterMotion? motion = null, Adventure? reflection = null)
    {
        var feet = new Point((int)MathF.Round(position.X * 32) + 16 * scale,
            (int)MathF.Round(position.Y * 32) + 32 * scale);
        var phase = time + type * .37 + position.X * .19 + position.Y * .31;
        var progress = motion?.Progress ?? 1;
        var airborne = MonsterLift(type, phase, motion);
        var breeding = monsterMap is { } map && IsBreeding(map);
        var volcanic = monsterMap is { } fire && IsFire(fire);
        var earthy = monsterMap is { } earth && IsEarth(earth);
        var mineral = monsterMap is { } stone && IsStone(stone);
        var frozen = monsterMap is { } ice && IsIce(ice);
        var ancient = monsterMap is { } final && IsFinal(final);
        var damp = monsterMap is { } waterCave && IsWaterCave(waterCave);
        var frame = motion is { IsMoving: true } ? 4 + Math.Min(3, (int)(progress * 4)) : MonsterIdleFrame(type, phase);
        // A tile move is one footstep; hold its plant while the landing settles.
        if (type is 7 or 8 or 9 or 12 or 15 or 16 or 19 or 20 or 30 or 31 or 33 or 34 or 36 or 37 or 39 or 40 or 41
            && motion is not null && (motion.IsMoving || motion.LandingAge is >= 0 and < .12))
            frame = 4 + (motion.AlternateStep ? 0 : 2) + (progress < .5f ? 0 : 1);
        if (reflection is not null)
        {
            if (monsterAnimations.TryGetValue(type, out var sheet))
            {
                var pose = Cell(frame, 4, 48); pose.Height = monsterBottoms[type - 1];
                DrawIceReflection(batch, reflection, sheet, pose, feet, scale, facingLeft, airborne);
            }
            return;
        }
        for (var row = 0; row < 4; row++)
        {
            var radius = (row is 1 or 2 ? 11 : 8) - (airborne > 0 ? 2 : 0);
            batch.Draw(pixel, new Rectangle(feet.X - radius * scale, feet.Y - (4 - row) * scale,
                (radius * 2 + 1) * scale, scale), RetroArt.Palette[0] *
                    (MonsterHovers(type) ? type is 5 or 14 ? .13f : .09f : row is 1 or 2 ? .22f : .12f)
                    * (breeding ? 1.8f : volcanic ? 1.65f : earthy || mineral || frozen || damp || ancient ? 1.6f : 1f));
        }
        DrawAnimatedMonster(batch, type, new(feet.X - 24 * scale,
            feet.Y - (monsterBottoms[type - 1] + airborne) * scale, 48 * scale, 48 * scale), frame, facingLeft,
            breeding ? new Color(210, 222, 236) : volcanic ? new Color(255, 226, 203) : earthy ? new Color(235, 225, 205)
                : mineral ? new Color(219, 231, 232) : frozen ? new Color(211, 232, 246) : damp ? new Color(206, 231, 223) : ancient ? new Color(238, 211, 225) : Color.White);
        if (motion is not null) DrawMonsterContact(batch, type, feet, scale, motion);
    }

    private void DrawMonsterContact(SpriteBatch batch, int type, Point feet, int scale, MonsterMotion motion)
    {
        if (monsterMap is null || MonsterHovers(type)) return;
        if (WaterAt(feet.X, feet.Y - 1))
        {
            if (type is 27 or 28 or 29 or 39 && (motion.IsMoving || motion.LandingAge is >= 0 and < .12))
            {
                var age = motion.IsMoving ? motion.Progress : (float)(motion.LandingAge / .12);
                var spread = 4 + (int)(age * 4);
                var alpha = motion.IsMoving ? MathF.Sin(age * MathF.PI) * .22f : (1 - age) * .35f;
                var color = RetroArt.Palette[30] * alpha;
                for (var row = 0; row < 2; row++)
                    for (var side = -1; side <= 1; side += 2)
                        for (var dx = 0; dx < 2; dx++)
                        {
                            var x = feet.X + side * (spread - row) * scale + dx;
                            var y = feet.Y - (2 - row) * scale;
                            if (WaterAt(x, y)) batch.Draw(pixel, new Rectangle(x, y, 1, 1), color);
                        }
            }
            return;
        }
        if (type is >= 1 and <= 4 or 7 or >= 8 and <= 41 && motion.LandingAge is >= 0 and < .12
            && (type >= 8 || monsterMap.Id is >= 1 and <= 40))
        {
            var spread = 4 + (int)(motion.LandingAge * 35);
            var heavy = MonsterMotion.UsesHeavyStep(type);
            var color = (IsFire(monsterMap) ? FirePalette[18] : IsBreeding(monsterMap) ? BreedingSoilPalette[3]
                : IsEarth(monsterMap) ? EarthSoilPalette[5]
                : IsStone(monsterMap) ? StonePalette[5]
                : IsIce(monsterMap) ? IcePalette[7]
                : IsWaterCave(monsterMap) ? WaterCavePalette[4]
                : IsFinal(monsterMap) ? FinalPalette[5]
                : RetroArt.Palette[type is 8 or 10 or 11 or 36 or 40 || monsterMap.Id > 40 ? 3 : 18])
                * (float)((heavy ? .5 : .4) * (1 - motion.LandingAge / .12));
            var width = heavy ? 2 : 1;
            batch.Draw(pixel, new Rectangle(feet.X - spread, feet.Y - 3, width, 1), color);
            batch.Draw(pixel, new Rectangle(feet.X + spread, feet.Y - 2, width, 1), color);
        }
    }

    private void DrawRoamingActor(SpriteBatch batch, Adventure game, int row, double time, bool reflection = false)
    {
        Draw(game.Roaming, game.Roaming?.TypeId ?? 0, game.Roaming?.Position ?? default, roamingMotion);
        Draw(game.Pursuer, game.Pursuer?.TypeId ?? 0, game.Pursuer?.Position ?? default, pursuingMotion);
        void Draw(object? actor, int type, MapPosition cell, MonsterMotion motion)
        {
            if (actor is null || !game.Visibility.Contains(cell)) return;
            var tracked = motion.Tracks(actor);
            var position = tracked ? motion.VisualPosition : new Vector2(cell.X, cell.Y);
            if (!game.Visibility.Contains(new((int)MathF.Floor(position.X), (int)MathF.Floor(position.Y)))) return;
            if (Math.Clamp((int)MathF.Floor(position.Y), 0, Map.Height - 1) != row) return;
            DrawMonsterActor(batch, type, position, 1, time, tracked && motion.FacingLeft, tracked ? motion : null,
                reflection ? game : null);
        }
    }

    public void DrawPursuitAlerts(SpriteBatch batch, Adventure game, double time)
    {
        Draw(game.Roaming, game.Roaming?.TypeId ?? 0, roamingMotion);
        Draw(game.Pursuer, game.Pursuer?.TypeId ?? 0, pursuingMotion);
        void Draw(object? actor, int type, MonsterMotion motion)
        {
            if (actor is null || !motion.Tracks(actor) || motion.AlertRemaining <= 0
                || !game.Visibility.Contains(motion.Position)) return;
            var position = motion.VisualPosition;
            if (!game.Visibility.Contains(new((int)MathF.Floor(position.X), (int)MathF.Floor(position.Y)))) return;
            var phase = time + type * .37 + position.X * .19 + position.Y * .31;
            var head = new Point((int)MathF.Round(position.X * 32) + 16,
                (int)MathF.Round(position.Y * 32) + 32 - monsterBottoms[type - 1] + monsterTops[type - 1] - MonsterLift(type, phase, motion));
            DrawPursuitAlert(batch, game, head, motion.AlertRemaining);
        }
    }

    private void DrawPursuitAlert(SpriteBatch batch, Adventure game, Point head, double remaining)
    {
        var x = head.X - 1;
        var y = head.Y - 14 - (int)(3 * (1 - remaining / .7));
        var alpha = (float)Math.Clamp(remaining / .18, 0, 1);
        Paint(new(x - 1, y - 1, 4, 7), RetroArt.Palette[0] * (.9f * alpha));
        Paint(new(x - 1, y + 6, 4, 4), RetroArt.Palette[0] * (.9f * alpha));
        Paint(new(x, y, 2, 5), RetroArt.Palette[35] * alpha);
        Paint(new(x, y + 7, 2, 2), RetroArt.Palette[35] * alpha);
        void Paint(Rectangle bounds, Color color)
        {
            for (var py = bounds.Top; py < bounds.Bottom; py++)
                for (var px = bounds.Left; px < bounds.Right; px++)
                    if (px >= 0 && py >= 0 && px < Map.Width * 32 && py < Map.Height * 32
                        && game.Visibility.Contains(new(px / 32, py / 32)))
                        batch.Draw(pixel, new Rectangle(px, py, 1, 1), color);
        }
    }
}

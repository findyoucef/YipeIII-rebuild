using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Yipe.Core;

namespace Yipe.Desktop;

internal enum NpcActor { None, King, Teacher, PrincessPink, PrincessPurple, PrincessBlue }

internal sealed partial class WorldRenderer
{
    private readonly Dictionary<NpcActor, Texture2D> npcAnimations = new();

    private void LoadNpcAnimations(GraphicsDevice device, string content)
    {
        foreach (var (actor, name) in new[] { (NpcActor.King, "king"), (NpcActor.Teacher, "teacher"),
            (NpcActor.PrincessPink, "princess-pink"), (NpcActor.PrincessPurple, "princess-purple"), (NpcActor.PrincessBlue, "princess-blue") })
        {
            var sheet = RetroArt.Load(device, content, $"World/npc-{name}-animation.png", 4, 3, 48);
            var source = new Color[sheet.Width * sheet.Height]; sheet.GetData(source);
            var bounds = Enumerable.Range(0, 12).Select(f => InkBounds(source, sheet.Width, Cell(f, 4, 48))).ToArray();
            var original = actor is NpcActor.King or NpcActor.Teacher ? monsters[2] : quests;
            var originalCell = actor is NpcActor.King ? Cell(9, 4, 48) : actor is NpcActor.Teacher ? Cell(11, 4, 48)
                : Cell((int)actor - (int)NpcActor.PrincessPink, 2, 48);
            var originalPixels = new Color[original.Width * original.Height]; original.GetData(originalPixels);
            var originalHeight = InkBounds(originalPixels, original.Width, originalCell).Height;
            var scale = Math.Min(originalHeight / (float)bounds[0].Height,
                Math.Min(40f / bounds.Max(b => b.Height), 42f / bounds.Max(b => b.Width)));
            var aligned = new Color[source.Length];
            for (var frame = 0; frame < 12; frame++)
            {
                var ink = bounds[frame];
                var width = Math.Max(2, (int)MathF.Round(ink.Width * scale));
                var height = Math.Max(2, (int)MathF.Round(ink.Height * scale));
                var pose = new Color[width * height];
                for (var y = 0; y < height; y++) for (var x = 0; x < width; x++)
                    pose[y * width + x] = source[(ink.Y + (int)MathF.Round(y * (ink.Height - 1f) / (height - 1))) * sheet.Width
                        + ink.X + (int)MathF.Round(x * (ink.Width - 1f) / (width - 1))];
                var resized = InkBounds(pose, width, new(0, 0, width, height));
                // Align the feet/skirt hem rather than the hands: a wave cannot slide the body sideways.
                var feet = InkBounds(pose, width, new(0, Math.Max(0, resized.Bottom - 4), width, Math.Min(4, resized.Bottom)));
                var left = 24 - (feet.Left + feet.Right) / 2; var top = 44 - resized.Bottom;
                if (left < 0 || left + width > 48 || top < 0)
                    throw new InvalidDataException($"NPC {name} pose {frame} exceeds its native cell.");
                var cell = Cell(frame, 4, 48);
                for (var y = 0; y < height; y++)
                    Array.Copy(pose, y * width, aligned, (cell.Y + top + y) * sheet.Width + cell.X + left, width);
            }
            sheet.SetData(aligned); npcAnimations.Add(actor, sheet);
        }
    }

    internal static NpcActor PrincessActor(int variant) => variant switch
    { 1 => NpcActor.PrincessPink, 2 => NpcActor.PrincessPurple, 3 => NpcActor.PrincessBlue, _ => NpcActor.None };

    internal static int NpcFrame(NpcActor actor, double time, bool speaking = false, bool celebrating = false)
    {
        time = Math.Max(0, time);
        if (celebrating && time < 1.8) return time < .2 ? 8 : time < .5 ? 9 : time < 1.35 ? 10 : 11;
        var phase = time % (actor == NpcActor.Teacher ? 7 : actor == NpcActor.King ? 6 : 5);
        if (speaking && !celebrating && phase < 1.65)
            return phase < .2 ? 4 : phase < .65 ? 5 : phase < 1.3 ? 6 : 7;
        return phase < 3.8 ? 0 : phase < 4 ? 1 : phase < 4.13 ? 2 : phase < 4.45 ? 3 : 0;
    }

    internal void DrawNpcPortrait(SpriteBatch batch, NpcActor actor, Rectangle destination, double time, bool celebrating)
    {
        if (npcAnimations.TryGetValue(actor, out var sheet))
            batch.Draw(sheet, destination, Cell(NpcFrame(actor, time, speaking: true, celebrating), 4, 48), Color.White);
    }

    internal void DrawNpcWorld(SpriteBatch batch, NpcActor actor, MapPosition cell, double time, bool nearby = false, Map? map = null)
    {
        if (!npcAnimations.TryGetValue(actor, out var sheet)) return;
        // The King stands beyond the castle's front edge, not on its facade.
        // His smaller world pose leaves the doorway legible; portraits retain their full size.
        var king = actor == NpcActor.King;
        var feet = new Point(cell.X * 32 + 16, cell.Y * 32 + (king ? 36 : 28));
        for (var row = 0; row < 3; row++)
        {
            var radius = king ? (row == 1 ? 6 : 4) : (row == 1 ? 9 : 6);
            batch.Draw(pixel, new Rectangle(feet.X - radius, feet.Y - 2 + row, radius * 2, 1), RetroArt.Palette[0] * (king ? .45f : .3f));
        }
        var tint = map is null ? Color.White : IsFire(map) || IsHell(map) ? new Color(247, 220, 205)
            : IsIce(map) ? new Color(224, 237, 249) : IsWaterCave(map) ? new Color(223, 239, 233)
            : IsFinal(map) ? new Color(244, 224, 233) : IsBreeding(map) ? new Color(236, 229, 231) : Color.White;
        var frame = NpcFrame(actor, time + cell.X * .31 + cell.Y * .17, speaking: nearby);
        var destination = king ? new Rectangle(feet.X - 18, feet.Y - 33, 36, 36)
            : new Rectangle(feet.X - 24, feet.Y - 44, 48, 48);
        batch.Draw(sheet, destination, Cell(frame, 4, 48), tint);
    }
}

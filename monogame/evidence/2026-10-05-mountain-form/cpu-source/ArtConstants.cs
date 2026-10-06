using Microsoft.Xna.Framework;
namespace Yipe.Desktop;
internal static class RetroArt { internal static readonly Color[] Palette =
    [
        new(20, 25, 36), new(37, 43, 59), new(64, 73, 87), new(105, 117, 133),
        new(164, 173, 176), new(213, 216, 204), new(241, 220, 158), new(255, 241, 198),
        new(39, 59, 47), new(54, 78, 51), new(73, 99, 72), new(107, 135, 71),
        new(153, 169, 87), new(192, 194, 119), new(65, 44, 37), new(85, 56, 46),
        new(131, 86, 62), new(183, 136, 88), new(217, 184, 124), new(242, 198, 143),
        new(103, 43, 58), new(137, 55, 71), new(191, 86, 82), new(232, 137, 101),
        new(37, 65, 109), new(50, 109, 168), new(114, 164, 194), new(36, 71, 94),
        new(43, 107, 131), new(74, 158, 170), new(145, 199, 188), new(82, 59, 107),
        new(128, 86, 129), new(175, 123, 158), new(155, 115, 47), new(217, 172, 79),
        new(217, 155, 112), new(168, 93, 64), new(113, 71, 45), new(82, 112, 112),
    ]; }
internal static class WorldRenderer { internal static bool FullSand(ushort tile) => tile is 0x2c or 0x2d or 0x2e or 0x2f or 0x36 or 0x42 or 0x57
        or >= 0x65 and <= 0x68; }

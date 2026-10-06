namespace Yipe.Core;

public readonly record struct MonsterPlacement(ushort PlacementIndex, int TypeId, MapPosition Position,
    int Slot, int EncounterSize, int CompanionType1, int CompanionType2, bool RespawnsAfterMapReload, bool OccupiesTwoByTwoTiles)
{
    public bool Contains(MapPosition cell)
    {
        var width = OccupiesTwoByTwoTiles ? 2 : 1;
        return cell.X >= Position.X && cell.X < Position.X + width && cell.Y >= Position.Y && cell.Y < Position.Y + width;
    }
}

// Recovered authored records from src/generated/original_monsters.cpp (stable save IDs).
public static class MonsterPlacements
{
    public const int Count = 186;
    public static MonsterPlacement[] ForMap(int mapId, int difficulty = 2, int playerLevel = 1) => mapId switch
    {
        6 => [
            new(0, 3, new(5, 5), 0, 1, 0, 0, false, false),
        ],
        8 => [
            new(1, 17, new(6, 8), 0, 2, 0, 0, false, false),
            new(2, 17, new(9, 3), 1, difficulty, 0, 0, false, false),
            new(3, 17, new(8, 6), 2, 2, 0, 0, false, false),
            new(4, 17, new(10, 6), 3, difficulty, 0, 0, false, false),
        ],
        11 => [
            new(5, 2, new(2, 8), 0, 1, 0, 0, false, false),
        ],
        12 => [
            new(6, 1, new(7, 5), 0, 2, 0, 0, false, false),
            new(7, 1, new(7, 6), 1, 2, 0, 0, false, false),
        ],
        13 => [
            new(8, 19, new(8, 4), 0, difficulty, 0, 0, false, false),
        ],
        19 => [
            new(9, 15, new(3, 4), 0, difficulty, 0, 0, false, false),
            new(10, 15, new(3, 5), 1, difficulty, 0, 0, false, false),
            new(11, 15, new(4, 3), 2, 2, 0, 0, false, false),
            new(12, 15, new(4, 4), 3, 2, 0, 0, false, false),
        ],
        20 => [
            new(13, 17, new(7, 5), 0, 1, 0, 0, false, false),
            new(14, 17, new(6, 6), 1, 0, 0, 0, true, false),
            new(15, 17, new(7, 6), 2, 0, 0, 0, false, false),
            new(16, 17, new(9, 7), 3, 0, 0, 0, true, false),
            new(17, 17, new(10, 4), 4, 1, 0, 0, true, false),
        ],
        22 => [
            new(18, 7, new(5, 7), 0, 3, 0, 0, true, false),
            new(19, 9, new(6, 7), 1, difficulty, 0, 0, false, false),
            new(20, 9, new(7, 7), 2, difficulty, 0, 0, false, false),
        ],
        23 => [
            new(21, 7, new(10, 6), 0, 2, 0, 0, false, false),
            new(22, 7, new(10, 7), 1, 2, 0, 0, false, false),
        ],
        29 => [
            new(23, 9, new(8, 7), 0, 2, 0, 0, false, false),
            new(24, 9, new(9, 7), 1, 3, 0, 0, false, false),
        ],
        30 => [
            new(25, 8, new(11, 2), 0, 1, 0, 0, false, false),
        ],
        31 => [
            new(26, 11, new(3, 3), 0, 2, 0, 0, false, false),
            new(27, 11, new(10, 6), 1, 1, 0, 0, false, false),
        ],
        32 => [
            new(28, 16, new(2, 5), 0, difficulty, 0, 0, false, false),
            new(29, 16, new(10, 6), 1, difficulty, 0, 0, false, false),
        ],
        34 => [
            new(30, 25, new(5, 8), 0, difficulty, 0, 0, false, false),
        ],
        41 => [
            new(31, 21, new(2, 3), 0, 2, 0, 0, false, false),
            new(32, 22, new(10, 5), 1, 1, 0, 0, false, false),
        ],
        42 => [
            new(33, 21, new(11, 4), 0, 3, 0, 0, false, false),
            new(34, 22, new(5, 10), 1, 1, 0, 0, false, false),
        ],
        43 => [
            new(35, 32, new(9, 8), 0, 1, 0, 0, false, false),
            new(36, 30, new(5, 10), 1, 2, 0, 0, false, false),
        ],
        47 => [
            new(37, 21, new(6, 8), 0, 2, 0, 0, false, false),
        ],
        48 => [
            new(38, 31, new(7, 4), 0, difficulty, 0, 0, false, false),
            new(39, 19, new(10, 7), 1, 3, 0, 0, false, false),
        ],
        49 => [
            new(40, 31, new(9, 4), 0, 2, 0, 0, false, false),
        ],
        50 => [
            new(41, 25, new(11, 4), 0, 1, 0, 0, false, false),
            new(42, 25, new(1, 4), 1, 1, 0, 0, false, false),
        ],
        52 => [
            new(43, 8, new(2, 2), 0, 3, 0, 0, false, false),
            new(44, 8, new(8, 8), 1, 2, 0, 0, false, false),
            new(45, 10, new(10, 3), 2, 2, 0, 0, false, false),
            new(46, 11, new(11, 5), 3, 1, 0, 0, false, false),
        ],
        53 => [
            new(47, 21, new(4, 10), 0, 1, 0, 0, false, false),
            new(48, 21, new(2, 5), 1, 1, 0, 0, false, false),
        ],
        54 => [
            new(49, 30, new(11, 7), 0, 2, 0, 0, false, false),
            new(50, 31, new(1, 9), 1, 1, 0, 0, false, false),
        ],
        55 => [
            new(51, 30, new(4, 3), 0, 1, 0, 0, false, false),
            new(52, 30, new(7, 6), 1, 1, 0, 0, false, false),
        ],
        56 => [
            new(53, 26, new(2, 7), 0, 1, 0, 0, false, false),
            new(54, 25, new(5, 5), 1, difficulty, 0, 0, false, false),
        ],
        57 => [
            new(55, 1, new(5, 4), 0, 1, 0, 0, false, false),
            new(56, (playerLevel < 7 ? 1 : 5), new(10, 2), 1, 1, 0, 0, true, false),
            new(57, 2, new(11, 5), 2, 1, 0, 0, false, false),
            new(58, 2, new(2, 10), 3, 1, 0, 0, false, false),
        ],
        58 => [
            new(59, 8, new(5, 1), 0, 1, 0, 0, false, false),
            new(60, 8, new(7, 10), 1, 2, 0, 0, false, false),
            new(61, 8, new(11, 9), 2, 1, 0, 0, true, false),
        ],
        59 => [
            new(62, 15, new(4, 10), 0, difficulty, 0, 0, false, false),
            new(63, 16, new(6, 10), 1, difficulty, 0, 0, true, false),
            new(64, 9, new(1, 7), 2, 3, 0, 0, false, false),
        ],
        62 => [
            new(65, 27, new(10, 6), 0, 1, 0, 0, false, false),
            new(66, 26, new(3, 8), 1, 1, 0, 0, false, false),
        ],
        63 => [
            new(67, 2, new(7, 2), 0, 2, 0, 0, false, false),
            new(68, 2, new(6, 8), 1, 2, 0, 0, false, false),
        ],
        64 => [
            new(69, 11, new(3, 6), 0, 1, 0, 0, false, false),
            new(70, 11, new(9, 8), 1, 2, 0, 0, false, false),
        ],
        65 => [
            new(71, 13, new(1, 1), 0, 1, 0, 0, false, false),
            new(72, 13, new(1, 1), 1, 1, 0, 0, true, false),
        ],
        66 => [
            new(73, 8, new(1, 10), 0, 1, 0, 0, true, false),
            new(74, 7, new(1, 1), 1, difficulty, 0, 0, true, false),
        ],
        67 => [
            new(75, 16, new(6, 1), 0, 1, 0, 0, false, false),
            new(76, 16, new(10, 1), 1, 1, 0, 0, false, false),
            new(77, 14, new(8, 2), 2, 2, 0, 0, true, false),
        ],
        69 => [
            new(78, 2, new(3, 6), 0, 3, 0, 0, false, false),
            new(79, 3, new(9, 8), 1, 1, 0, 0, false, false),
        ],
        70 => [
            new(80, 11, new(3, 10), 0, 3, 0, 0, false, false),
            new(81, 11, new(5, 8), 1, 2, 0, 0, false, false),
        ],
        71 => [
            new(82, 13, new(5, 7), 0, 1, 0, 0, false, false),
        ],
        72 => [
            new(83, 13, new(2, 5), 0, 1, 0, 0, false, false),
            new(84, 13, new(10, 5), 1, 1, 0, 0, false, false),
            new(85, 13, new(5, 10), 2, 1, 0, 0, true, false),
        ],
        73 => [
            new(86, 13, new(9, 9), 0, 2, 0, 0, false, false),
            new(87, 13, new(9, 10), 1, 2, 0, 0, false, false),
        ],
        74 => [
            new(88, 27, new(11, 7), 0, 2, 0, 0, false, false),
            new(89, 27, new(8, 10), 1, 2, 0, 0, true, false),
        ],
        75 => [
            new(90, 3, new(10, 8), 0, 1, 0, 0, true, false),
            new(91, 3, new(4, 4), 1, 2, 0, 0, false, false),
            new(92, 3, new(2, 10), 2, difficulty, 0, 0, false, false),
            new(93, 5, new(1, 3), 3, 1, 0, 0, false, false),
        ],
        76 => [
            new(94, 35, new(8, 5), 0, 3, 8, 8, false, true),
            new(95, 12, new(8, 8), 1, 1, 0, 0, false, false),
        ],
        81 => [
            new(96, 22, new(6, 9), 0, 3, 0, 0, false, false),
            new(97, 23, new(9, 5), 1, 1, 0, 0, false, false),
        ],
        82 => [
            new(98, 22, new(6, 7), 0, 2, 0, 0, false, false),
            new(99, 23, new(2, 5), 1, 1, 0, 0, false, false),
        ],
        83 => [
            new(100, 36, new(8, 1), 0, 3, 31, 30, false, true),
            new(101, 30, new(1, 9), 1, 3, 0, 0, false, false),
        ],
        84 => [
            new(102, 33, new(2, 8), 0, 1, 0, 0, true, false),
            new(103, 32, new(10, 7), 1, 2, 0, 0, false, false),
        ],
        85 => [
            new(104, 33, new(6, 10), 0, 1, 0, 0, false, false),
        ],
        86 => [
            new(105, 33, new(6, 2), 0, 2, 0, 0, false, false),
            new(106, 34, new(4, 5), 1, 1, 0, 0, false, false),
            new(107, 31, new(9, 1), 2, 3, 0, 0, false, false),
            new(108, 32, new(1, 9), 3, difficulty, 0, 0, false, false),
        ],
        87 => [
            new(109, 23, new(6, 3), 0, 2, 0, 0, false, false),
            new(110, 23, new(10, 5), 1, 2, 0, 0, false, false),
            new(111, 23, new(10, 7), 2, 2, 0, 0, false, false),
        ],
        88 => [
            new(112, 22, new(7, 8), 0, 3, 0, 0, false, false),
            new(113, 22, new(7, 9), 1, 3, 0, 0, false, false),
        ],
        89 => [
            new(114, 32, new(3, 7), 0, 1, 0, 0, false, false),
        ],
        90 => [
            new(115, 39, new(5, 3), 0, 3, 25, 28, false, true),
            new(116, 29, new(1, 5), 1, 2, 0, 0, false, false),
        ],
        91 => [
            new(117, 37, new(10, 5), 0, 3, 28, 33, false, true),
        ],
        92 => [
            new(118, 38, new(1, 4), 0, 3, 30, 34, false, true),
            new(119, 30, new(10, 5), 1, difficulty, 0, 0, false, false),
            new(120, 34, new(6, 4), 2, 1, 0, 0, false, false),
        ],
        93 => [
            new(121, 24, new(6, 8), 0, 2, 0, 0, false, false),
            new(122, 24, new(7, 3), 1, 1, 0, 0, false, false),
        ],
        94 => [
            new(123, 24, new(8, 6), 0, 1, 0, 0, false, false),
            new(124, 24, new(1, 7), 1, 1, 0, 0, false, false),
        ],
        95 => [
            new(125, 37, new(4, 1), 0, 3, 18, (difficulty == 3 ? 20 : 16), false, true),
            new(126, 20, new(4, 4), 1, difficulty, 0, 0, false, false),
        ],
        96 => [
            new(127, 29, new(10, 6), 0, 1, 0, 0, false, false),
            new(128, 29, new(1, 3), 1, 2, 0, 0, false, false),
        ],
        97 => [
            new(129, 40, new(9, 7), 0, 3, 34, 33, false, true),
            new(130, 34, new(8, 10), 1, difficulty, 0, 0, false, false),
            new(131, 31, new(8, 2), 2, 3, 0, 0, false, false),
            new(132, 33, new(5, 6), 3, 2, 0, 0, false, false),
        ],
        98 => [
            new(133, 38, new(8, 6), 0, 3, 31, 32, false, true),
            new(134, 34, new(4, 6), 1, 2, 0, 0, false, false),
            new(135, 33, new(2, 1), 2, 1, 0, 0, false, false),
        ],
        99 => [
            new(136, 38, new(5, 6), 0, 3, 21, 24, false, true),
            new(137, 24, new(3, 4), 1, difficulty, 0, 0, false, false),
            new(138, 24, new(11, 5), 2, 3, 0, 0, false, false),
        ],
        100 => [
            new(139, 35, new(7, 9), 0, 3, 13, 13, false, true),
            new(140, 18, new(7, 7), 1, difficulty, 0, 0, false, false),
        ],
        101 => [
            new(141, 18, new(1, 4), 0, 2, 0, 0, false, false),
            new(142, 20, new(3, 1), 1, 1, 0, 0, false, false),
            new(143, 20, new(11, 3), 2, 1, 0, 0, false, false),
        ],
        102 => [
            new(144, 29, new(11, 3), 0, 1, 0, 0, false, false),
            new(145, 29, new(1, 4), 1, 1, 0, 0, false, false),
        ],
        103 => [
            new(146, 7, new(3, 4), 0, 1, 0, 0, true, false),
        ],
        105 => [
            new(147, 37, new(3, 4), 0, 3, 13, 20, false, true),
            new(148, 20, new(5, 2), 1, difficulty, 0, 0, true, false),
        ],
        106 => [
            new(149, 36, new(4, 9), 0, 3, 12, 8, false, true),
            new(150, 23, new(11, 1), 1, 1, 0, 0, true, false),
            new(151, 18, new(5, 5), 2, 1, 0, 0, true, false),
            new(152, 18, new(5, 7), 3, difficulty, 0, 0, false, false),
        ],
        107 => [
            new(153, 18, new(7, 5), 0, 1, 0, 0, false, false),
            new(154, 18, new(6, 8), 1, 1, 0, 0, false, false),
        ],
        108 => [
            new(155, 28, new(5, 7), 0, 1, 0, 0, false, false),
        ],
        109 => [
            new(156, 35, new(5, 5), 0, 3, difficulty, difficulty, false, true),
            new(157, 7, new(10, 8), 1, difficulty, 0, 0, false, false),
        ],
        110 => [
            new(158, 36, new(6, 4), 0, 3, 12, (difficulty == 3 ? 12 : 0), false, true),
            new(159, 12, new(8, 9), 1, 2, 0, 0, false, false),
            new(160, 12, new(1, 7), 2, difficulty, 0, 0, false, false),
            new(161, 12, new(4, 9), 3, 2, 0, 0, false, false),
        ],
        111 => [
            new(162, 13, new(1, 4), 0, 2, 0, 0, false, false),
            new(163, 14, new(7, 1), 1, 1, 0, 0, false, false),
        ],
        112 => [
            new(164, 14, new(6, 1), 0, 1, 0, 0, true, false),
            new(165, 14, new(6, 2), 1, 2, 0, 0, false, false),
        ],
        113 => [
            new(166, 14, new(6, 9), 0, 1, 0, 0, false, false),
            new(167, 14, new(6, 10), 1, 1, 0, 0, false, false),
        ],
        114 => [
            new(168, 27, new(7, 6), 0, 3, 0, 0, false, false),
            new(169, 28, new(3, 6), 1, 1, 0, 0, false, false),
        ],
        115 => [
            new(170, 3, new(6, 8), 0, 3, 0, 0, false, false),
            new(171, 5, new(9, 5), 1, 1, 0, 0, false, false),
            new(172, 7, new(6, 2), 2, 1, 0, 0, false, false),
        ],
        116 => [
            new(173, 12, new(8, 6), 0, 2, 0, 0, false, false),
            new(174, 11, new(1, 4), 1, 3, 0, 0, false, false),
        ],
        117 => [
            new(175, HellMonsterType(playerLevel), new(4, 7), 0, difficulty, 0, 0, true, false),
            new(176, HellMonsterType(playerLevel), new(8, 7), 1, difficulty, 0, 0, true, false),
            new(177, 15, new(2, 1), 2, difficulty, 0, 0, true, false),
        ],
        118 => [
            new(178, 32, new(11, 1), 0, difficulty, 0, 0, false, false),
        ],
        119 => [
            new(179, 33, new(11, 1), 0, difficulty, 0, 0, false, false),
        ],
        120 => [
            new(180, 34, new(11, 1), 0, difficulty, 0, 0, false, false),
        ],
        121 => [
            new(181, difficulty + 35, new(10, 7), 0, 3, 29, 12, false, true),
            new(182, 30, new(1, 8), 1, 2, 0, 0, false, false),
            new(183, 30, new(8, 8), 2, 2, 0, 0, false, false),
        ],
        127 => [
            new(184, difficulty + 36, new(6, 5), 0, 3, 23, 16, false, true),
            new(185, 41, new(4, 7), 1, 1, 0, 0, false, false),
        ],
        _ => [],
    };
    private static int HellMonsterType(int level) => level >= 21 ? 20 : level is 17 or 19 ? 18 : Math.Max(level, 3);
}

namespace Yipe.Core;

/// <summary>Original sound IDs and shared monster/spell aliases from src/audio.cpp.</summary>
public static class AudioCatalog
{
    public static IReadOnlyDictionary<int, string> Effects { get; } = new Dictionary<int, string>
    {
        [12] = "MONSTER_DEATH-1033.wav",
        [14] = "POTION-1033.wav",
        [35] = "DOT_RACING-1033.wav",
        [40] = "INVIS_ABILITY-1033.wav",
        [50] = "BEE_STING-1033.wav",
        [128] = "128_DEATH-1033.wav",
        [129] = "129_APPLAUSE-1033.wav",
        [130] = "130_HIT-1033.wav",
        [131] = "131_DOOR_OPEN-1033.wav",
        [133] = "133_EVIL-1033.wav",
        [134] = "134_MON_HIT-1033.wav",
        [135] = "135_TRAP-1033.wav",
        [136] = "136_ENRAGE-1033.wav",
        [137] = "137_SPIKE-1033.wav",
        [138] = "138_EATING-1033.wav",
        [140] = "140_CASTLE-1033.wav",
        [141] = "141_TRIGGER-1033.wav",
        [142] = "142_WARP-1033.wav",
        [143] = "143_DRINKING-1033.wav",
        [144] = "144_FIREWALL-1033.wav",
        [145] = "145_COINS-1033.wav",
        [147] = "147_DART_HIT-1033.wav",
        [148] = "148_DART_THROW-1033.wav",
        [151] = "151_FRIGHTEN-1033.wav",
        [152] = "152_STARTUP-1033.wav",
        [155] = "155_TURKEYEATING-1033.wav",
        [199] = "199_END_STINK-1033.wav",
        [201] = "201_RETURN_SURFACE-1033.wav",
        [202] = "202_HEAL-1033.wav",
        [203] = "203_GOBBLE_SPELL-1033.wav",
        [204] = "204_INSECT_SPELL-1033.wav",
        [205] = "205_STINK_SPRAY-1033.wav",
        [206] = "206_FULL_HEAL-1033.wav",
        [207] = "207_LARGEATTACK-1033.wav",
        [208] = "208_HPSHUFFLE-1033.wav",
        [209] = "209_RABBIT_SPELL-1033.wav",
        [210] = "210_COMBO_SPELL-1033.wav",
    }.AsReadOnly();
    public static int MonsterFile(int type) => type switch
    {
        10 or 11 => 8, 22 => 21, 26 or 27 => 25, 39 => 35, 38 => 40, _ => type,
    };
    private static readonly int[] SpellIds = [201, 202, 203, 204, 205, 206, 207, 206, 201, 210, 208, 209];
    public static int Spell(int index) => SpellIds[index];
    public static int Ability(SpecialAbility ability) => ability switch
    {
        SpecialAbility.Backstab => 130, SpecialAbility.Frighten => 151,
        SpecialAbility.Invisibility => 40, SpecialAbility.Enrage => 136, _ => 0,
    };
}

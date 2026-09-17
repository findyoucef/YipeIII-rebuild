#include "yipe/audio.hpp"

#include <stdexcept>

namespace yipe {
namespace {

constexpr std::array<SoundEffect, 37> effects{{
    {12, "MONSTER_DEATH-1033.wav"},
    {14, "POTION-1033.wav"},
    {35, "DOT_RACING-1033.wav"},
    {40, "INVIS_ABILITY-1033.wav"},
    {50, "BEE_STING-1033.wav"},
    {128, "128_DEATH-1033.wav"},
    {129, "129_APPLAUSE-1033.wav"},
    {130, "130_HIT-1033.wav"},
    {131, "131_DOOR_OPEN-1033.wav"},
    {133, "133_EVIL-1033.wav"},
    {134, "134_MON_HIT-1033.wav"},
    {135, "135_TRAP-1033.wav"},
    {136, "136_ENRAGE-1033.wav"},
    {137, "137_SPIKE-1033.wav"},
    {138, "138_EATING-1033.wav"},
    {140, "140_CASTLE-1033.wav"},
    {141, "141_TRIGGER-1033.wav"},
    {142, "142_WARP-1033.wav"},
    {143, "143_DRINKING-1033.wav"},
    {144, "144_FIREWALL-1033.wav"},
    {145, "145_COINS-1033.wav"},
    {147, "147_DART_HIT-1033.wav"},
    {148, "148_DART_THROW-1033.wav"},
    {151, "151_FRIGHTEN-1033.wav"},
    {152, "152_STARTUP-1033.wav"},
    {155, "155_TURKEYEATING-1033.wav"},
    {199, "199_END_STINK-1033.wav"},
    {201, "201_RETURN_SURFACE-1033.wav"},
    {202, "202_HEAL-1033.wav"},
    {203, "203_GOBBLE_SPELL-1033.wav"},
    {204, "204_INSECT_SPELL-1033.wav"},
    {205, "205_STINK_SPRAY-1033.wav"},
    {206, "206_FULL_HEAL-1033.wav"},
    {207, "207_LARGEATTACK-1033.wav"},
    {208, "208_HPSHUFFLE-1033.wav"},
    {209, "209_RABBIT_SPELL-1033.wav"},
    {210, "210_COMBO_SPELL-1033.wav"},
}};

} // namespace

const std::array<SoundEffect, 37>& sound_effects() {
    return effects;
}

std::optional<std::string_view> sound_asset_name(int sound_id) {
    for (const SoundEffect& effect : effects) {
        if (effect.id == sound_id) {
            return effect.asset_name;
        }
    }
    return std::nullopt;
}

int monster_sound_file_id(int monster_type) {
    switch (monster_type) {
    case 10:
    case 11:
        return 8;
    case 22:
        return 21;
    case 26:
    case 27:
        return 25;
    case 39:
        return 35;
    case 38:
        return 40;
    default:
        return monster_type;
    }
}

int spell_sound_id(std::size_t spell) {
    constexpr std::array<int, 12> ids{
        201, 202, 203, 204, 205, 206, 207, 206, 201, 210, 208, 209,
    };
    if (spell >= ids.size()) {
        throw std::out_of_range("spell index is out of range");
    }
    return ids[spell];
}

int ability_sound_id(SpecialAbility ability) {
    switch (ability) {
    case SpecialAbility::none:
        return 0;
    case SpecialAbility::backstab:
        return 130;
    case SpecialAbility::frighten:
        return 151;
    case SpecialAbility::invisibility:
        return 40;
    case SpecialAbility::enrage:
        return 136;
    }
    return 0;
}

} // namespace yipe

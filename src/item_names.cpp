#include "yipe/item_names.hpp"

#include <algorithm>
#include <cctype>
#include <fstream>
#include <string_view>
#include <vector>

namespace yipe {
namespace {

constexpr std::array<std::string_view, ItemNames::armor_count> armor_fallback = {
    "T-Shirt", "Robe", "Cloth Armor", "Leather Jacket", "Leather Armor",
    "Padded Armor", "Magician's Robe", "Chainmail", "Scale Armor", "Platemail",
    "Heavy Platemail", "Mithril Mail", "Enhanced Chainmail", "Fighter's Armor",
    "Magic Scale Armor", "Magic Chainmail", "Magic Platemail", "Glow Plate",
    "Spiked Scale Armor", "Spiked Chainmail", "Spiked Platemail", "Royal Knight's Armor",
    "Majestic Chainmail", "Majestic Platemail", "King's Armor", "Yipe! Magic Armor",
    "Armor of Maynard", "Holy Yipe! Armor", "Quizilog Platemail", "Dragonscale Armor",
};

constexpr std::array<std::string_view, ItemNames::weapon_count> weapon_fallback = {
    "Hands", "Small Dagger", "Sling", "Dull Sword", "Axe", "Short Sword",
    "Black Mace", "Shiny Sword", "Flail", "Broadsword", "Spear", "Nunchucks",
    "Mystic Sword", "Two-Handed Axe", "Bow and Arrows", "Longsword",
    "Two-Handed Sword", "Jeweled Dagger", "Magic Mace", "Giant Sword", "Magic Spear",
    "Enhanced Spear", "Crossbow and Bolts", "Sai Swords", "Dragonsword", "Mystic Mace",
    "Magic Yipe! Flail", "Magic Yipe! Sword", "Deathsword", "Maynard Light Sword",
    "Quizilog Mystic Sword", "Fire Battle Axe", "Razor Boomerangs", "King's Sword",
    "Frost Sword", "Yipe! Deathblade", "Manynard Power Sword", "Almighty Longsword",
};

void trim(std::string& value) {
    const auto not_space = [](unsigned char character) { return !std::isspace(character); };
    value.erase(value.begin(), std::find_if(value.begin(), value.end(), not_space));
    value.erase(std::find_if(value.rbegin(), value.rend(), not_space).base(), value.end());
}

} // namespace

ItemNames embedded_item_names() {
    ItemNames result;
    result.used_embedded_fallback = true;
    for (std::size_t index = 0; index < result.armor.size(); ++index) {
        result.armor[index] = armor_fallback[index];
    }
    for (std::size_t index = 0; index < result.weapons.size(); ++index) {
        result.weapons[index] = weapon_fallback[index];
    }
    return result;
}

ItemNames load_item_names(const std::filesystem::path& path) {
    std::ifstream input(path);
    if (!input) {
        return embedded_item_names();
    }

    std::vector<std::string> names;
    std::string line;
    while (std::getline(input, line) && names.size() < ItemNames::armor_count + ItemNames::weapon_count) {
        trim(line);
        if (line.empty() || line.find('#') != std::string::npos) {
            continue;
        }
        line.resize(std::min(line.size(), ItemNames::maximum_name_length));
        names.push_back(std::move(line));
    }

    if (names.size() != ItemNames::armor_count + ItemNames::weapon_count) {
        return embedded_item_names();
    }

    ItemNames result;
    for (std::size_t index = 0; index < result.armor.size(); ++index) {
        result.armor[index] = names[index];
    }
    for (std::size_t index = 0; index < result.weapons.size(); ++index) {
        result.weapons[index] = names[result.armor.size() + index];
    }
    return result;
}

} // namespace yipe

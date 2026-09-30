#include "yipe/shop.hpp"
#include "yipe/spell.hpp"

#include <algorithm>
#include <array>
#include <stdexcept>

namespace yipe {

bool enter_miscellaneous_shop(Player& player) {
    const bool price_increase = player.miscellaneous_shop_stage == 0
        && static_cast<std::int64_t>(player.strength) + player.dexterity + player.intelligence
            > 60 - static_cast<std::int64_t>(player.difficulty);
    if (price_increase) player.miscellaneous_shop_stage = 1;
    if (player.miscellaneous_shop_stage == 1 && player.level > 25) {
        player.miscellaneous_shop_stage = 2;
    }
    return price_increase;
}

std::array<ShopStockItem, 11> miscellaneous_shop_stock(const Player& player, int map_id) {
    std::array<ShopStockItem, 11> stock;
    switch (map_id) {
    case 0x11:
        stock = {{
            {"French Fries", 75, ShopItemKind::consumable, 1, 0},
            {"Juicy Cheeseburger", 250, ShopItemKind::consumable, 2, 0},
            {"Strength Potion", 1000, ShopItemKind::consumable, 6, 0},
            {"Intelligence Potion", 1000, ShopItemKind::consumable, 8, 0},
            {"Potion of HP Increase", 2000, ShopItemKind::consumable, 9, 0},
            {"Potion of SP Increase", 2500, ShopItemKind::consumable, 10, 0},
            {"Return to Surface Potion", 250, ShopItemKind::consumable, 5, 0},
            {"Wet Mittens", 50, ShopItemKind::equipment, 2, 1},
            {"Cheap Ring", 100, ShopItemKind::equipment, 3, 1},
            {"Sandals", 50, ShopItemKind::equipment, 4, 1},
            {"Plain Necklace", 75, ShopItemKind::equipment, 7, 1},
        }};
        break;
    case 0x1b:
        stock = {{
            {"French Fries", 80, ShopItemKind::consumable, 1, 0},
            {"Juicy Cheeseburger", 275, ShopItemKind::consumable, 2, 0},
            {"Intelligence Potion", 1000, ShopItemKind::consumable, 8, 0},
            {"Dexterity Potion", 900, ShopItemKind::consumable, 7, 0},
            {"Potion of HP Increase", 2100, ShopItemKind::consumable, 9, 0},
            {"Stink Potion", 1200, ShopItemKind::consumable, 4, 0},
            {"Return to Surface Potion", 200, ShopItemKind::consumable, 5, 0},
            {"Working Gloves", 1500, ShopItemKind::equipment, 2, 3},
            {"Platinum Ring", 3000, ShopItemKind::equipment, 3, 3},
            {"Strong Hiking Boots", 2500, ShopItemKind::equipment, 4, 4},
            {"Gold Medallion", 500, ShopItemKind::equipment, 7, 2},
        }};
        break;
    case 0x08:
        stock = {{
            {"French Fries", 75, ShopItemKind::consumable, 1, 0},
            {"Juicy Cheeseburger", 245, ShopItemKind::consumable, 2, 0},
            {"Intelligence Potion", 800, ShopItemKind::consumable, 8, 0},
            {"Potion of HP Increase", 1900, ShopItemKind::consumable, 9, 0},
            {"Potion of SP Increase", 2750, ShopItemKind::consumable, 10, 0},
            {"Potion of SP Healing", 2000, ShopItemKind::consumable, 3, 0},
            {"Return to Surface Potion", 300, ShopItemKind::consumable, 5, 0},
            {"Double Horned Helmet", 10000, ShopItemKind::equipment, 1, 7},
            {"Leather Gloves", 2500, ShopItemKind::equipment, 2, 4},
            {"Protection Ring", 6000, ShopItemKind::equipment, 3, 5},
            {"Speedy Boots", 8000, ShopItemKind::equipment, 4, 6},
        }};
        break;
    default:
        stock = {{
            {"French Fries", 70, ShopItemKind::consumable, 1, 0},
            {"Juicy Cheeseburger", 240, ShopItemKind::consumable, 2, 0},
            {"Strength Potion", 900, ShopItemKind::consumable, 6, 0},
            {"Dexterity Potion", 1000, ShopItemKind::consumable, 7, 0},
            {"Potion of SP Increase", 2600, ShopItemKind::consumable, 10, 0},
            {"Stink Potion", 1000, ShopItemKind::consumable, 4, 0},
            {"Return to Surface Potion", 350, ShopItemKind::consumable, 5, 0},
            {"Shower Cap", 500, ShopItemKind::equipment, 1, 2},
            {"Wet Mittens", 25, ShopItemKind::equipment, 2, 1},
            {"Aluminum Ring", 1000, ShopItemKind::equipment, 3, 2},
            {"Fire Amulet", 5000, ShopItemKind::equipment, 7, 5},
        }};
        if (player.miscellaneous_shop_stage == 3
            || (player.miscellaneous_shop_stage == 2 && !player.has_rainbow_key)) {
            // Stage 3 lasts until the dialog closes. The original keeps the
            // purchase mapped to the key, even if the stock list is rebuilt.
            if (player.miscellaneous_shop_stage == 2) stock[8].name = "Rainbow Key";
            stock[8].type = 8;
            stock[8].tier = 5;
            stock[8].price = (player.difficulty + 6) * 500;
        }
        break;
    }
    if (player.miscellaneous_shop_stage > 0) {
        for (auto& item : stock) {
            if (item.kind == ShopItemKind::consumable && item.type >= 6 && item.type <= 8) {
                item.price += 1500;
            }
        }
    }
    return stock;
}

PurchaseResult purchase_shop_item(Player& player, const ShopStockItem& item) {
    if (player.gold < item.price) {
        return {PurchaseStatus::insufficient_gold};
    }

    if (item.kind == ShopItemKind::consumable) {
        const auto end = player.inventory.begin()
            + static_cast<std::ptrdiff_t>(Player::carried_inventory_size);
        const auto slot = std::find(player.inventory.begin(), end, 0);
        if (slot == end) {
            return {PurchaseStatus::inventory_full};
        }
        player.gold -= item.price;
        *slot = static_cast<std::uint16_t>(item.type);
        return {PurchaseStatus::purchased,
                static_cast<std::size_t>(slot - player.inventory.begin())};
    }

    std::uint16_t* equipped = nullptr;
    switch (item.type) {
    case 1:
        equipped = &player.helmet_rank;
        break;
    case 2:
        equipped = &player.gloves_rank;
        break;
    case 3:
        equipped = &player.ring_rank;
        break;
    case 4:
        equipped = &player.boots_rank;
        break;
    case 7:
        equipped = &player.necklace_rank;
        break;
    case 8:
        if (player.has_rainbow_key) return {PurchaseStatus::already_equipped};
        player.has_rainbow_key = true;
        player.miscellaneous_shop_stage = 3;
        player.gold -= item.price;
        return {PurchaseStatus::purchased};
    default:
        throw std::logic_error("unsupported shop equipment type");
    }
    if (*equipped >= item.tier) {
        return {PurchaseStatus::already_equipped};
    }
    player.gold -= item.price;
    *equipped = static_cast<std::uint16_t>(item.tier);
    return {PurchaseStatus::purchased};
}

namespace {

constexpr std::array<int, 39> weapon_prices{
    0, 0, 50, 110, 220, 350, 500, 700, 900, 1100, 1400, 1700, 2000,
    2400, 2800, 3300, 3800, 4300, 4900, 6600, 7800, 9500, 11000,
    12500, 14250, 16150, 18100, 20100, 22000, 24000, 26500, 30000,
    32700, 35000, 40000, 44000, 48500, 53000, 55000,
};

constexpr std::array<int, 31> armor_prices{
    0, 0, 40, 75, 150, 300, 425, 600, 800, 1100, 1300, 1444, 1575,
    2000, 2500, 3300, 3800, 4300, 4900, 6600, 8000, 12500, 13000,
    14000, 15000, 16500, 18000, 19500, 21000, 22500, 24250,
};

constexpr std::array<int, 11> sequential_stock{
    2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12,
};
constexpr std::array<int, 11> town_weapons{
    2, 3, 4, 5, 6, 7, 8, 9, 10, 13, 16,
};
constexpr std::array<int, 11> cave_weapons{
    11, 12, 14, 15, 17, 18, 20, 22, 23, 24, 25,
};
constexpr std::array<int, 11> remote_weapons{
    19, 21, 26, 27, 28, 29, 30, 31, 33, 35, 36,
};
constexpr std::array<int, 11> town_armor{
    2, 3, 4, 5, 6, 7, 8, 10, 12, 13, 15,
};
constexpr std::array<int, 11> cave_armor{
    9, 11, 14, 16, 17, 18, 20, 22, 23, 25, 28,
};

int& equipped_rank(Player& player, EquipmentKind kind) {
    return kind == EquipmentKind::weapon ? player.weapon_rank : player.armor_rank;
}

int equipped_rank(const Player& player, EquipmentKind kind) {
    return kind == EquipmentKind::weapon ? player.weapon_rank : player.armor_rank;
}

} // namespace

int equipment_price(EquipmentKind kind, int rank) {
    if (rank < 0) {
        return 0;
    }
    const auto index = static_cast<std::size_t>(rank);
    if (kind == EquipmentKind::weapon) {
        return index < weapon_prices.size() ? weapon_prices[index] : 0;
    }
    return index < armor_prices.size() ? armor_prices[index] : 0;
}

std::array<EquipmentShopItem, 11> equipment_shop_stock(EquipmentKind kind, int map_id) {
    const std::array<int, 11>* ranks = &sequential_stock;
    if (kind == EquipmentKind::weapon) {
        ranks = map_id == 0x08 ? &remote_weapons
            : map_id == 0x11 ? &town_weapons
            : map_id == 0x1b ? &cave_weapons : &sequential_stock;
    } else if (map_id == 0x11) {
        ranks = &town_armor;
    } else if (map_id == 0x1b) {
        ranks = &cave_armor;
    }

    std::array<EquipmentShopItem, 11> result{};
    for (std::size_t index = 0; index < result.size(); ++index) {
        result[index] = {(*ranks)[index], equipment_price(kind, (*ranks)[index])};
    }
    return result;
}

int equipment_sale_value(const Player& player, EquipmentKind kind) {
    const int rank = equipped_rank(player, kind);
    return rank == 1 ? 0 : equipment_price(kind, rank) / 2;
}

bool equipment_sale_requires_confirmation(const Player& player, EquipmentKind kind) {
    const int rank = equipped_rank(player, kind);
    return rank != 1
           && player.gold + equipment_sale_value(player, kind)
               < equipment_price(kind, rank + 1);
}

EquipmentTransactionStatus purchase_equipment(Player& player, EquipmentKind kind,
                                               int rank) {
    if (equipped_rank(player, kind) >= 2) {
        return EquipmentTransactionStatus::must_sell_current;
    }
    const int price = equipment_price(kind, rank);
    if (price <= 0 || player.gold < price) {
        return EquipmentTransactionStatus::insufficient_gold;
    }
    player.gold -= price;
    equipped_rank(player, kind) = rank;
    return EquipmentTransactionStatus::purchased;
}

EquipmentTransactionStatus sell_equipment(Player& player, EquipmentKind kind) {
    int& rank = equipped_rank(player, kind);
    if (rank == 1) {
        return EquipmentTransactionStatus::nothing_to_sell;
    }
    player.gold += equipment_price(kind, rank) / 2;
    rank = 1;
    return EquipmentTransactionStatus::sold;
}

const std::array<HealingService, 4>& healing_services() {
    static constexpr std::array<HealingService, 4> services{{
        {"Tasty Apple", 5},
        {"Vegetables", 25},
        {"Pumpkin Pie", 60},
        {"Turkey Dinner", 125},
    }};
    return services;
}

ServiceResult purchase_healing(Player& player, std::size_t service,
                               MsvcRandom& random) {
    if (service >= healing_services().size()) {
        throw std::out_of_range("healing service is out of range");
    }
    const int price = healing_services()[service].price;
    if (player.gold < price) {
        return {ServiceStatus::insufficient_gold};
    }
    if (player.health >= player.maximum_health) {
        return {ServiceStatus::already_full};
    }

    const int bonus = player.level >= 18 ? 1 : 0;
    int amount = 0;
    switch (service) {
    case 0:
        amount = random.next() % 3 + 3 + bonus;
        break;
    case 1:
        amount = random.next() % 9 + 18 + bonus;
        break;
    case 2:
        amount = random.next() % 16 + 42 + bonus;
        break;
    case 3:
        amount = player.maximum_health - player.health;
        break;
    default:
        break;
    }
    player.health = std::min(player.health + amount, player.maximum_health);
    player.gold -= price;
    return {ServiceStatus::completed, amount};
}

ServiceResult purchase_spell_point_healing(Player& player, bool full,
                                           MsvcRandom& random) {
    const int price = full ? 250 : 50;
    if (player.gold < price) {
        return {ServiceStatus::insufficient_gold};
    }
    if (player.spell_points >= player.maximum_spell_points) {
        return {ServiceStatus::already_full};
    }

    int amount = player.maximum_spell_points - player.spell_points;
    if (!full) {
        const int level_bonus = player.level >= 36 ? 10
            : player.level >= 26 ? 6 : player.level >= 16 ? 3 : 1;
        amount = random.next() % 12 + 11 + level_bonus;
    }
    player.spell_points = std::min(player.spell_points + amount,
                                   player.maximum_spell_points);
    player.gold -= price;
    return {ServiceStatus::completed, amount};
}

std::optional<SpellOffer> next_spell_offer(const Player& player) {
    const auto& definitions = spell_definitions();
    for (std::size_t index = 0; index < definitions.size(); ++index) {
        if (player.spells[index] == 0) {
            const SpellDefinition& spell = definitions[index];
            const int required_level = spell_required_level(spell, player.difficulty);
            if (player.level < required_level) {
                return std::nullopt;
            }
            return SpellOffer{index, spell.name, spell.description,
                              spell.price, required_level};
        }
    }
    return std::nullopt;
}

SpellLearningStatus learn_next_spell(Player& player) {
    const std::optional<SpellOffer> offer = next_spell_offer(player);
    if (!offer) {
        return SpellLearningStatus::unavailable;
    }
    // The original handler uses a strict comparison here: exact gold is not enough.
    if (player.gold <= offer->price) {
        return SpellLearningStatus::insufficient_gold;
    }
    player.spells[offer->index] = 1;
    player.gold -= offer->price;
    return SpellLearningStatus::learned;
}

std::optional<AbilityOffer> ability_offer_for_environment(int environment) {
    switch (environment) {
    case 1:
        return AbilityOffer{SpecialAbility::backstab, "Backstab",
                            "Sneak around the first monster and stab it. Stronger monsters take more damage.",
                            150};
    case 2:
        return AbilityOffer{SpecialAbility::frighten, "Frighten",
                            "Scare away the third monster when fighting three monsters at once.",
                            250};
    case 4:
        return AbilityOffer{SpecialAbility::enrage, "Enrage",
                            "Briefly raise all skills and tend to deal more damage.", 400};
    case 7:
        return AbilityOffer{SpecialAbility::invisibility, "Invisibility",
                            "Briefly avoid monster attacks and gain a better chance to hit.",
                            400};
    default:
        return std::nullopt;
    }
}

AbilityTrainingStatus train_ability(Player& player, int environment) {
    const std::optional<AbilityOffer> offer = ability_offer_for_environment(environment);
    if (!offer) {
        return AbilityTrainingStatus::unavailable;
    }
    if (player.special_ability == offer->ability) {
        return AbilityTrainingStatus::already_trained;
    }
    if (player.gold < offer->price) {
        return AbilityTrainingStatus::insufficient_gold;
    }
    player.special_ability = offer->ability;
    player.gold -= offer->price;
    return AbilityTrainingStatus::learned;
}

} // namespace yipe

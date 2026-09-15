#include "yipe/shop.hpp"

#include <algorithm>
#include <stdexcept>

namespace yipe {

const std::array<ShopStockItem, 11>& freds_miscellaneous_stock() {
    static constexpr std::array<ShopStockItem, 11> stock{{
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

} // namespace yipe

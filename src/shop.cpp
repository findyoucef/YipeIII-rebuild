#include "yipe/shop.hpp"

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

} // namespace yipe

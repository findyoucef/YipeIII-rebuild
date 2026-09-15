#pragma once

#include "yipe/player.hpp"

#include <array>
#include <cstddef>
#include <string_view>

namespace yipe {

enum class ShopItemKind {
    consumable,
    equipment,
};

struct ShopStockItem {
    std::string_view name;
    int price;
    ShopItemKind kind;
    int type;
    int tier;
};

// Stock selected for map 0x11 by 0x00440e50 and priced by 0x00441010.
[[nodiscard]] const std::array<ShopStockItem, 11>& freds_miscellaneous_stock();

enum class PurchaseStatus {
    purchased,
    insufficient_gold,
    inventory_full,
    already_equipped,
};

struct PurchaseResult {
    PurchaseStatus status = PurchaseStatus::purchased;
    std::size_t inventory_slot = 0;
};

// Reconstructs Fred's purchase handling at 0x00441250 and equipment updates at
// 0x00440710. Only successful purchases deduct gold.
[[nodiscard]] PurchaseResult purchase_shop_item(Player& player,
                                                const ShopStockItem& item);

} // namespace yipe

#pragma once

#include <array>
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

} // namespace yipe

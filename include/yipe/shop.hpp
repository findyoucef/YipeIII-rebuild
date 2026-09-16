#pragma once

#include "yipe/player.hpp"

#include <array>
#include <cstddef>
#include <optional>
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

enum class EquipmentKind {
    weapon,
    armor,
};

struct EquipmentShopItem {
    int rank;
    int price;
};

enum class EquipmentTransactionStatus {
    purchased,
    sold,
    insufficient_gold,
    must_sell_current,
    nothing_to_sell,
};

// Reconstructs the map-dependent catalogs and price switches at 0x0044dfa0,
// 0x00401000, 0x00417830, and 0x004179a0.
[[nodiscard]] std::array<EquipmentShopItem, 11>
equipment_shop_stock(EquipmentKind kind, int map_id);
[[nodiscard]] int equipment_price(EquipmentKind kind, int rank);
[[nodiscard]] int equipment_sale_value(const Player& player, EquipmentKind kind);
[[nodiscard]] bool equipment_sale_requires_confirmation(const Player& player,
                                                        EquipmentKind kind);
[[nodiscard]] EquipmentTransactionStatus purchase_equipment(
    Player& player, EquipmentKind kind, int rank);
[[nodiscard]] EquipmentTransactionStatus sell_equipment(Player& player,
                                                        EquipmentKind kind);

struct HealingService {
    std::string_view name;
    int price;
};

enum class ServiceStatus {
    completed,
    insufficient_gold,
    already_full,
};

struct ServiceResult {
    ServiceStatus status = ServiceStatus::completed;
    int amount = 0;
};

[[nodiscard]] const std::array<HealingService, 4>& healing_services();
[[nodiscard]] ServiceResult purchase_healing(Player& player, std::size_t service,
                                             MsvcRandom& random);
[[nodiscard]] ServiceResult purchase_spell_point_healing(Player& player, bool full,
                                                         MsvcRandom& random);

struct SpellOffer {
    std::size_t index;
    std::string_view name;
    std::string_view description;
    int price;
    int required_level;
};

enum class SpellLearningStatus {
    learned,
    unavailable,
    insufficient_gold,
};

[[nodiscard]] std::optional<SpellOffer> next_spell_offer(const Player& player);
[[nodiscard]] SpellLearningStatus learn_next_spell(Player& player);

struct AbilityOffer {
    SpecialAbility ability;
    std::string_view name;
    std::string_view description;
    int price;
};

enum class AbilityTrainingStatus {
    learned,
    unavailable,
    already_trained,
    insufficient_gold,
};

// The original training center selects its lesson from the current map's
// environment value (0x004498c0).
[[nodiscard]] std::optional<AbilityOffer> ability_offer_for_environment(int environment);
[[nodiscard]] AbilityTrainingStatus train_ability(Player& player, int environment);

} // namespace yipe

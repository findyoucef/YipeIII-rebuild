#pragma once

#include <array>
#include <cstddef>
#include <cstdint>
#include <filesystem>
#include <optional>
#include <string_view>

namespace yipe {

struct OriginalAuxiliaryData;

enum class InputAction : std::size_t {
    northwest,
    north,
    northeast,
    west,
    east,
    southwest,
    south,
    southeast,
    attack,
    run,
    cast,
    use_item,
    special,
    count,
};

inline constexpr std::size_t input_action_count =
    static_cast<std::size_t>(InputAction::count);

using KeyBindings = std::array<std::uint16_t, input_action_count>;

[[nodiscard]] constexpr KeyBindings default_key_bindings() {
    // VK_NUMPAD7, Up, VK_NUMPAD9, Left, Right, VK_NUMPAD1, Down,
    // VK_NUMPAD3, then Attack, Run, Cast, Use Item, and Special.
    return {0x67, 0x26, 0x69, 0x25, 0x27, 0x61, 0x28,
            0x63, 0x41, 0x52, 0x43, 0x49, 0x53};
}

struct GameSettings {
    bool sound_enabled = true;
    KeyBindings keys = default_key_bindings();

    bool operator==(const GameSettings&) const = default;
};

[[nodiscard]] std::string_view input_action_name(InputAction action);
[[nodiscard]] std::string_view key_binding_name(std::uint16_t key);
[[nodiscard]] std::optional<InputAction> combat_action_for_key(
    const GameSettings& settings, std::uint16_t key);
[[nodiscard]] GameSettings settings_from_original(const OriginalAuxiliaryData& data);

void save_settings_atomic(const std::filesystem::path& path,
                          const GameSettings& settings);
[[nodiscard]] GameSettings load_settings(const std::filesystem::path& path);

} // namespace yipe

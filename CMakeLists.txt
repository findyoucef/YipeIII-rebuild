cmake_minimum_required(VERSION 3.20)

project(yipe3_rebuild VERSION 0.1.0 LANGUAGES CXX)

set(CMAKE_CXX_STANDARD 20)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_CXX_EXTENSIONS OFF)

find_package(SDL2 CONFIG REQUIRED)

add_library(yipe_core
    src/generated/original_maps.cpp
    src/generated/original_monster_definitions.cpp
    src/generated/original_monsters.cpp
    src/generated/original_weapon_damage.cpp
    src/audio.cpp
    src/auxiliary_data.cpp
    src/casino.cpp
    src/encounter.cpp
    src/item_names.cpp
    src/legacy_save.cpp
    src/map.cpp
    src/player.cpp
    src/quest.cpp
    src/random.cpp
    src/save.cpp
    src/settings.cpp
    src/shop.cpp
    src/spell.cpp
)
target_include_directories(yipe_core PUBLIC include)
target_compile_options(yipe_core PRIVATE
    $<$<CXX_COMPILER_ID:AppleClang,Clang,GNU>:-Wall;-Wextra;-Wpedantic;-Wconversion>
)

add_executable(yipe3 app/main.cpp)
target_link_libraries(yipe3 PRIVATE yipe_core SDL2::SDL2)
target_compile_options(yipe3 PRIVATE
    $<$<CXX_COMPILER_ID:AppleClang,Clang,GNU>:-Wall;-Wextra;-Wpedantic;-Wconversion>
)

set(YIPE_RUNTIME_ASSET_DIR "${CMAKE_CURRENT_BINARY_DIR}/assets")
file(MAKE_DIRECTORY "${YIPE_RUNTIME_ASSET_DIR}")
configure_file(assets/original/Yipe3Items.txt
               "${YIPE_RUNTIME_ASSET_DIR}/Yipe3Items.txt" COPYONLY)
foreach(YIPE_BITMAP_ID 133 134 135 136 137 138 139 140 141 142 143 144 145 146 147
                       153 326 377 379 391 392 393 418 460)
    configure_file(assets/embedded/bitmaps/${YIPE_BITMAP_ID}-1033.bmp
                   "${YIPE_RUNTIME_ASSET_DIR}/${YIPE_BITMAP_ID}-1033.bmp" COPYONLY)
endforeach()
foreach(YIPE_BITMAP_ID RANGE 406 417)
    configure_file(assets/embedded/bitmaps/${YIPE_BITMAP_ID}-1033.bmp
                   "${YIPE_RUNTIME_ASSET_DIR}/${YIPE_BITMAP_ID}-1033.bmp" COPYONLY)
endforeach()
foreach(YIPE_BITMAP_ID RANGE 174 214)
    configure_file(assets/embedded/bitmaps/${YIPE_BITMAP_ID}-1033.bmp
                   "${YIPE_RUNTIME_ASSET_DIR}/${YIPE_BITMAP_ID}-1033.bmp" COPYONLY)
endforeach()
file(GLOB YIPE_EMBEDDED_AUDIO CONFIGURE_DEPENDS
     "${CMAKE_CURRENT_SOURCE_DIR}/assets/embedded/audio/*.wav")
foreach(YIPE_AUDIO_FILE IN LISTS YIPE_EMBEDDED_AUDIO)
    get_filename_component(YIPE_AUDIO_NAME "${YIPE_AUDIO_FILE}" NAME)
    configure_file("${YIPE_AUDIO_FILE}"
                   "${YIPE_RUNTIME_ASSET_DIR}/audio/${YIPE_AUDIO_NAME}" COPYONLY)
endforeach()
file(GLOB YIPE_MONSTER_AUDIO CONFIGURE_DEPENDS
     "${CMAKE_CURRENT_SOURCE_DIR}/assets/original/MonsterSounds/*.wav")
foreach(YIPE_AUDIO_FILE IN LISTS YIPE_MONSTER_AUDIO)
    get_filename_component(YIPE_AUDIO_NAME "${YIPE_AUDIO_FILE}" NAME)
    configure_file("${YIPE_AUDIO_FILE}"
                   "${YIPE_RUNTIME_ASSET_DIR}/MonsterSounds/${YIPE_AUDIO_NAME}" COPYONLY)
endforeach()

enable_testing()
add_executable(yipe_core_tests tests/core_tests.cpp)
target_link_libraries(yipe_core_tests PRIVATE yipe_core)
target_compile_definitions(yipe_core_tests PRIVATE
    YIPE_TEST_ASSET_DIR="${CMAKE_CURRENT_SOURCE_DIR}/assets/original"
    YIPE_TEST_EVIDENCE_DIR="${CMAKE_CURRENT_SOURCE_DIR}/evidence/original-runtime"
)
add_test(NAME yipe_core_tests COMMAND yipe_core_tests)
add_test(NAME yipe_app_smoke COMMAND yipe3 --smoke-test)
set_tests_properties(yipe_app_smoke PROPERTIES
    ENVIRONMENT "SDL_VIDEODRIVER=dummy;SDL_AUDIODRIVER=dummy"
)

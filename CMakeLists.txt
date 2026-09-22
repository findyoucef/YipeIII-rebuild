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
if(APPLE)
    enable_language(OBJCXX)
    set(CMAKE_OBJCXX_STANDARD 20)
    set_target_properties(yipe3 PROPERTIES
        MACOSX_BUNDLE TRUE
        OUTPUT_NAME "Yipe III"
        MACOSX_BUNDLE_BUNDLE_NAME "Yipe III Rebuild"
        MACOSX_BUNDLE_GUI_IDENTIFIER "org.yipe3rebuild.game"
        MACOSX_BUNDLE_BUNDLE_VERSION "${PROJECT_VERSION}"
        MACOSX_BUNDLE_SHORT_VERSION_STRING "${PROJECT_VERSION}"
    )
    target_sources(yipe3 PRIVATE app/file_dialog_mac.mm)
    target_link_libraries(yipe3 PRIVATE "-framework AppKit"
                                        "-framework UniformTypeIdentifiers")
else()
    target_sources(yipe3 PRIVATE app/file_dialog_fallback.cpp)
endif()
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

if(APPLE)
    # SDL_GetBasePath resolves an application's Contents/Resources directory.
    file(GLOB_RECURSE YIPE_BUNDLE_ASSETS "${YIPE_RUNTIME_ASSET_DIR}/*")
    foreach(YIPE_ASSET IN LISTS YIPE_BUNDLE_ASSETS)
        file(RELATIVE_PATH YIPE_ASSET_RELATIVE "${YIPE_RUNTIME_ASSET_DIR}" "${YIPE_ASSET}")
        get_filename_component(YIPE_ASSET_SUBDIRECTORY "${YIPE_ASSET_RELATIVE}" DIRECTORY)
        set_source_files_properties("${YIPE_ASSET}" PROPERTIES
            MACOSX_PACKAGE_LOCATION "Resources/assets/${YIPE_ASSET_SUBDIRECTORY}")
    endforeach()
    target_sources(yipe3 PRIVATE ${YIPE_BUNDLE_ASSETS})
    set_source_files_properties(assets/licenses/SDL2-LICENSE.txt PROPERTIES
        MACOSX_PACKAGE_LOCATION Resources)
    target_sources(yipe3 PRIVATE assets/licenses/SDL2-LICENSE.txt)
    install(TARGETS yipe3 BUNDLE DESTINATION .)
    install(SCRIPT cmake/InstallMacBundle.cmake)
else()
    install(TARGETS yipe3 RUNTIME DESTINATION .)
    install(DIRECTORY "${YIPE_RUNTIME_ASSET_DIR}/" DESTINATION assets)
endif()
install(FILES README.md DESTINATION .)
install(DIRECTORY docs/ DESTINATION docs FILES_MATCHING PATTERN "*.md")
if(APPLE)
    install(PROGRAMS tools/verify_mac_package.py DESTINATION tools)
endif()

set(CPACK_PACKAGE_NAME "YipeIII-rebuild")
set(CPACK_GENERATOR TGZ)
set(CPACK_PACKAGE_DIRECTORY "${CMAKE_CURRENT_BINARY_DIR}/packages")
include(CPack)

enable_testing()
add_executable(yipe_core_tests tests/core_tests.cpp)
target_link_libraries(yipe_core_tests PRIVATE yipe_core)
target_compile_definitions(yipe_core_tests PRIVATE
    YIPE_TEST_ASSET_DIR="${CMAKE_CURRENT_SOURCE_DIR}/assets/original"
    YIPE_TEST_EVIDENCE_DIR="${CMAKE_CURRENT_SOURCE_DIR}/evidence/original-runtime"
)
add_test(NAME yipe_core_tests COMMAND yipe_core_tests)
add_test(NAME yipe_app_smoke COMMAND yipe3 --smoke-test)
add_executable(yipe_app_shortcut_tests tests/app_shortcut_tests.cpp)
target_link_libraries(yipe_app_shortcut_tests PRIVATE yipe_core SDL2::SDL2)
add_test(NAME yipe_app_shortcut_tests COMMAND yipe_app_shortcut_tests)
set_tests_properties(yipe_app_smoke yipe_app_shortcut_tests PROPERTIES
    ENVIRONMENT "SDL_VIDEODRIVER=dummy;SDL_AUDIODRIVER=dummy"
    TIMEOUT 30
)

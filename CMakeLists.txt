cmake_minimum_required(VERSION 3.20)

project(yipe3_rebuild VERSION 0.1.0 LANGUAGES CXX)

set(CMAKE_CXX_STANDARD 20)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_CXX_EXTENSIONS OFF)

find_package(SDL2 CONFIG REQUIRED)

add_library(yipe_core
    src/auxiliary_data.cpp
    src/item_names.cpp
    src/player.cpp
    src/random.cpp
    src/save.cpp
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
configure_file(assets/embedded/bitmaps/379-1033.bmp
               "${YIPE_RUNTIME_ASSET_DIR}/379-1033.bmp" COPYONLY)
configure_file(assets/embedded/bitmaps/326-1033.bmp
               "${YIPE_RUNTIME_ASSET_DIR}/326-1033.bmp" COPYONLY)

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
    ENVIRONMENT "SDL_VIDEODRIVER=dummy"
)

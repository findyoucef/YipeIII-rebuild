# Run after installation so CPack and cmake --install share the same bundle.
include(BundleUtilities)
set(bundle "$ENV{DESTDIR}${CMAKE_INSTALL_PREFIX}/Yipe III.app")
set(BU_CHMOD_BUNDLE_ITEMS TRUE)
fixup_bundle("${bundle}" "" "${YIPE_SDL2_LIBRARY_DIR}")

# install_name_tool invalidates the copied Mach-O signatures. Sign nested
# libraries first, then seal the application for local Apple Silicon execution.
# This is ad-hoc signing, not Developer ID signing or notarization.
file(GLOB_RECURSE libraries "${bundle}/*.dylib")
foreach(item IN LISTS libraries)
    execute_process(COMMAND /usr/bin/codesign --force --sign - "${item}"
        RESULT_VARIABLE status ERROR_VARIABLE error)
    if(NOT status EQUAL 0)
        message(FATAL_ERROR "Could not sign ${item}: ${error}")
    endif()
endforeach()
execute_process(COMMAND /usr/bin/codesign --force --sign - "${bundle}"
    RESULT_VARIABLE status ERROR_VARIABLE error)
if(NOT status EQUAL 0)
    message(FATAL_ERROR "Could not sign application: ${error}")
endif()
execute_process(COMMAND /usr/bin/codesign --verify --deep --strict "${bundle}"
    RESULT_VARIABLE status ERROR_VARIABLE error)
if(NOT status EQUAL 0)
    message(FATAL_ERROR "Application signature verification failed: ${error}")
endif()

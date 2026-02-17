# CMakeLists.txt for SoC Firmware (SPEC-FW-001)
# Target: NXP i.MX8M Plus (ARM Cortex-A53)
# Build System: CMake 3.20+ with Yocto SDK cross-compilation
# Test Framework: CMocka

cmake_minimum_required(VERSION 3.20)

project(detector_daemon
    VERSION 1.0.0
    DESCRIPTION "X-ray Detector Panel SoC Controller Firmware"
    LANGUAGES C
)

# Set C standard
set(CMAKE_C_STANDARD 11)
set(CMAKE_C_STANDARD_REQUIRED ON)
set(CMAKE_C_EXTENSIONS OFF)

# Build options
option(BUILD_TESTS "Build unit tests" ON)
option(ENABLE_COVERAGE "Enable code coverage" OFF)
option(CROSS_COMPILE "Cross-compile for ARM target" OFF)

# Cross-compilation setup
if(CROSS_COMPILE)
    # Include toolchain file if specified
    if(CMAKE_TOOLCHAIN_FILE)
        include(${CMAKE_TOOLCHAIN_FILE})
    endif()
endif()

# Compiler flags
set(CMAKE_C_FLAGS "${CMAKE_C_FLAGS} -Wall -Wextra -Wpedantic")
set(CMAKE_C_FLAGS_DEBUG "${CMAKE_C_FLAGS_DEBUG} -g -O0")
set(CMAKE_C_FLAGS_RELEASE "${CMAKE_C_FLAGS_RELEASE} -O2 -DNDEBUG")

# Coverage flags
if(ENABLE_COVERAGE)
    set(CMAKE_C_FLAGS "${CMAKE_C_FLAGS} --coverage")
    set(CMAKE_EXE_LINKER_FLAGS "${CMAKE_EXE_LINKER_FLAGS} --coverage")
endif()

# Find required packages
find_package(Threads REQUIRED)
find_package(PkgConfig REQUIRED)

# Find libraries via pkg-config (if available)
pkg_check_modules(YAML libyaml REQUIRED)
pkg_check_modules(CMOCKA cmocka)

# If CMocka not found via pkg-config, try find_package
if(NOT CMOCKA_FOUND)
    find_package(CMOCKA)
endif()

# ============================================================================
# Source Files
# ============================================================================

set(SRC_DIRS
    src/util
    src/hal
    src/protocol
    src/config
)

# Utility sources
set(UTIL_SRCS
    src/util/crc16.c
    src/util/log.c
)

# HAL sources
set(HAL_SRCS
    src/hal/spi_master.c
    src/hal/csi2_rx.c
    src/hal/eth_tx.c
    src/hal/bq40z50_driver.c
)

# Protocol sources
set(PROTOCOL_SRCS
    src/protocol/frame_header.c
    src/protocol/command_protocol.c
)

# Config sources
set(CONFIG_SRCS
    src/config/config_loader.c
)

# Core application sources
set(CORE_SRCS
    src/sequence_engine.c
    src/frame_manager.c
    src/health_monitor.c
    src/main.c
)

set(ALL_SRCS
    ${UTIL_SRCS}
    ${HAL_SRCS}
    ${PROTOCOL_SRCS}
    ${CONFIG_SRCS}
    ${CORE_SRCS}
)

# ============================================================================
# Main Daemon Target
# ============================================================================

add_executable(detector_daemon ${ALL_SRCS})

target_include_directories(detector_daemon
    PRIVATE
        ${CMAKE_SOURCE_DIR}/src
        ${CMAKE_SOURCE_DIR}/src/util
        ${CMAKE_SOURCE_DIR}/src/hal
        ${CMAKE_SOURCE_DIR}/src/protocol
        ${CMAKE_SOURCE_DIR}/src/config
)

target_link_libraries(detector_daemon
    PRIVATE
        Threads::Threads
        ${YAML_LIBRARIES}
        # OpenSSL for HMAC-SHA256 (when security is implemented)
        # PkgConfig::OPENSSL
)

# Install target
install(TARGETS detector_daemon
    RUNTIME DESTINATION bin
)

# ============================================================================
# Test Targets
# ============================================================================

if(BUILD_TESTS AND CMOCKA_FOUND)
    enable_testing()

    # Test sources
    set(TEST_SRCS
        tests/unit/test_crc16.c
        tests/unit/test_spi_master.c
        tests/unit/test_frame_header.c
        tests/unit/test_config_loader.c
        tests/unit/test_sequence_engine.c
        tests/unit/test_frame_manager.c
        tests/unit/test_command_protocol.c
        tests/unit/test_health_monitor.c
        tests/unit/test_csi2_rx.c
    )

    # Mock sources
    set(MOCK_SRCS
        tests/mock/mock_spidev.c
        tests/mock/mock_v4l2.c
        tests/mock/mock_yaml.c
    )

    # Create test library from source files (for linking with tests)
    add_library(test_obj OBJECT
        ${UTIL_SRCS}
        ${PROTOCOL_SRCS}
        ${CONFIG_SRCS}
    )

    target_include_directories(test_obj
        PUBLIC
            ${CMAKE_SOURCE_DIR}/src
            ${CMAKE_SOURCE_DIR}/src/util
            ${CMAKE_SOURCE_DIR}/src/protocol
            ${CMAKE_SOURCE_DIR}/src/config
    )

    # Individual test executables
    # CRC-16 tests
    add_executable(test_crc16
        tests/unit/test_crc16.c
        $<TARGET_OBJECTS:test_obj>
    )
    target_link_libraries(test_crc16 PRIVATE ${CMOCKA_LIBRARIES})
    add_test(NAME test_crc16 COMMAND test_crc16)

    # Frame header tests
    add_executable(test_frame_header
        tests/unit/test_frame_header.c
        $<TARGET_OBJECTS:test_obj>
    )
    target_link_libraries(test_frame_header PRIVATE ${CMOCKA_LIBRARIES} ${YAML_LIBRARIES})
    add_test(NAME test_frame_header COMMAND test_frame_header)

    # Config loader tests
    add_executable(test_config_loader
        tests/unit/test_config_loader.c
        $<TARGET_OBJECTS:test_obj>
    )
    target_link_libraries(test_config_loader PRIVATE ${CMOCKA_LIBRARIES} ${YAML_LIBRARIES})
    add_test(NAME test_config_loader COMMAND test_config_loader)

    # Frame manager tests
    add_executable(test_frame_manager
        tests/unit/test_frame_manager.c
        src/frame_manager.c
    )
    target_include_directories(test_frame_manager PRIVATE ${CMAKE_SOURCE_DIR}/include)
    target_link_libraries(test_frame_manager PRIVATE ${CMOCKA_LIBRARIES})
    add_test(NAME test_frame_manager COMMAND test_frame_manager)

    # CSI-2 RX tests
    add_executable(test_csi2_rx
        tests/unit/test_csi2_rx.c
        tests/mock/mock_v4l2.c
        src/hal/csi2_rx.c
    )
    target_include_directories(test_csi2_rx PRIVATE ${CMAKE_SOURCE_DIR}/include)
    target_link_libraries(test_csi2_rx PRIVATE ${CMOCKA_LIBRARIES} Threads::Threads)
    add_test(NAME test_csi2_rx COMMAND test_csi2_rx)

    # Health monitor tests
    add_executable(test_health_monitor
        tests/unit/test_health_monitor.c
        src/health_monitor.c
    )
    target_include_directories(test_health_monitor PRIVATE ${CMAKE_SOURCE_DIR}/include)
    target_link_libraries(test_health_monitor PRIVATE ${CMOCKA_LIBRARIES} Threads::Threads)
    add_test(NAME test_health_monitor COMMAND test_health_monitor)

    # BQ40z50 driver tests
    add_executable(test_bq40z50_driver
        tests/unit/test_bq40z50_driver.c
        src/hal/bq40z50_driver.c
    )
    target_include_directories(test_bq40z50_driver PRIVATE ${CMAKE_SOURCE_DIR}/include)
    target_link_libraries(test_bq40z50_driver PRIVATE ${CMOCKA_LIBRARIES} Threads::Threads)
    add_test(NAME test_bq40z50_driver COMMAND test_bq40z50_driver)

    # Add more test executables as implementation progresses...

elseif(BUILD_TESTS AND NOT CMOCKA_FOUND)
    message(WARNING "BUILD_TESTS=ON but CMocka not found. Tests will not be built.")
endif()

# ============================================================================
# Coverage Target
# ============================================================================

if(ENABLE_COVERAGE AND BUILD_TESTS)
    find_program(LCOV lcov)
    find_program(GCOV gcov)
    find_program(GENHTML genhtml)

    if(LCOV AND GCOV AND GENHTML)
        add_custom_target(coverage
            COMMAND ${LCOV} --capture --directory . --output-file coverage.info
            COMMAND ${LCOV} --remove coverage.info '/usr/*' '*tests/*' '*mock/*' --output-file coverage.info
            COMMAND ${GENHTML} coverage.info --output-directory coverage_html
            COMMENT "Generating code coverage report..."
        )
    else()
        message(WARNING "Coverage tools (lcov/gcov/genhtml) not found. Coverage target not created.")
    endif()
endif()

# ============================================================================
# Documentation Target
# ============================================================================

option(BUILD_DOCS "Build API documentation" OFF)

if(BUILD_DOCS)
    find_program(DOXYGEN doxygen)

    if(DOXYGEN)
        set(DOXYGEN_IN ${CMAKE_CURRENT_SOURCE_DIR}/docs/Doxyfile.in)
        set(DOXYGEN_OUT ${CMAKE_CURRENT_BINARY_DIR}/Doxyfile)

        configure_file(${DOXYGEN_IN} ${DOXYGEN_OUT} @ONLY)

        add_custom_target(docs
            COMMAND ${DOXYGEN} ${DOXYGEN_OUT}
            WORKING_DIRECTORY ${CMAKE_CURRENT_BINARY_DIR}
            COMMENT "Generating API documentation with Doxygen"
            VERBATIM
        )
    else()
        message(WARNING "Doxygen not found. Documentation target not created.")
    endif()
endif()

# ============================================================================
# Summary
# ============================================================================()

message(STATUS "")
    message(STATUS "Configuration Summary:")
    message(STATUS "  Version: ${PROJECT_VERSION}")
    message(STATUS "  Build type: ${CMAKE_BUILD_TYPE}")
    message(STATUS "  C Compiler: ${CMAKE_C_COMPILER}")
    message(STATUS "  C Flags: ${CMAKE_C_FLAGS}")
    message(STATUS "  Install prefix: ${CMAKE_INSTALL_PREFIX}")
    message(STATUS "  Build tests: ${BUILD_TESTS}")
    message(STATUS "  Enable coverage: ${ENABLE_COVERAGE}")
    message(STATUS "  Cross-compile: ${CROSS_COMPILE}")
    message(STATUS "")
endif()

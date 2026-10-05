# SmileySecure — a portable C++ library for the Smiley Secure Protocol (SSP/eSSP).
# SPDX-License-Identifier: LGPL-3.0-or-later
cmake_minimum_required(VERSION 3.16)

project(SmileySecure VERSION 0.1.0 LANGUAGES CXX)

if(CMAKE_SOURCE_DIR STREQUAL CMAKE_CURRENT_SOURCE_DIR)
    set(SMILEYSECURE_TOP_LEVEL ON)
else()
    set(SMILEYSECURE_TOP_LEVEL OFF)
endif()

option(SMILEYSECURE_BUILD_TESTS "Build the test suite" ${SMILEYSECURE_TOP_LEVEL})
option(SMILEYSECURE_WARNINGS_AS_ERRORS "Treat compiler warnings as errors" OFF)

set(SMILEYSECURE_SOURCES
    src/clock.cpp
    src/codes.cpp
    src/crc.cpp
    src/error.cpp
    src/event_table.cpp
    src/link.cpp
    src/message.cpp
    src/packet.cpp
    src/poll.cpp
    src/setup.cpp
    src/values.cpp
)

add_library(smileysecure ${SMILEYSECURE_SOURCES})
add_library(SmileySecure::smileysecure ALIAS smileysecure)

target_compile_features(smileysecure PUBLIC cxx_std_17)
set_target_properties(smileysecure PROPERTIES CXX_EXTENSIONS OFF POSITION_INDEPENDENT_CODE ON)
target_include_directories(smileysecure PUBLIC
    $<BUILD_INTERFACE:${CMAKE_CURRENT_SOURCE_DIR}/include>
    $<INSTALL_INTERFACE:include>)

find_package(Threads REQUIRED)
target_link_libraries(smileysecure PUBLIC Threads::Threads)

if(MSVC)
    target_compile_options(smileysecure PRIVATE /W4)
    if(SMILEYSECURE_WARNINGS_AS_ERRORS)
        target_compile_options(smileysecure PRIVATE /WX)
    endif()
else()
    target_compile_options(smileysecure PRIVATE -Wall -Wextra -Wpedantic -Wconversion -Wshadow)
    if(SMILEYSECURE_WARNINGS_AS_ERRORS)
        target_compile_options(smileysecure PRIVATE -Werror)
    endif()
endif()

include(GNUInstallDirs)
install(TARGETS smileysecure EXPORT SmileySecureTargets
    ARCHIVE DESTINATION ${CMAKE_INSTALL_LIBDIR}
    LIBRARY DESTINATION ${CMAKE_INSTALL_LIBDIR})
install(DIRECTORY include/ DESTINATION ${CMAKE_INSTALL_INCLUDEDIR})
install(EXPORT SmileySecureTargets NAMESPACE SmileySecure:: DESTINATION ${CMAKE_INSTALL_LIBDIR}/cmake/SmileySecure)

if(SMILEYSECURE_BUILD_TESTS)
    enable_testing()
    add_subdirectory(tests)
endif()

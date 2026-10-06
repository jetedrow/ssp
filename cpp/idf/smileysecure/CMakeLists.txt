# SmileySecure as an ESP-IDF component (also usable from Arduino-ESP32 run as a component).
# SPDX-License-Identifier: LGPL-3.0-or-later
#
# The portable core lives two directories up and is shared with every other platform; this
# component compiles it alongside the ESP32-specific pieces: a UART stream and a way to run the
# poll host on a chosen core.
set(core "${CMAKE_CURRENT_LIST_DIR}/../..")

idf_component_register(
    SRCS
        "${core}/src/clock.cpp"
        "${core}/src/codes.cpp"
        "${core}/src/crc.cpp"
        "${core}/src/device.cpp"
        "${core}/src/error.cpp"
        "${core}/src/event_table.cpp"
        "${core}/src/host.cpp"
        "${core}/src/link.cpp"
        "${core}/src/message.cpp"
        "${core}/src/packet.cpp"
        "${core}/src/poll.cpp"
        "${core}/src/setup.cpp"
        "${core}/src/values.cpp"
        "src/host_task.cpp"
        "src/uart_stream.cpp"
    INCLUDE_DIRS
        "${core}/include"
        "include"
    REQUIRES
        driver
        pthread
)

target_compile_features(${COMPONENT_LIB} PUBLIC cxx_std_17)

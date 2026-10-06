# SPDX-License-Identifier: LGPL-3.0-or-later
add_executable(smileysecure_tests
    test_main.cpp
    test_codec.cpp
    test_device.cpp
    test_framing.cpp
    test_host.cpp
    test_link.cpp
    test_manual_examples.cpp
)

target_link_libraries(smileysecure_tests PRIVATE SmileySecure::smileysecure)

if(NOT MSVC)
    target_compile_options(smileysecure_tests PRIVATE -Wall -Wextra)
endif()

add_test(NAME smileysecure_tests COMMAND smileysecure_tests)

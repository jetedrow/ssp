// A deliberately tiny test harness, so the suite builds anywhere the library does — including
// toolchains with exceptions switched off — with nothing to download.
// SPDX-License-Identifier: LGPL-3.0-or-later
#pragma once

#include <cstdint>
#include <cstdio>
#include <functional>
#include <string>
#include <type_traits>
#include <vector>

#include "smileysecure/bytes.hpp"

namespace sstest {

struct Case {
    const char* name;
    void (*body)();
};

std::vector<Case>& registry();
int& failures();

struct Registrar {
    Registrar(const char* name, void (*body)()) { registry().push_back(Case{name, body}); }
};

void report(const char* file, int line, const std::string& what);

inline std::string hex(smileysecure::ByteView bytes) {
    std::string text;
    char byte[4];
    for (size_t i = 0; i < bytes.size(); ++i) {
        std::snprintf(byte, sizeof byte, i == 0 ? "%02X" : " %02X", bytes[i]);
        text += byte;
    }
    return text;
}

inline std::string show(const std::vector<uint8_t>& value) { return "[" + hex(value) + "]"; }
inline std::string show(const std::string& value) { return "\"" + value + "\""; }
inline std::string show(const char* value) { return value == nullptr ? "null" : show(std::string(value)); }
inline std::string show(bool value) { return value ? "true" : "false"; }
template <typename T>
std::string show(const T& value) {
    if constexpr (std::is_enum_v<T>) {
        return std::to_string(static_cast<long long>(value));
    } else if constexpr (std::is_integral_v<T>) {
        return std::to_string(value);
    } else {
        return "(value)";
    }
}

/// Parses "7F 80 01" into bytes.
inline std::vector<uint8_t> bytes(const std::string& text) {
    std::vector<uint8_t> out;
    unsigned value = 0;
    int digits = 0;
    for (char c : text) {
        int nibble = -1;
        if (c >= '0' && c <= '9') nibble = c - '0';
        if (c >= 'A' && c <= 'F') nibble = c - 'A' + 10;
        if (c >= 'a' && c <= 'f') nibble = c - 'a' + 10;
        if (nibble < 0) continue;
        value = (value << 4) | static_cast<unsigned>(nibble);
        if (++digits == 2) {
            out.push_back(static_cast<uint8_t>(value));
            value = 0;
            digits = 0;
        }
    }
    return out;
}

}  // namespace sstest

#define SSTEST_CONCAT2(a, b) a##b
#define SSTEST_CONCAT(a, b) SSTEST_CONCAT2(a, b)

#define TEST(name)                                                              \
    static void name();                                                         \
    static ::sstest::Registrar SSTEST_CONCAT(name, _registrar)(#name, &name);   \
    static void name()

#define CHECK(condition)                                                                    \
    do {                                                                                    \
        if (!(condition)) ::sstest::report(__FILE__, __LINE__, "CHECK(" #condition ")");   \
    } while (0)

#define CHECK_EQ(actual, ...)                                                               \
    do {                                                                                         \
        const auto sstest_actual = (actual);                                                     \
        const auto sstest_expected = (__VA_ARGS__);                                              \
        if (!(sstest_actual == sstest_expected)) {                                               \
            ::sstest::report(__FILE__, __LINE__,                                                 \
                             "CHECK_EQ(" #actual ", " #__VA_ARGS__ "): got " +                      \
                                 ::sstest::show(sstest_actual) + ", expected " +                  \
                                 ::sstest::show(sstest_expected));                               \
        }                                                                                        \
    } while (0)

/// Stops the test when a precondition for the rest of it fails.
#define REQUIRE(condition)                                                                    \
    do {                                                                                      \
        if (!(condition)) {                                                                   \
            ::sstest::report(__FILE__, __LINE__, "REQUIRE(" #condition ")");                 \
            return;                                                                           \
        }                                                                                     \
    } while (0)

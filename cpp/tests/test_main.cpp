// SPDX-License-Identifier: LGPL-3.0-or-later
#include <cstring>

#include "test.hpp"

namespace sstest {

std::vector<Case>& registry() {
    static std::vector<Case> cases;
    return cases;
}

int& failures() {
    static int count = 0;
    return count;
}

void report(const char* file, int line, const std::string& what) {
    ++failures();
    std::fprintf(stderr, "  %s:%d: %s\n", file, line, what.c_str());
}

}  // namespace sstest

int main(int argc, char** argv) {
    const char* filter = argc > 1 ? argv[1] : nullptr;
    int run = 0;
    int failed = 0;

    for (const auto& test : sstest::registry()) {
        if (filter != nullptr && std::strstr(test.name, filter) == nullptr) continue;

        const int before = sstest::failures();
        test.body();
        ++run;

        if (sstest::failures() != before) {
            ++failed;
            std::fprintf(stderr, "FAIL %s\n", test.name);
        }
    }

    std::printf("%d test(s), %d failed\n", run, failed);
    return failed == 0 && run > 0 ? 0 : 1;
}

// SmileySecure — a portable C++ library for the Smiley Secure Protocol (SSP/eSSP).
// SPDX-License-Identifier: LGPL-3.0-or-later
#pragma once

#include <cstddef>
#include <cstdint>
#include <vector>

namespace smileysecure {

/// A read-only view of a run of bytes: what std::span<const uint8_t> is in C++20.
///
/// The library targets C++17 so that it builds with every ESP-IDF and NDK toolchain in use,
/// which is why this exists rather than std::span.  It never owns what it points at.
class ByteView {
public:
    constexpr ByteView() noexcept = default;
    constexpr ByteView(const uint8_t* data, size_t size) noexcept : data_(data), size_(size) {}
    ByteView(const std::vector<uint8_t>& bytes) noexcept  // NOLINT: implicit by design
        : data_(bytes.data()), size_(bytes.size()) {}

    constexpr const uint8_t* data() const noexcept { return data_; }
    constexpr size_t size() const noexcept { return size_; }
    constexpr bool empty() const noexcept { return size_ == 0; }
    constexpr const uint8_t* begin() const noexcept { return data_; }
    constexpr const uint8_t* end() const noexcept { return data_ + size_; }
    constexpr uint8_t operator[](size_t index) const noexcept { return data_[index]; }

    /// The bytes from `offset` to the end, or an empty view when `offset` is past it.
    constexpr ByteView subview(size_t offset) const noexcept {
        return offset >= size_ ? ByteView(data_ + size_, 0) : ByteView(data_ + offset, size_ - offset);
    }

    /// Up to `count` bytes from `offset`, cut short at the end of the view.
    constexpr ByteView subview(size_t offset, size_t count) const noexcept {
        const ByteView rest = subview(offset);
        return ByteView(rest.data_, count < rest.size_ ? count : rest.size_);
    }

    std::vector<uint8_t> to_vector() const { return std::vector<uint8_t>(data_, data_ + size_); }

private:
    const uint8_t* data_ = nullptr;
    size_t size_ = 0;
};

inline bool operator==(ByteView left, ByteView right) noexcept {
    if (left.size() != right.size()) return false;
    for (size_t i = 0; i < left.size(); ++i) {
        if (left[i] != right[i]) return false;
    }
    return true;
}

inline bool operator!=(ByteView left, ByteView right) noexcept { return !(left == right); }

}  // namespace smileysecure

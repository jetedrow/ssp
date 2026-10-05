// SmileySecure — a portable C++ library for the Smiley Secure Protocol (SSP/eSSP).
// SPDX-License-Identifier: LGPL-3.0-or-later
#pragma once

#include <cstdint>
#include <optional>
#include <string>
#include <utility>

namespace smileysecure {

/// What went wrong.
///
/// The library reports failures as values rather than by throwing, because ESP-IDF builds
/// without C++ exceptions by default and a protocol library should not force them on.  Each
/// value corresponds to one of the .NET library's exception types.
enum class Error : uint8_t {
    /// Nothing went wrong.
    None = 0,

    /// The device did not answer in time.
    Timeout,

    /// The stream ended, mid-packet or before one began.
    ConnectionClosed,

    /// The transport refused a write.
    TransportFailed,

    /// A packet was shorter or longer than SSP allows, or its length byte disagreed with it.
    PacketLength,

    /// A packet was malformed: no STX, a lone STX inside it, the wrong sequence flag or address,
    /// or a reply carrying no response code.
    PacketFormat,

    /// A packet's CRC did not match its contents.
    PacketCrc,

    /// Every attempt at an exchange failed.  `Status::cause` says how the last one did.
    NoUsableReply,

    /// The device answered, but not with OK.  `Status::response` carries the code it gave.
    Refused,

    /// An encrypted packet would not decrypt, or its counter was out of step.
    Encryption,

    /// A firmware or dataset download failed.
    Download,

    /// An argument was out of range.
    InvalidArgument,

    /// The call is not valid in the object's current state.
    InvalidState,
};

/// A short, fixed name for an error, such as "packet CRC".
const char* to_string(Error error) noexcept;

/// The outcome of an operation that returns nothing on success.
///
/// `message` always points at a string literal, so a status can be copied freely and costs no
/// allocation — on a microcontroller that matters.  `describe()` builds the full sentence when
/// one is wanted for a log.
struct Status {
    Error error = Error::None;

    /// For `Error::NoUsableReply`, how the last attempt failed.
    Error cause = Error::None;

    /// For `Error::Refused`, the response code the device gave.
    uint8_t response = 0;

    /// A fixed sentence about what failed.  Never null.
    const char* message = "";

    constexpr bool ok() const noexcept { return error == Error::None; }
    constexpr explicit operator bool() const noexcept { return ok(); }

    static constexpr Status success() noexcept { return Status{}; }

    static constexpr Status failure(Error error, const char* message) noexcept {
        return Status{error, Error::None, 0, message};
    }

    static constexpr Status refused(uint8_t response, const char* message) noexcept {
        return Status{Error::Refused, Error::None, response, message};
    }

    /// The whole story, including the cause and response code where they apply.
    std::string describe() const;
};

/// A value, or the reason there is none.
///
/// A small stand-in for C++23's std::expected.  `value()` on a failed result is a programming
/// error; check `ok()` first.
template <typename T>
class Result {
public:
    Result(T value) : value_(std::move(value)) {}  // NOLINT: implicit by design
    Result(Status status) : status_(status) {}     // NOLINT: implicit by design

    bool ok() const noexcept { return status_.ok() && value_.has_value(); }
    explicit operator bool() const noexcept { return ok(); }

    const Status& status() const noexcept { return status_; }

    T& value() & { return *value_; }
    const T& value() const& { return *value_; }
    T&& value() && { return std::move(*value_); }

    T* operator->() { return &*value_; }
    const T* operator->() const { return &*value_; }
    T& operator*() & { return *value_; }
    const T& operator*() const& { return *value_; }

private:
    Status status_{};
    std::optional<T> value_{};
};

}  // namespace smileysecure

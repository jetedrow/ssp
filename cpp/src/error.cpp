// SmileySecure — a portable C++ library for the Smiley Secure Protocol (SSP/eSSP).
// SPDX-License-Identifier: LGPL-3.0-or-later
#include "smileysecure/error.hpp"

#include <cstdio>

#include "smileysecure/codes.hpp"

namespace smileysecure {

const char* to_string(Error error) noexcept {
    switch (error) {
        case Error::None: return "none";
        case Error::Timeout: return "timeout";
        case Error::ConnectionClosed: return "connection closed";
        case Error::TransportFailed: return "transport failed";
        case Error::PacketLength: return "packet length";
        case Error::PacketFormat: return "packet format";
        case Error::PacketCrc: return "packet CRC";
        case Error::NoUsableReply: return "no usable reply";
        case Error::Refused: return "refused";
        case Error::Encryption: return "encryption";
        case Error::Download: return "download";
        case Error::InvalidArgument: return "invalid argument";
        case Error::InvalidState: return "invalid state";
    }
    return "unknown";
}

std::string Status::describe() const {
    if (ok()) return "OK";

    std::string text = to_string(error);
    if (message != nullptr && *message != '\0') {
        text += ": ";
        text += message;
    }

    if (error == Error::Refused) {
        char code[8];
        std::snprintf(code, sizeof code, "0x%02X", response);
        const char* name = response_name(response);
        text += " (the device answered ";
        text += name != nullptr ? name : "an unnamed code";
        text += ", ";
        text += code;
        text += ")";
    }

    if (cause != Error::None) {
        text += " (last attempt: ";
        text += to_string(cause);
        text += ")";
    }

    return text;
}

}  // namespace smileysecure

// SmileySecure — a portable C++ library for the Smiley Secure Protocol (SSP/eSSP).
// SPDX-License-Identifier: LGPL-3.0-or-later
#include "smileysecure/message.hpp"

#include <cstdio>

#include "smileysecure/constants.hpp"

namespace smileysecure {

Result<std::vector<uint8_t>> make_message(uint8_t command, ByteView parameters) {
    if (parameters.size() + 1 > kMaxDataLength) {
        return Status::failure(Error::InvalidArgument, "a command and its parameters must fit in 255 bytes");
    }

    std::vector<uint8_t> message;
    message.reserve(parameters.size() + 1);
    message.push_back(command);
    message.insert(message.end(), parameters.begin(), parameters.end());
    return message;
}

Result<Reply> Reply::parse(ByteView payload) {
    if (payload.empty()) return Status::failure(Error::PacketFormat, "a reply must carry at least a response code");

    Reply reply;
    reply.code = payload[0];
    reply.data = payload.subview(1).to_vector();
    return reply;
}

Status Reply::ensure_ok() const noexcept {
    if (ok()) return Status::success();
    return Status::refused(code, "the device did not accept the command");
}

std::string Reply::to_string() const {
    const char* name = response_name(code);
    char text[64];
    if (name != nullptr) {
        std::snprintf(text, sizeof text, "%s, %u data byte(s)", name, static_cast<unsigned>(data.size()));
    } else {
        std::snprintf(text, sizeof text, "0x%02X, %u data byte(s)", code, static_cast<unsigned>(data.size()));
    }
    return text;
}

}  // namespace smileysecure

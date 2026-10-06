// SmileySecure — a portable C++ library for the Smiley Secure Protocol (SSP/eSSP).
// SPDX-License-Identifier: LGPL-3.0-or-later
#include "smileysecure/link.hpp"

namespace smileysecure {

Link::Link(Stream& stream, LinkOptions options, Clock& clock)
    : stream_(stream), clock_(clock), options_(options) {}

Result<Packet> Link::exchange(uint8_t address, ByteView data) {
    if (address > kAddressMask) return Status::failure(Error::InvalidArgument, "an SSP address is seven bits");

    std::lock_guard<std::mutex> lock(gate_);

    // Advanced once, for the exchange as a whole.  Every retry below reuses it.
    const bool sequence_flag = advance_sequence_flag(address);

    Packet request;
    request.address = address;
    request.data = data.to_vector();
    Result<std::vector<uint8_t>> logical = request.encode(sequence_flag);
    if (!logical) return logical.status();

    Error last_failure = Error::None;

    for (unsigned attempt = 0; attempt <= options_.max_retries; ++attempt) {
        Result<Packet> response = attempt_once(*logical, address, sequence_flag);
        if (response) return response;

        switch (response.status().error) {
            // A lost reply, a corrupt one and one answering an earlier command are all
            // indistinguishable from the device not having heard; resend with the same flag.
            case Error::Timeout:
            case Error::PacketCrc:
            case Error::PacketFormat:
            case Error::PacketLength:
                last_failure = response.status().error;
                break;

            // Anything else will not get better by asking again.
            default:
                return response;
        }
    }

    Status failed = Status::failure(Error::NoUsableReply, "no usable reply from the device after every attempt");
    failed.cause = last_failure;
    return failed;
}

Result<Packet> Link::attempt_once(const std::vector<uint8_t>& logical, uint8_t address, bool sequence_flag) {
    const Status written = write_packet(stream_, logical);
    if (!written) return written;

    Frame frame;
    const Status read = read_packet(stream_, clock_, clock_.now_ms() + options_.response_timeout_ms, frame);
    if (!read) return read;

    // A reply carries back the flag it was sent with, so a mismatch means this is the device
    // answering an earlier command.
    Result<Packet> response = Packet::parse(frame.view(), sequence_flag);
    if (!response) return response;

    if (response->address != address) {
        return Status::failure(Error::PacketFormat, "the reply came from a different address");
    }

    return response;
}

void Link::reset_sequence(uint8_t address) {
    std::lock_guard<std::mutex> lock(gate_);
    sequence_flags_.reset(address & kAddressMask);
}

bool Link::advance_sequence_flag(uint8_t address) {
    const bool next = !sequence_flags_.test(address);
    sequence_flags_.set(address, next);
    return next;
}

}  // namespace smileysecure

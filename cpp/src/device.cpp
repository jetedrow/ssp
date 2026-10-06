// SmileySecure — a portable C++ library for the Smiley Secure Protocol (SSP/eSSP).
// SPDX-License-Identifier: LGPL-3.0-or-later
#include "smileysecure/device.hpp"

#include "smileysecure/values.hpp"

namespace smileysecure {

// ---- Bus ------------------------------------------------------------------------------------

Bus::Bus(Stream& stream, DeviceOptions options, Clock& clock)
    : options_(std::move(options)), link_(stream, options_.link, clock) {}

Bus::~Bus() = default;

Device* Bus::device(uint8_t address) {
    if (address > kAddressMask) return nullptr;

    std::lock_guard<std::mutex> lock(gate_);
    auto& slot = devices_[address];
    if (!slot) slot = std::make_unique<Device>(*this, address);
    return slot.get();
}

// ---- Device ---------------------------------------------------------------------------------

Device::Device(Bus& bus, uint8_t address)
    : bus_(bus), address_(address), protocol_version_(protocol_version::kLowest) {}

std::optional<Setup> Device::setup() const {
    std::lock_guard<std::mutex> lock(setup_gate_);
    return setup_;
}

Result<Reply> Device::send(uint8_t command, ByteView parameters) {
    Result<std::vector<uint8_t>> message = make_message(command, parameters);
    if (!message) return message.status();

    Result<Packet> packet = bus_.link().exchange(address_, *message);
    if (!packet) return packet.status();

    return Reply::parse(packet->data);
}

Result<Reply> Device::send_checked(Command command, ByteView parameters) {
    Result<Reply> reply = send(command, parameters);
    if (!reply) return reply;

    const Status accepted = reply->ensure_ok();
    if (!accepted) return accepted;
    return reply;
}

Status Device::command(Command command, ByteView parameters) {
    return send_checked(command, parameters).status();
}

Result<Setup> Device::connect() {
    const Status synced = sync();
    if (!synced) return synced;

    Result<uint8_t> version = negotiate_protocol_version();
    if (!version) return version.status();

    return setup_request();
}

Result<uint8_t> Device::negotiate_protocol_version(std::optional<uint8_t> highest) {
    const uint8_t ceiling = highest.value_or(bus_.options().highest_protocol_version);

    for (unsigned version = ceiling; version >= protocol_version::kLowest; --version) {
        const std::vector<uint8_t> parameter{static_cast<uint8_t>(version)};
        Result<Reply> reply = send(Command::HostProtocolVersion, parameter);
        if (!reply) return reply.status();

        if (reply->ok()) {
            protocol_version_.store(static_cast<uint8_t>(version));
            return static_cast<uint8_t>(version);
        }

        // FAIL means "not that version".  Anything else is a different problem, and stepping down
        // will not fix it.
        if (reply->response() != Response::Failure) return reply->ensure_ok();
    }

    return Status::refused(static_cast<uint8_t>(Response::Failure),
                           "the device accepted no protocol version this host can decode");
}

Status Device::set_protocol_version(uint8_t version) {
    const std::vector<uint8_t> parameter{version};
    const Status accepted = command(Command::HostProtocolVersion, parameter);
    if (accepted) protocol_version_.store(version);
    return accepted;
}

Status Device::sync() {
    // The device resets its own flag on SYNC, so the link has to reset its copy too or the two
    // ends disagree about the next command's flag.
    bus_.link().reset_sequence(address_);
    const Status synced = command(Command::Sync);
    bus_.link().reset_sequence(address_);
    return synced;
}

Status Device::reset() { return command(Command::Reset); }
Status Device::enable() { return command(Command::Enable); }
Status Device::disable() { return command(Command::Disable); }
Status Device::display_on() { return command(Command::DisplayOn); }
Status Device::display_off() { return command(Command::DisplayOff); }
Status Device::reject_banknote() { return command(Command::RejectBanknote); }
Status Device::hold() { return command(Command::Hold); }

Status Device::set_channel_inhibits(uint16_t channel_mask) {
    const std::vector<uint8_t> mask{static_cast<uint8_t>(channel_mask), static_cast<uint8_t>(channel_mask >> 8)};
    return set_channel_inhibits(mask);
}

Status Device::set_channel_inhibits(ByteView channel_mask) {
    if (channel_mask.empty()) return Status::failure(Error::InvalidArgument, "a channel mask needs at least one byte");
    return command(Command::SetChannelInhibits, channel_mask);
}

Result<Setup> Device::setup_request() {
    Result<Reply> reply = send_checked(Command::SetupRequest);
    if (!reply) return reply.status();

    Result<Setup> setup = Setup::parse(reply->data);
    if (!setup) return setup;

    {
        std::lock_guard<std::mutex> lock(setup_gate_);
        setup_ = *setup;
    }
    if (setup->protocol_version) protocol_version_.store(*setup->protocol_version);

    return setup;
}

Result<uint32_t> Device::serial_number() {
    Result<Reply> reply = send_checked(Command::GetSerialNumber);
    if (!reply) return reply.status();
    if (reply->data.size() < 4) return Status::failure(Error::PacketFormat, "a serial number is four bytes");

    // Serial numbers are big-endian, unlike the amounts in event payloads.
    return values::read_big_endian(reply->data);
}

Result<std::string> Device::firmware_version() {
    Result<Reply> reply = send_checked(Command::GetFirmwareVersion);
    if (!reply) return reply.status();
    return values::ascii(reply->data);
}

Result<std::string> Device::dataset_version() {
    Result<Reply> reply = send_checked(Command::GetDatasetVersion);
    if (!reply) return reply.status();
    return values::ascii(reply->data);
}

Result<PollResult> Device::poll() { return poll_using(Command::Poll); }

Result<PollResult> Device::poll_with_ack() { return poll_using(Command::PollWithAck); }

Status Device::event_acknowledge() { return command(Command::EventAck); }

Result<PollResult> Device::poll_using(Command poll_command) {
    Result<Reply> reply = send_checked(poll_command);
    if (!reply) return reply.status();
    return decode_poll(reply->data, protocol_version(), bus_.options().event_table);
}

}  // namespace smileysecure

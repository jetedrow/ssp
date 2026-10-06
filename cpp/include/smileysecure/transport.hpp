// SmileySecure — a portable C++ library for the Smiley Secure Protocol (SSP/eSSP).
// SPDX-License-Identifier: LGPL-3.0-or-later
#pragma once

#include <cstddef>
#include <cstdint>

namespace smileysecure {

/// The byte transport a bus runs over: a UART on an ESP32, a USB serial adapter on Android, a
/// pipe in a test.
///
/// This is the C++ library's equivalent of the .NET library's System.IO.Stream boundary, and it
/// is the only thing a platform has to supply.  It asks for nothing beyond moving bytes, so
/// nothing in the core depends on an operating system.
class Stream {
public:
    virtual ~Stream() = default;

    /// Writes every byte, blocking until they are queued for sending.
    /// @return false if the transport refused the write.
    virtual bool write(const uint8_t* data, size_t size) = 0;

    /// Reads up to `size` bytes, waiting at most `timeout_ms` for the first one to arrive.
    /// @return the number of bytes read; 0 if none arrived in time; a negative number if the
    ///         stream has closed and no more will ever arrive.
    virtual int read(uint8_t* data, size_t size, uint32_t timeout_ms) = 0;

    /// Waits until everything written has left.  Optional; the default does nothing.
    virtual void flush() {}
};

/// A transport that can change line speed under a running connection.
///
/// Only a firmware download needs this: partway through, the device switches to a faster speed
/// to take the payload.  A UART implements it; a pipe has no line speed and does not.
class BaudRateControl {
public:
    virtual ~BaudRateControl() = default;

    /// Changes the line speed in bits per second.
    virtual bool set_baud_rate(uint32_t baud_rate) = 0;

    /// Throws away anything sitting unread in the transport's buffers.
    virtual void discard_buffers() = 0;
};

/// A monotonic millisecond clock, and a way to wait on it.
///
/// Timeouts and the poll interval are measured against this.  `Clock::system()` uses
/// std::chrono and std::this_thread, which ESP-IDF, Arduino-ESP32 and the Android NDK all
/// provide; tests substitute one whose time only moves when told to.
class Clock {
public:
    virtual ~Clock() = default;

    /// Milliseconds since an arbitrary fixed point.  Never goes backwards.
    virtual uint64_t now_ms() = 0;

    /// Blocks the calling thread for about `duration_ms`.
    virtual void sleep_ms(uint32_t duration_ms) = 0;

    /// The process-wide clock built on std::chrono::steady_clock.
    static Clock& system();
};

}  // namespace smileysecure

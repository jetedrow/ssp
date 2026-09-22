using System;
using System.Collections.Generic;
using System.IO;
using CCS.SspNet.Communication;

namespace CCS.SspNet
{
    /// <summary>
    /// One SSP bus: a stream, and the devices addressed over it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// SSP is a multi-drop protocol — several devices can share one pair of wires, each answering
    /// to its own address — and only one exchange can be in flight at a time.  A bus owns the
    /// serialization that makes that safe, so calls to two devices from two threads queue behind
    /// each other rather than interleaving on the wire.
    /// </para>
    /// <para>
    /// For the common case of one device on its own connection, <see cref="SspDevice.Attach"/>
    /// makes the bus for you.
    /// </para>
    /// </remarks>
    public sealed class SspBus : IDisposable
    {
        private readonly SspLink link;
        private readonly Dictionary<byte, SspDevice> devices = new Dictionary<byte, SspDevice>();
        private readonly object gate = new object();
        private bool disposed;

        private SspBus(Stream stream, SspDeviceOptions options)
        {
            Options = options;
            link = new SspLink(stream, new SspLinkOptions
            {
                ResponseTimeout = options.ResponseTimeout,
                MaxRetries = options.MaxRetries,
            });
        }

        /// <summary>
        /// Opens a bus over a stream.
        /// </summary>
        /// <param name="stream">
        /// The transport.  Serial, network, or anything else that can be a <see cref="Stream"/> —
        /// the bus does not care which, which is what lets it be an in-memory stream in a test.
        /// </param>
        /// <param name="options">Timing, retry and decoding settings, or <see langword="null"/> for the defaults.</param>
        /// <remarks>The stream is not owned by the bus and is left open when the bus is disposed.</remarks>
        public static SspBus Open(Stream stream, SspDeviceOptions? options = null)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));

            return new SspBus(stream, options ?? new SspDeviceOptions());
        }

        /// <summary>Gets the settings this bus and its devices were opened with.</summary>
        public SspDeviceOptions Options { get; }

        /// <summary>
        /// Gets the device at an address, creating it the first time it is asked for.
        /// </summary>
        /// <param name="address">
        /// The device's address on the bus.  Validators default to 0 and hoppers to 16; a device's
        /// address is set with its configuration tool.
        /// </param>
        /// <exception cref="ArgumentOutOfRangeException">The address does not fit in seven bits.</exception>
        public SspDevice Device(byte address = 0x00)
        {
            ThrowIfDisposed();

            if (address > Constants.AddressMask)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(address), address,
                    $"An SSP address is seven bits, so at most {Constants.AddressMask}.");
            }

            lock (gate)
            {
                if (!devices.TryGetValue(address, out var device))
                {
                    device = new SspDevice(this, link, address);
                    devices[address] = device;
                }

                return device;
            }
        }

        private void ThrowIfDisposed()
        {
            if (disposed) throw new ObjectDisposedException(nameof(SspBus));
        }

        /// <summary>
        /// Closes the bus.  The stream it was opened over is not owned by the bus and is left open.
        /// </summary>
        public void Dispose()
        {
            if (disposed) return;
            disposed = true;

            link.Dispose();
        }
    }
}

using CCS.SspNet.Communication;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CCS.SspNet.Tests.Fakes
{
    /// <summary>
    /// A scriptable stand-in for an SSP device, speaking the real wire format over a stream.
    /// </summary>
    /// <remarks>
    /// Register a reply per command with <see cref="Respond"/>, then run it against the device end
    /// of an <see cref="SspLoopbackStream"/> pair.  It echoes back the sequence flag it was sent,
    /// as a real device does, and can be told to drop or corrupt replies so that the retry and
    /// resynchronisation paths are reachable from a test.
    /// </remarks>
    public sealed class SspDeviceSimulator
    {
        private readonly Stream stream;
        private readonly SspStreamReader reader;
        private readonly SspStreamWriter writer;
        private readonly Dictionary<byte, Func<byte[], byte[]>> responses = new Dictionary<byte, Func<byte[], byte[]>>();
        private readonly List<byte[]> received = new List<byte[]>();
        private readonly List<bool> receivedFlags = new List<bool>();
        private readonly List<byte> receivedAddresses = new List<byte>();
        private readonly HashSet<byte> addresses;

        public SspDeviceSimulator(Stream stream, byte address = 0x00)
            : this(stream, new[] { address })
        {
        }

        /// <summary>
        /// Stands in for several devices sharing one bus, each answering on its own address.
        /// </summary>
        public SspDeviceSimulator(Stream stream, params byte[] addresses)
        {
            this.stream = stream ?? throw new ArgumentNullException(nameof(stream));
            if (addresses == null || addresses.Length == 0)
            {
                throw new ArgumentException("A simulated device needs at least one address.", nameof(addresses));
            }

            this.addresses = new HashSet<byte>(addresses);
            Address = addresses[0];
            reader = new SspStreamReader(stream);
            writer = new SspStreamWriter(stream);
        }

        /// <summary>Gets the first address this simulated device answers on.</summary>
        public byte Address { get; }

        /// <summary>
        /// Gets the command payloads received so far, oldest first, including retransmissions.
        /// </summary>
        public IReadOnlyList<byte[]> ReceivedCommands => received;

        /// <summary>
        /// Gets the sequence flag carried by each received command, in step with
        /// <see cref="ReceivedCommands"/>.  A retransmission repeats the previous flag.
        /// </summary>
        public IReadOnlyList<bool> ReceivedSequenceFlags => receivedFlags;

        /// <summary>
        /// Gets the address each received command was sent to, in step with
        /// <see cref="ReceivedCommands"/>.
        /// </summary>
        public IReadOnlyList<byte> ReceivedAddresses => receivedAddresses;

        /// <summary>
        /// Gets or sets how many of the next replies to swallow, simulating a device that does not
        /// answer.
        /// </summary>
        public int DropNextReplies { get; set; }

        /// <summary>
        /// Gets or sets how many of the next replies to send with a broken CRC, simulating a
        /// corrupted line.
        /// </summary>
        public int CorruptNextReplies { get; set; }

        /// <summary>Registers the payload to reply with when <paramref name="command"/> arrives.</summary>
        public SspDeviceSimulator Respond(byte command, params byte[] responseData)
        {
            var body = responseData ?? Array.Empty<byte>();
            responses[command] = _ => body;
            return this;
        }

        /// <summary>
        /// Registers a reply that depends on the command's parameters, for commands a real device
        /// answers differently according to what it was asked — setting the protocol version, most
        /// of all, where the same command draws OK or FAIL depending on the version requested.
        /// </summary>
        /// <param name="command">The command code.</param>
        /// <param name="respond">
        /// Takes the whole command payload, code included, and returns the reply payload.
        /// </param>
        public SspDeviceSimulator Respond(byte command, Func<byte[], byte[]> respond)
        {
            responses[command] = respond ?? throw new ArgumentNullException(nameof(respond));
            return this;
        }

        /// <summary>
        /// Serves requests until cancelled or the stream closes.
        /// </summary>
        public async Task RunAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    var request = await reader.ReadRawPacketAsync(cancellationToken).ConfigureAwait(false);
                    await HandleAsync(request, cancellationToken).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException)
            {
                // Expected on shutdown.
            }
            catch (Exceptions.SspConnectionClosedException)
            {
                // The host hung up.
            }
        }

        private async Task HandleAsync(byte[] request, CancellationToken cancellationToken)
        {
            // Read the flag off the wire rather than through Parse, which masks it away.
            var sequenceFlag = (request[1] & Constants.SequenceFlagMask) != 0;
            var address = (byte)(request[1] & Constants.AddressMask);

            var payload = new byte[request[2]];
            Array.Copy(request, 3, payload, 0, payload.Length);
            received.Add(payload);
            receivedFlags.Add(sequenceFlag);
            receivedAddresses.Add(address);

            if (!addresses.Contains(address)) return;

            if (DropNextReplies > 0)
            {
                DropNextReplies--;
                return;
            }

            var command = payload.Length > 0 ? payload[0] : (byte)0x00;
            var body = responses.TryGetValue(command, out var registered)
                ? registered(payload)
                : new[] { (byte)SspResponse.CommandNotKnown };

            var reply = new SspRawPacket(address, body);
            var bytes = reply.GetPacketBytes(sequenceFlag).ToArray();

            if (CorruptNextReplies > 0)
            {
                CorruptNextReplies--;
                // Flip the CRC so the host sees a damaged packet rather than a malformed one.
                bytes[bytes.Length - 1] ^= 0xFF;
            }

            await writer.WriteRawPacketAsync(bytes, cancellationToken).ConfigureAwait(false);
        }
    }
}

using CCS.SspNet.Communication;
using CCS.SspNet.Protocol;
using CCS.SspNet.Security;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Security.Cryptography;
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
        private readonly List<bool> receivedEncrypted = new List<bool>();
        private readonly HashSet<byte> addresses;
        private SimulatedEncryption? encryption;

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
        /// Gets whether each received command arrived encrypted, in step with
        /// <see cref="ReceivedCommands"/>.
        /// </summary>
        public IReadOnlyList<bool> ReceivedEncrypted => receivedEncrypted;

        /// <summary>Gets the session key this device has agreed, if any.</summary>
        public SspEncryptionKey? NegotiatedKey => encryption?.Key;

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
        /// Gives this device the encrypted layer: it will negotiate a key, then decrypt what it is
        /// sent and encrypt what it sends back.
        /// </summary>
        /// <param name="fixedKey">The fixed half of the key it expects the host to know.</param>
        /// <param name="order">How it reads and writes the packet counter.</param>
        /// <param name="requireKey">
        /// When true it answers everything but the key exchange with KEY_NOT_SET until a key has
        /// been agreed, as a device with encryption fitted does.
        /// </param>
        /// <param name="countsReplySeparately">
        /// Which of the two readings of the counter rule it follows: false makes a reply carry the
        /// same count as the command it answers, true makes it carry one more.
        /// </param>
        public SspDeviceSimulator WithEncryption(
            ulong fixedKey = SspEncryptionKey.DefaultFixedHalf,
            SspCountByteOrder order = SspCountByteOrder.LittleEndian,
            bool requireKey = false,
            bool countsReplySeparately = false)
        {
            encryption = new SimulatedEncryption(fixedKey, order, requireKey, countsReplySeparately);
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

            var wire = new byte[request[2]];
            Array.Copy(request, 3, wire, 0, wire.Length);

            if (!addresses.Contains(address))
            {
                Record(wire, sequenceFlag, address, encrypted: false);
                return;
            }

            var encrypted = encryption != null && encryption.IsKeyed && SspEncryptionSession.IsEncrypted(wire);
            var payload = wire;

            if (encrypted && !encryption!.TryUnwrap(wire, out payload))
            {
                // A packet whose counter is not the expected one is discarded without a reply,
                // which is what leaves the host waiting rather than misled.
                Record(wire, sequenceFlag, address, encrypted: true);
                return;
            }

            Record(payload, sequenceFlag, address, encrypted);

            if (DropNextReplies > 0)
            {
                DropNextReplies--;
                return;
            }

            var command = payload.Length > 0 ? payload[0] : (byte)0x00;
            var body = Answer(command, payload);

            if (encrypted) body = encryption!.Wrap(body);

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

        private byte[] Answer(byte command, byte[] payload)
        {
            if (encryption != null)
            {
                var negotiated = encryption.Negotiate(command, payload);
                if (negotiated != null) return negotiated;

                if (encryption.RequireKey && !encryption.IsKeyed)
                {
                    return new[] { (byte)SspResponse.KeyNotSet };
                }
            }

            return responses.TryGetValue(command, out var registered)
                ? registered(payload)
                : new[] { (byte)SspResponse.CommandNotKnown };
        }

        private void Record(byte[] payload, bool sequenceFlag, byte address, bool encrypted)
        {
            received.Add(payload);
            receivedFlags.Add(sequenceFlag);
            receivedAddresses.Add(address);
            receivedEncrypted.Add(encrypted);
        }

        /// <summary>
        /// The device end of the encrypted layer: the key exchange, and the counter that goes with
        /// the key once it is agreed.
        /// </summary>
        private sealed class SimulatedEncryption
        {
            private readonly ulong fixedKey;
            private readonly SspCountByteOrder order;
            private readonly bool countsReplySeparately;

            private ulong generator;
            private ulong modulus;
            private byte[]? keyBytes;
            private uint count;

            internal SimulatedEncryption(ulong fixedKey, SspCountByteOrder order, bool requireKey, bool countsReplySeparately)
            {
                this.fixedKey = fixedKey;
                this.order = order;
                this.countsReplySeparately = countsReplySeparately;
                RequireKey = requireKey;
            }

            internal bool RequireKey { get; }

            internal SspEncryptionKey? Key { get; private set; }

            internal bool IsKeyed => keyBytes != null;

            /// <summary>
            /// Answers the three key-exchange commands, or returns null for anything else.
            /// </summary>
            internal byte[]? Negotiate(byte command, byte[] payload)
            {
                switch (command)
                {
                    case (byte)SspCommand.SetGenerator:
                        return Store(payload, out generator);

                    case (byte)SspCommand.SetModulus:
                        return Store(payload, out modulus);

                    case (byte)SspCommand.RequestKeyExchange:
                        return Exchange(payload);

                    case (byte)SspCommand.ResetFixedEncryptionKey:
                        return new[] { (byte)SspResponse.OK };

                    default:
                        return null;
                }
            }

            private byte[] Store(byte[] payload, out ulong number)
            {
                number = 0;
                if (payload.Length != 9) return new[] { (byte)SspResponse.WrongParameterCount };

                var value = SspValues.ReadUInt64(new ReadOnlySpan<byte>(payload, 1, 8));
                if (!SspPrimes.IsPrime(value)) return new[] { (byte)SspResponse.ParameterOutOfRange };

                number = value;
                return new[] { (byte)SspResponse.OK };
            }

            private byte[] Exchange(byte[] payload)
            {
                if (generator == 0 || modulus == 0) return new[] { (byte)SspResponse.Failure };

                // The implementation guide requires this of a host that makes its own primes, so a
                // device that did not check would let the mistake through to the field.
                if (generator <= modulus) return new[] { (byte)SspResponse.ParameterOutOfRange };

                if (payload.Length != 9) return new[] { (byte)SspResponse.WrongParameterCount };

                var hostIntermediate = SspValues.ReadUInt64(new ReadOnlySpan<byte>(payload, 1, 8));

                var secret = RandomSecret(modulus);
                var intermediate = (ulong)BigInteger.ModPow(generator, secret, modulus);
                var shared = (ulong)BigInteger.ModPow(hostIntermediate, secret, modulus);

                Key = new SspEncryptionKey(fixedKey, shared);
                keyBytes = Key.Value.ToArray();
                count = 0;

                var reply = new byte[9];
                reply[0] = (byte)SspResponse.OK;
                Array.Copy(SspValues.WriteUInt64(intermediate), 0, reply, 1, 8);
                return reply;
            }

            internal bool TryUnwrap(byte[] wire, out byte[] plain)
            {
                uint received;
                byte[] data;

                try
                {
                    (received, data) = SspEncryptedEnvelope.Unwrap(wire, keyBytes!, order);
                }
                catch (Exceptions.SspEncryptionException)
                {
                    // A block that will not decrypt is a host using the wrong key.  A real device
                    // stops answering and stays that way until it is power cycled; going quiet is
                    // the part a test can see.
                    plain = Array.Empty<byte>();
                    return false;
                }

                if (received != count + 1)
                {
                    plain = Array.Empty<byte>();
                    return false;
                }

                count = received;
                plain = data;
                return true;
            }

            internal byte[] Wrap(byte[] plain)
            {
                if (countsReplySeparately) count++;

                using var random = RandomNumberGenerator.Create();
                return SspEncryptedEnvelope.Wrap(plain, keyBytes!, count, order, random);
            }

            private static ulong RandomSecret(ulong modulus)
            {
                using var random = RandomNumberGenerator.Create();
                var buffer = new byte[8];
                random.GetBytes(buffer);

                ulong value = 0;
                for (var i = 0; i < 8; i++) value |= (ulong)buffer[i] << (i * 8);

                return (value % (modulus - 3)) + 2;
            }
        }
    }
}

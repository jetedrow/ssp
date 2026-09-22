using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CCS.SspNet.Communication;
using CCS.SspNet.Exceptions;
using CCS.SspNet.Protocol;
using CCS.SspNet.Security;

namespace CCS.SspNet
{
    /// <summary>
    /// One device on a bus, as a set of awaitable commands.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the procedural half of the library's API: you ask the device to do something and
    /// await the answer.  The event-driven half is a poll loop built on top of these same calls,
    /// and both go through one serializer on the bus, so a command issued from inside an event
    /// handler queues between polls rather than racing one.
    /// </para>
    /// <para>
    /// <see cref="ConnectAsync"/> runs the startup sequence the implementation guide describes.
    /// Until the protocol version has been established, <see cref="PollAsync(CancellationToken)"/> reads at
    /// <see cref="SspProtocolVersion.Lowest"/>, which is right for a device that has never been
    /// set higher and wrong for one that has — so connect before polling.
    /// </para>
    /// </remarks>
    public sealed class SspDevice
    {
        private readonly SspBus bus;
        private readonly SspLink link;

        internal SspDevice(SspBus bus, SspLink link, byte address)
        {
            this.bus = bus;
            this.link = link;
            Address = address;
            ProtocolVersion = SspProtocolVersion.Lowest;
        }

        /// <summary>
        /// Opens a bus over a stream and returns the single device on it.
        /// </summary>
        /// <param name="stream">The transport.</param>
        /// <param name="address">The device's address on the bus.</param>
        /// <param name="options">Timing, retry and decoding settings, or <see langword="null"/> for the defaults.</param>
        /// <returns>
        /// The device.  Its bus is reachable through <see cref="Bus"/> and must be disposed to
        /// release the connection; the stream itself is left open.
        /// </returns>
        public static SspDevice Attach(Stream stream, byte address = 0x00, SspDeviceOptions? options = null) =>
            SspBus.Open(stream, options).Device(address);

        /// <summary>Gets the bus this device is on.</summary>
        public SspBus Bus => bus;

        /// <summary>Gets the device's address on the bus.</summary>
        public byte Address { get; }

        /// <summary>
        /// Gets the protocol version this device is believed to be set to, which decides how its
        /// poll replies are read.
        /// </summary>
        /// <remarks>
        /// Set by <see cref="ConnectAsync"/>, <see cref="SetProtocolVersionAsync"/> and
        /// <see cref="NegotiateProtocolVersionAsync"/>.  Before any of those have run it is
        /// <see cref="SspProtocolVersion.Lowest"/>.
        /// </remarks>
        public SspProtocolVersion ProtocolVersion { get; private set; }

        /// <summary>
        /// Gets what the device reported the last time <see cref="SetupRequestAsync"/> ran, or
        /// <see langword="null"/> if it has not run.
        /// </summary>
        public SspSetup? Setup { get; private set; }

        // ---- the escape hatch -------------------------------------------------------------

        /// <summary>
        /// Sends a command and returns the reply, without checking whether the device accepted it.
        /// </summary>
        /// <param name="command">The command code.</param>
        /// <param name="parameters">The bytes that follow it.</param>
        /// <param name="cancellationToken">Cancels the exchange.</param>
        /// <remarks>
        /// Every typed command below is a call to this.  It takes a raw <see cref="byte"/> so that
        /// a device capability this library has not modelled is still reachable.
        /// </remarks>
        public async Task<SspReply> SendAsync(byte command, byte[]? parameters = null, CancellationToken cancellationToken = default)
        {
            var packet = await link.ExchangeAsync(
                Address, SspMessage.Create(command, parameters ?? Array.Empty<byte>()), cancellationToken)
                .ConfigureAwait(false);

            return SspReply.Parse(packet.Data);
        }

        /// <inheritdoc cref="SendAsync(byte, byte[], CancellationToken)"/>
        public Task<SspReply> SendAsync(SspCommand command, byte[]? parameters = null, CancellationToken cancellationToken = default) =>
            SendAsync((byte)command, parameters, cancellationToken);

        /// <summary>
        /// Sends a command and throws unless the device accepted it.
        /// </summary>
        /// <exception cref="SspResponseException">The device refused or failed the command.</exception>
        public async Task<SspReply> SendCheckedAsync(SspCommand command, byte[]? parameters = null, CancellationToken cancellationToken = default)
        {
            var reply = await SendAsync(command, parameters, cancellationToken).ConfigureAwait(false);
            reply.EnsureOk();
            return reply;
        }

        // ---- connecting -------------------------------------------------------------------

        /// <summary>
        /// Runs the startup sequence: synchronise, agree a protocol version, and read the device's
        /// setup.
        /// </summary>
        /// <param name="cancellationToken">Cancels the sequence.</param>
        /// <returns>What the device reported about itself.</returns>
        /// <remarks>
        /// <para>
        /// The version is settled before the setup is read, and the setup is read again afterwards,
        /// because a validator's channel data is laid out differently from protocol version 6 — so
        /// the second read is the one that parses correctly.
        /// </para>
        /// <para>
        /// This does not enable the device or lift its channel inhibits.  A validator will not
        /// accept anything until <see cref="SetChannelInhibitsAsync(ushort, CancellationToken)"/>
        /// and <see cref="EnableAsync"/> have both run, which is deliberate: it gives a host the
        /// chance to look at what it has connected to first.
        /// </para>
        /// </remarks>
        public async Task<SspSetup> ConnectAsync(CancellationToken cancellationToken = default)
        {
            await SyncAsync(cancellationToken).ConfigureAwait(false);
            await NegotiateProtocolVersionAsync(cancellationToken: cancellationToken).ConfigureAwait(false);

            return await SetupRequestAsync(cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Finds the highest protocol version both this host and the device support, and sets the
        /// device to it.
        /// </summary>
        /// <param name="highest">
        /// The highest version to try, or <see langword="null"/> for
        /// <see cref="SspDeviceOptions.HighestProtocolVersion"/>.
        /// </param>
        /// <param name="cancellationToken">Cancels the exchange.</param>
        /// <returns>The version the device is now set to.</returns>
        /// <remarks>
        /// The protocol gives no way to ask a device what it supports, only to ask it to change:
        /// it answers OK if it can and FAIL if it cannot.  So this walks down from the highest
        /// version until one is accepted.  Never setting a device above what this host can decode
        /// is the whole point — the manual is explicit that a host must not.
        /// </remarks>
        public async Task<SspProtocolVersion> NegotiateProtocolVersionAsync(
            SspProtocolVersion? highest = null, CancellationToken cancellationToken = default)
        {
            var ceiling = highest ?? bus.Options.HighestProtocolVersion;

            for (var version = ceiling.Value; version >= SspProtocolVersion.Lowest.Value; version--)
            {
                var reply = await SendAsync(
                    SspCommand.HostProtocolVersion, new[] { version }, cancellationToken).ConfigureAwait(false);

                if (reply.IsOk)
                {
                    ProtocolVersion = new SspProtocolVersion(version);
                    return ProtocolVersion;
                }

                if (reply.Code != SspResponse.Failure)
                {
                    // FAIL means "not that version". Anything else is a different problem and
                    // stepping down will not fix it.
                    reply.EnsureOk();
                }
            }

            throw new SspResponseException(
                $"The device at address {Address} accepted no protocol version between " +
                $"{SspProtocolVersion.Lowest} and {ceiling}.");
        }

        /// <summary>
        /// Sets the device to a specific protocol version.
        /// </summary>
        /// <param name="version">The version to set.</param>
        /// <param name="cancellationToken">Cancels the exchange.</param>
        /// <exception cref="SspResponseException">The device does not support that version.</exception>
        public async Task SetProtocolVersionAsync(SspProtocolVersion version, CancellationToken cancellationToken = default)
        {
            await SendCheckedAsync(
                SspCommand.HostProtocolVersion, new[] { version.Value }, cancellationToken).ConfigureAwait(false);

            ProtocolVersion = version;
        }

        // ---- encryption -------------------------------------------------------------------

        /// <summary>
        /// Gets whether commands to this device are being encrypted.
        /// </summary>
        public bool IsEncrypted => link.EncryptionFor(Address) != null;

        /// <summary>
        /// Agrees a session key with the device, after which every command it is sent is
        /// encrypted.
        /// </summary>
        /// <param name="options">
        /// The fixed half of the key and how the exchange picks its numbers, or
        /// <see langword="null"/> for <see cref="SspDeviceOptions.Encryption"/>.
        /// </param>
        /// <param name="cancellationToken">Cancels the sequence.</param>
        /// <returns>The key now in use.  Its negotiated half is different every session.</returns>
        /// <remarks>
        /// <para>
        /// A device that has encryption fitted answers every command with
        /// <see cref="SspResponse.KeyNotSet"/> until this has run, without doing what it was asked
        /// — so on such a device this comes first, before even
        /// <see cref="ConnectAsync"/>.  On a device without encryption the first command of the
        /// exchange comes back unknown, and this throws rather than leaving the link half
        /// configured.
        /// </para>
        /// <para>
        /// The three numbers go out in clear, which is safe: watching them does not give away the
        /// key.  What an observer cannot see is the random number each side keeps, and the key is
        /// built from both.
        /// </para>
        /// <para>
        /// This can be run again at any time.  Doing so resets the packet counter on both sides,
        /// which is the way back from a conversation that has lost its place — and the three
        /// commands go in clear whether or not a session was already running, because the device
        /// changes key as it answers the last of them.
        /// </para>
        /// </remarks>
        /// <exception cref="SspResponseException">The device refused one of the three commands.</exception>
        public async Task<SspEncryptionKey> NegotiateKeysAsync(
            SspEncryptionOptions? options = null, CancellationToken cancellationToken = default)
        {
            var settings = options ?? bus.Options.Encryption;

            // The exchange itself goes in clear, even when a session is already running.  The
            // device swaps to the new key the moment it answers the third command, so a reply sent
            // under it could not be read by a host still holding the old one -- and renegotiating
            // is exactly what a host does when the encrypted channel has lost its place, which is
            // when it can least afford to need that channel.
            link.DisableEncryption(Address);

            var exchange = settings.Parameters is SspKeyExchangeParameters parameters
                ? SspKeyExchange.Create(parameters)
                : SspKeyExchange.Create(settings.PrimeBits);

            // The order matters: a device answers Request Key Exchange with FAIL until it has both
            // numbers.
            await SendCheckedAsync(
                SspCommand.SetGenerator, SspValues.WriteUInt64(exchange.Parameters.Generator), cancellationToken)
                .ConfigureAwait(false);

            await SendCheckedAsync(
                SspCommand.SetModulus, SspValues.WriteUInt64(exchange.Parameters.Modulus), cancellationToken)
                .ConfigureAwait(false);

            var reply = await SendCheckedAsync(
                SspCommand.RequestKeyExchange, SspValues.WriteUInt64(exchange.HostIntermediate), cancellationToken)
                .ConfigureAwait(false);

            if (reply.Data.Length < 8)
            {
                throw new SspResponseException(
                    $"The device at address {Address} accepted the key exchange but replied with " +
                    $"{reply.Data.Length} bytes where its half of the key needs eight.");
            }

            var key = new SspEncryptionKey(
                settings.FixedKey, exchange.CreateSharedSecret(SspValues.ReadUInt64(reply.Data.Span)));

            link.EnableEncryption(Address, new SspEncryptionSession(key, settings.CountByteOrder));
            return key;
        }

        /// <summary>
        /// Stops encrypting commands to this device and forgets the session key.
        /// </summary>
        /// <remarks>
        /// This is a decision made at this end only; the device keeps its key and will still accept
        /// encrypted commands.  What it will not accept in clear are the commands that move money.
        /// </remarks>
        public void StopEncrypting() => link.DisableEncryption(Address);

        /// <summary>
        /// Changes the fixed half of the key the device expects — the half a machine's
        /// manufacturer sets.
        /// </summary>
        /// <param name="fixedKey">The new fixed half.</param>
        /// <param name="cancellationToken">Cancels the exchange.</param>
        /// <remarks>
        /// <para>
        /// The device only accepts this encrypted, so the current key has to be known to set a new
        /// one.  That is the point: it is what stops someone swapping a device into a machine it
        /// was not sold for.
        /// </para>
        /// <para>
        /// The session ends here.  Once the device has changed its half of the key, what the two
        /// ends share is no longer the same, so this stops encrypting and leaves it to the caller
        /// to run <see cref="NegotiateKeysAsync"/> again with the new fixed half — which also
        /// puts both packet counters back to zero.
        /// </para>
        /// </remarks>
        /// <exception cref="InvalidOperationException">The session is not encrypted.</exception>
        public async Task SetFixedKeyAsync(ulong fixedKey, CancellationToken cancellationToken = default)
        {
            if (!IsEncrypted)
            {
                throw new InvalidOperationException(
                    "A device only accepts a new fixed key encrypted, so negotiate a session key first.");
            }

            await SendCheckedAsync(
                SspCommand.SetFixedEncryptionKey, SspValues.WriteUInt64(fixedKey), cancellationToken)
                .ConfigureAwait(false);

            StopEncrypting();
        }

        /// <summary>
        /// Puts the fixed half of the key back to what the device left the factory with.
        /// </summary>
        /// <param name="cancellationToken">Cancels the exchange.</param>
        /// <remarks>
        /// As with <see cref="SetFixedKeyAsync"/>, the session ends here and has to be negotiated
        /// again — with <see cref="SspEncryptionKey.DefaultFixedHalf"/> this time.
        /// </remarks>
        /// <exception cref="InvalidOperationException">The session is not encrypted.</exception>
        public async Task ResetFixedKeyAsync(CancellationToken cancellationToken = default)
        {
            if (!IsEncrypted)
            {
                throw new InvalidOperationException(
                    "A device only accepts this encrypted, so negotiate a session key first.");
            }

            await SendCheckedAsync(SspCommand.ResetFixedEncryptionKey, null, cancellationToken).ConfigureAwait(false);

            StopEncrypting();
        }

        // ---- the everyday commands ---------------------------------------------------------

        /// <summary>
        /// Resets the sequence flag at both ends, which is also the usual way to check a device is
        /// there at all.
        /// </summary>
        public async Task SyncAsync(CancellationToken cancellationToken = default)
        {
            // The device resets its own flag on SYNC, so the link has to reset its copy too or the
            // two ends disagree about what the next command's flag should be.
            link.ResetSequence(Address);
            await SendCheckedAsync(SspCommand.Sync, null, cancellationToken).ConfigureAwait(false);
            link.ResetSequence(Address);
        }

        /// <summary>Restarts the device, as a power cycle would.</summary>
        public Task ResetAsync(CancellationToken cancellationToken = default) =>
            SendCheckedAsync(SspCommand.Reset, null, cancellationToken);

        /// <summary>Puts the device into its enabled state, where it will accept money.</summary>
        public Task EnableAsync(CancellationToken cancellationToken = default) =>
            SendCheckedAsync(SspCommand.Enable, null, cancellationToken);

        /// <summary>Puts the device into its disabled state, where it will not accept anything.</summary>
        public Task DisableAsync(CancellationToken cancellationToken = default) =>
            SendCheckedAsync(SspCommand.Disable, null, cancellationToken);

        /// <summary>Lights the bezel.</summary>
        public Task DisplayOnAsync(CancellationToken cancellationToken = default) =>
            SendCheckedAsync(SspCommand.DisplayOn, null, cancellationToken);

        /// <summary>Turns the bezel light off.</summary>
        public Task DisplayOffAsync(CancellationToken cancellationToken = default) =>
            SendCheckedAsync(SspCommand.DisplayOff, null, cancellationToken);

        /// <summary>
        /// Rejects the note currently held in escrow, returning it to the user.
        /// </summary>
        public Task RejectBanknoteAsync(CancellationToken cancellationToken = default) =>
            SendCheckedAsync(SspCommand.RejectBanknote, null, cancellationToken);

        /// <summary>
        /// Holds a note in escrow for another poll interval instead of accepting it.
        /// </summary>
        /// <remarks>
        /// A validator accepts an escrowed note on the next poll unless told otherwise, so a host
        /// that needs longer to decide has to keep sending this.
        /// </remarks>
        public Task HoldAsync(CancellationToken cancellationToken = default) =>
            SendCheckedAsync(SspCommand.Hold, null, cancellationToken);

        /// <summary>
        /// Says which channels may accept notes.
        /// </summary>
        /// <param name="channelMask">
        /// A bit per channel, the lowest bit being channel 1.  A set bit allows acceptance; a clear
        /// bit inhibits it.  <see cref="ushort.MaxValue"/> allows all sixteen.
        /// </param>
        /// <param name="cancellationToken">Cancels the exchange.</param>
        /// <remarks>
        /// A device accepts nothing until this has been sent, whatever its enabled state.
        /// </remarks>
        public Task SetChannelInhibitsAsync(ushort channelMask, CancellationToken cancellationToken = default) =>
            SetChannelInhibitsAsync(new[] { (byte)channelMask, (byte)(channelMask >> 8) }, cancellationToken);

        /// <summary>
        /// Says which channels may accept notes, for a device with more than sixteen.
        /// </summary>
        /// <param name="channelMask">
        /// One byte per eight channels, lowest channels first.  An NV200 takes up to eight bytes;
        /// other validators take one or two.
        /// </param>
        /// <param name="cancellationToken">Cancels the exchange.</param>
        public Task SetChannelInhibitsAsync(byte[] channelMask, CancellationToken cancellationToken = default)
        {
            if (channelMask == null) throw new ArgumentNullException(nameof(channelMask));
            if (channelMask.Length == 0)
            {
                throw new ArgumentException("A channel mask needs at least one byte.", nameof(channelMask));
            }

            return SendCheckedAsync(SspCommand.SetChannelInhibits, channelMask, cancellationToken);
        }

        /// <summary>
        /// Asks the device what it is, and remembers the answer in <see cref="Setup"/>.
        /// </summary>
        /// <remarks>
        /// If the reply names a protocol version, <see cref="ProtocolVersion"/> is updated to it —
        /// this is the only way to learn what version a device is already at.
        /// </remarks>
        public async Task<SspSetup> SetupRequestAsync(CancellationToken cancellationToken = default)
        {
            var reply = await SendCheckedAsync(SspCommand.SetupRequest, null, cancellationToken).ConfigureAwait(false);
            var setup = SspSetup.Parse(reply.Data.Span);

            Setup = setup;
            if (setup.ProtocolVersion is SspProtocolVersion reported) ProtocolVersion = reported;

            return setup;
        }

        /// <summary>Gets the device's factory-programmed serial number.</summary>
        public async Task<uint> GetSerialNumberAsync(CancellationToken cancellationToken = default)
        {
            var reply = await SendCheckedAsync(SspCommand.GetSerialNumber, null, cancellationToken).ConfigureAwait(false);

            if (reply.Data.Length < 4)
            {
                throw new PacketFormatException(
                    $"A serial number is four bytes; the device sent {reply.Data.Length}.");
            }

            // Serial numbers are big-endian, unlike the amounts in event payloads.
            return SspValues.ReadBigEndian(reply.Data.Span);
        }

        /// <summary>Gets the device's firmware version as the ASCII string it reports.</summary>
        public async Task<string> GetFirmwareVersionAsync(CancellationToken cancellationToken = default)
        {
            var reply = await SendCheckedAsync(SspCommand.GetFirmwareVersion, null, cancellationToken).ConfigureAwait(false);
            return Ascii(reply.Data.Span);
        }

        /// <summary>Gets the loaded dataset's version as the ASCII string the device reports.</summary>
        public async Task<string> GetDatasetVersionAsync(CancellationToken cancellationToken = default)
        {
            var reply = await SendCheckedAsync(SspCommand.GetDatasetVersion, null, cancellationToken).ConfigureAwait(false);
            return Ascii(reply.Data.Span);
        }

        // ---- polling -----------------------------------------------------------------------

        /// <summary>
        /// Asks the device what has happened since the last poll.
        /// </summary>
        /// <param name="cancellationToken">Cancels the exchange.</param>
        /// <returns>
        /// The events, read at <see cref="ProtocolVersion"/>.  Check
        /// <see cref="SspPollResult.IsComplete"/>: a reply containing an event this library has no
        /// payload length for is reported rather than guessed at.
        /// </returns>
        /// <remarks>
        /// A validator also takes the poll as permission to accept a note sitting in escrow, so
        /// polling is not a read-only operation.
        /// </remarks>
        public Task<SspPollResult> PollAsync(CancellationToken cancellationToken = default) =>
            PollAsync(SspCommand.Poll, cancellationToken);

        /// <summary>
        /// Polls, and holds each event until it is acknowledged with
        /// <see cref="EventAcknowledgeAsync"/>.
        /// </summary>
        /// <remarks>
        /// The device repeats an unacknowledged event on every poll, so a host that crashes between
        /// reading a credit and recording it sees the credit again rather than losing it.  Only
        /// some events support this; the rest behave as they do under an ordinary poll.
        /// </remarks>
        public Task<SspPollResult> PollWithAckAsync(CancellationToken cancellationToken = default) =>
            PollAsync(SspCommand.PollWithAck, cancellationToken);

        /// <summary>
        /// Acknowledges the events from the last <see cref="PollWithAckAsync"/>, letting the device
        /// move on.
        /// </summary>
        public Task EventAcknowledgeAsync(CancellationToken cancellationToken = default) =>
            SendCheckedAsync(SspCommand.EventAck, null, cancellationToken);

        private async Task<SspPollResult> PollAsync(SspCommand command, CancellationToken cancellationToken)
        {
            var reply = await SendCheckedAsync(command, null, cancellationToken).ConfigureAwait(false);

            return SspPollDecoder.Decode(reply.Data.Span, ProtocolVersion, bus.Options.EventTable);
        }

        private static string Ascii(ReadOnlySpan<byte> data)
        {
#if NETSTANDARD2_0
            return System.Text.Encoding.ASCII.GetString(data.ToArray());
#else
            return System.Text.Encoding.ASCII.GetString(data);
#endif
        }

        /// <inheritdoc />
        public override string ToString() =>
            Setup is null
                ? $"SSP device at address {Address}"
                : $"{Setup.UnitType} at address {Address}, protocol version {ProtocolVersion}";
    }
}

using CCS.SspNet.Communication;
using CCS.SspNet.Firmware;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CCS.SspNet.Tests.Firmware
{
    /// <summary>
    /// The device end of a firmware download: it speaks the framed opening, then drops to raw bytes
    /// exactly as the implementation guide's flow does, checksumming each block as a real device
    /// would.
    /// </summary>
    /// <remarks>
    /// It is told the file's shape up front — the sizes a real device learns from the header and
    /// its own update code — so it knows when each phase ends.  The checksums it returns are
    /// computed over the bytes it actually receives, so a download that sent the wrong bytes, sized
    /// a block wrong, or miscalculated a checksum is caught here rather than passed.
    /// </remarks>
    public sealed class SspDownloadTargetSimulator
    {
        private readonly Stream stream;
        private readonly SspStreamReader reader;
        private readonly SspStreamWriter writer;
        private readonly int ramSize;
        private readonly int payloadSize;
        private readonly int blockSize;
        private readonly bool acceptHeader;
        private readonly int syncsToFailAfterReset;

        private readonly List<byte> ramReceived = new List<byte>();
        private readonly List<byte> payloadReceived = new List<byte>();
        private readonly List<byte[]> headersReceived = new List<byte[]>();

        public SspDownloadTargetSimulator(
            Stream stream,
            SspFirmwareFile file,
            int blockSize = 4096,
            bool acceptHeader = true,
            int syncsToFailAfterReset = 0)
        {
            this.stream = stream ?? throw new ArgumentNullException(nameof(stream));
            reader = new SspStreamReader(stream);
            writer = new SspStreamWriter(stream);
            ramSize = file.RamBlockLength;
            payloadSize = file.PayloadLength;
            this.blockSize = blockSize;
            this.acceptHeader = acceptHeader;
            this.syncsToFailAfterReset = syncsToFailAfterReset;
            ExpectedUpdateCode = file.UpdateCode;
        }

        /// <summary>Gets the update code the download told the device to run.</summary>
        public byte ExpectedUpdateCode { get; }

        /// <summary>Gets the update code byte the device actually received in the raw phase.</summary>
        public byte? ReceivedUpdateCode { get; private set; }

        /// <summary>Gets the RAM block bytes the device received.</summary>
        public IReadOnlyList<byte> RamReceived => ramReceived;

        /// <summary>Gets the payload bytes the device received.</summary>
        public IReadOnlyList<byte> PayloadReceived => payloadReceived;

        /// <summary>Gets the header blocks the device received (once framed, once raw).</summary>
        public IReadOnlyList<byte[]> HeadersReceived => headersReceived;

        /// <summary>Gets whether every payload block's host checksum matched the device's own.</summary>
        public bool AllPayloadChecksumsMatched { get; private set; } = true;

        /// <summary>Gets whether the device reached the end of the download and reset.</summary>
        public bool Completed { get; private set; }

        public async Task RunAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                if (!await RunFramedOpeningAsync(cancellationToken).ConfigureAwait(false)) return;
                await RunRawTransferAsync(cancellationToken).ConfigureAwait(false);
                await RunRestartAsync(cancellationToken).ConfigureAwait(false);
                Completed = true;
            }
            catch (OperationCanceledException)
            {
            }
            catch (CCS.SspNet.Exceptions.SspConnectionClosedException)
            {
            }
        }

        /// <summary>Returns true once the header was accepted and the raw phase should begin.</summary>
        private async Task<bool> RunFramedOpeningAsync(CancellationToken cancellationToken)
        {
            while (true)
            {
                var packet = await reader.ReadRawPacketAsync(cancellationToken).ConfigureAwait(false);
                var request = SspRawPacket.Parse(packet);
                var flag = (packet[1] & Constants.SequenceFlagMask) != 0;
                var payload = request.Data;
                var command = payload.Length > 0 ? payload[0] : (byte)0x00;

                if (command == 0x11 && payload.Length == 1)
                {
                    await ReplyAsync(flag, new[] { (byte)SspResponse.OK }, cancellationToken).ConfigureAwait(false);
                }
                else if (command == 0x0B)
                {
                    await ReplyAsync(flag, new[] { (byte)SspResponse.OK, (byte)(blockSize & 0xFF), (byte)(blockSize >> 8) }, cancellationToken)
                        .ConfigureAwait(false);
                }
                else if (payload.Length == SspFirmwareFile.HeaderLength)
                {
                    headersReceived.Add(payload);
                    if (!acceptHeader)
                    {
                        await ReplyAsync(flag, new[] { (byte)SspResponse.HeaderFailure }, cancellationToken).ConfigureAwait(false);
                        return false;
                    }

                    await ReplyAsync(flag, new[] { (byte)SspResponse.OK }, cancellationToken).ConfigureAwait(false);
                    return true;
                }
                else
                {
                    await ReplyAsync(flag, new[] { (byte)SspResponse.CommandNotKnown }, cancellationToken).ConfigureAwait(false);
                }
            }
        }

        private async Task RunRawTransferAsync(CancellationToken cancellationToken)
        {
            // RAM block: read it all, answer with our checksum (no host checksum precedes it).
            var ram = await ReadRawAsync(ramSize, cancellationToken).ConfigureAwait(false);
            ramReceived.AddRange(ram);
            await stream.WriteAsync(new[] { Xor(ram) }, 0, 1, cancellationToken).ConfigureAwait(false);
            await stream.FlushAsync(cancellationToken).ConfigureAwait(false);

            // Update code, then acknowledge.
            var updateCode = await ReadRawAsync(1, cancellationToken).ConfigureAwait(false);
            ReceivedUpdateCode = updateCode[0];
            await AcknowledgeAsync(cancellationToken).ConfigureAwait(false);

            // Header again, then acknowledge.
            var header = await ReadRawAsync(SspFirmwareFile.HeaderLength, cancellationToken).ConfigureAwait(false);
            headersReceived.Add(header);
            await AcknowledgeAsync(cancellationToken).ConfigureAwait(false);

            // Payload, block by block: read the block, read the host's checksum, answer with ours.
            var remaining = payloadSize;
            while (remaining > 0)
            {
                var length = Math.Min(blockSize, remaining);
                var block = await ReadRawAsync(length, cancellationToken).ConfigureAwait(false);
                payloadReceived.AddRange(block);

                var hostChecksum = (await ReadRawAsync(1, cancellationToken).ConfigureAwait(false))[0];
                var ourChecksum = Xor(block);
                if (hostChecksum != ourChecksum) AllPayloadChecksumsMatched = false;

                await stream.WriteAsync(new[] { ourChecksum }, 0, 1, cancellationToken).ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);

                remaining -= length;
            }
        }

        private async Task RunRestartAsync(CancellationToken cancellationToken)
        {
            var syncsSeen = 0;
            while (true)
            {
                var packet = await reader.ReadRawPacketAsync(cancellationToken).ConfigureAwait(false);
                var flag = (packet[1] & Constants.SequenceFlagMask) != 0;
                var request = SspRawPacket.Parse(packet);
                var command = request.Data.Length > 0 ? request.Data[0] : (byte)0x00;

                if (command != 0x11) continue;

                syncsSeen++;
                if (syncsSeen > syncsToFailAfterReset)
                {
                    await ReplyAsync(flag, new[] { (byte)SspResponse.OK }, cancellationToken).ConfigureAwait(false);
                    return;
                }

                await ReplyAsync(flag, new[] { (byte)SspResponse.Failure }, cancellationToken).ConfigureAwait(false);
            }
        }

        private async Task AcknowledgeAsync(CancellationToken cancellationToken)
        {
            await stream.WriteAsync(new byte[] { 0x32 }, 0, 1, cancellationToken).ConfigureAwait(false);
            await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
        }

        private async Task ReplyAsync(bool flag, byte[] body, CancellationToken cancellationToken)
        {
            var reply = new SspRawPacket(0x00, body);
            var bytes = reply.GetPacketBytes(flag).ToArray();
            await writer.WriteRawPacketAsync(bytes, cancellationToken).ConfigureAwait(false);
        }

        private async Task<byte[]> ReadRawAsync(int count, CancellationToken cancellationToken)
        {
            var buffer = new byte[count];
            var read = 0;
            while (read < count)
            {
                var n = await stream.ReadAsync(buffer, read, count - read, cancellationToken).ConfigureAwait(false);
                if (n == 0) throw new CCS.SspNet.Exceptions.SspConnectionClosedException("The host closed mid-download.");
                read += n;
            }

            return buffer;
        }

        private static byte Xor(IEnumerable<byte> data)
        {
            byte checksum = 0;
            foreach (var b in data) checksum ^= b;
            return checksum;
        }
    }
}

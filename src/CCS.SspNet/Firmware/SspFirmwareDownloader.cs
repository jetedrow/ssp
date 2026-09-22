using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CCS.SspNet.Communication;
using CCS.SspNet.Exceptions;

namespace CCS.SspNet.Firmware
{
    /// <summary>
    /// Writes a firmware or dataset file to a device.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the one operation in the library that is dangerous to get wrong: it replaces the
    /// program the device runs, and ITL's own guidance says a botched download can leave a unit
    /// damaged.  So it is deliberately its own thing, run on its own, not a method on a device that
    /// might be polling — a download owns the connection for its whole length and shares it with
    /// nothing.
    /// </para>
    /// <para>
    /// Two shapes of traffic go down one connection.  The start of a download is ordinary SSP
    /// commands — a sync, the command that announces the download and reports the block size, and
    /// the header the device accepts or rejects.  After that the framing is dropped and the file is
    /// written as raw bytes at a faster line speed, each block answered by a one-byte checksum the
    /// device calculates and the host checks.  When the last block lands the device resets, and the
    /// download waits for it to come back before calling itself done.
    /// </para>
    /// <para>
    /// The transfer speed is raised through <see cref="SspDownloadOptions.BaudRateControl"/> when a
    /// transport offers it, and left alone when it does not — which is right for an in-memory or
    /// network stream, and a thing to know for a real serial device, whose faster half the host has
    /// to follow it to.
    /// </para>
    /// </remarks>
    public static class SspFirmwareDownloader
    {
        private const byte Acknowledge = 0x32;
        private const int SectionLength = SspFirmwareFile.HeaderLength; // the raw phase writes in 128-byte sections

        /// <summary>
        /// Writes a file to the single device on a stream.
        /// </summary>
        /// <param name="stream">The connection to the device, owned by the download for its length.</param>
        /// <param name="file">The parsed firmware or dataset file.</param>
        /// <param name="options">Speed control and timings, or <see langword="null"/> for the defaults.</param>
        /// <param name="progress">Told how far the download has got, or <see langword="null"/>.</param>
        /// <param name="cancellationToken">
        /// Cancels the download.  Cancelling mid-transfer leaves the device part way through an
        /// update, so a cancelled download is reported, not swallowed.
        /// </param>
        /// <exception cref="SspDownloadException">
        /// The device refused the file, a checksum did not match, or the device did not come back
        /// after resetting.
        /// </exception>
        public static async Task DownloadAsync(
            Stream stream,
            SspFirmwareFile file,
            SspDownloadOptions? options = null,
            IProgress<SspDownloadProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            if (file == null) throw new ArgumentNullException(nameof(file));

            var settings = options ?? new SspDownloadOptions();
            var baud = settings.BaudRateControl;
            var total = (long)file.RamBlockLength + file.PayloadLength;
            long sent = 0;

            using var writer = new SspStreamWriter(stream);
            using var reader = new SspStreamReader(stream);
            var sequence = new SequenceFlag();

            void Report(SspDownloadStage stage) => progress?.Report(new SspDownloadProgress(stage, sent, total));

            // --- the framed phase --------------------------------------------------------------

            Report(SspDownloadStage.Synchronising);
            var sync = await ExchangeAsync(writer, reader, sequence, settings, new byte[] { 0x11 }, cancellationToken)
                .ConfigureAwait(false);
            EnsureOk(sync, "The device did not answer the sync that opens a download.");

            Report(SspDownloadStage.Preparing);
            var prepare = await ExchangeAsync(writer, reader, sequence, settings, new byte[] { 0x0B, 0x03 }, cancellationToken)
                .ConfigureAwait(false);
            EnsureOk(prepare, "The device refused the command that begins a download.");

            if (prepare.Length < 3)
            {
                throw new SspDownloadException(
                    "The device accepted the download but did not report a block size, so there is no size to send the payload in.");
            }

            var blockSize = prepare[1] | (prepare[2] << 8);
            if (blockSize <= 0)
            {
                throw new SspDownloadException($"The device reported a block size of {blockSize}, which cannot be used.");
            }

            Report(SspDownloadStage.SendingHeader);
            var headerReply = await ExchangeAsync(writer, reader, sequence, settings, file.Header.ToArray(), cancellationToken)
                .ConfigureAwait(false);

            if (headerReply.Length >= 1 && headerReply[0] == (byte)SspResponse.HeaderFailure)
            {
                throw new SspDownloadException(
                    "The device rejected the header: this file is not a firmware or dataset for this device. " +
                    "Nothing has been overwritten.");
            }

            EnsureOk(headerReply, "The device did not accept the download header.");

            // --- the raw phase -----------------------------------------------------------------

            SetBaud(baud, settings.TransferBaudRate);

            Report(SspDownloadStage.SendingRamBlock);

            // The RAM block gets no host checksum: the device sends the one it calculated on its
            // own once the whole block has arrived, and the host checks that against its own.
            var ramChecksum = await WriteRawAsync(stream, file.RamBlock, cancellationToken).ConfigureAwait(false);
            await ExpectChecksumAsync(stream, ramChecksum, settings, "the RAM block", cancellationToken).ConfigureAwait(false);
            sent += file.RamBlockLength;
            Report(SspDownloadStage.SendingRamBlock);

            await Task.Delay(settings.RamExecutionDelay, cancellationToken).ConfigureAwait(false);
            baud?.DiscardBuffers();

            await WriteByteAsync(stream, file.UpdateCode, cancellationToken).ConfigureAwait(false);
            await ExpectAckAsync(stream, settings, "the update code", cancellationToken).ConfigureAwait(false);

            await WriteRawAsync(stream, file.Header, cancellationToken).ConfigureAwait(false);
            await ExpectAckAsync(stream, settings, "the header resend", cancellationToken).ConfigureAwait(false);

            Report(SspDownloadStage.SendingPayload);
            var payload = file.Payload;
            for (var offset = 0; offset < payload.Length; offset += blockSize)
            {
                var length = Math.Min(blockSize, payload.Length - offset);
                var block = payload.Slice(offset, length);

                var checksum = await WriteRawAsync(stream, block, cancellationToken).ConfigureAwait(false);

                // Unlike the RAM block, a payload block ends with the host sending its own checksum;
                // the device then answers with the one it calculated, and the two must agree.
                await WriteByteAsync(stream, checksum, cancellationToken).ConfigureAwait(false);
                await ExpectChecksumAsync(stream, checksum, settings, "a payload block", cancellationToken).ConfigureAwait(false);

                sent += length;
                Report(SspDownloadStage.SendingPayload);
            }

            // --- coming back -------------------------------------------------------------------

            Report(SspDownloadStage.Restarting);
            SetBaud(baud, settings.NormalBaudRate);
            baud?.DiscardBuffers();

            await WaitForRestartAsync(writer, reader, settings, cancellationToken).ConfigureAwait(false);
            Report(SspDownloadStage.Complete);
        }

        private static void SetBaud(Interfaces.ISspBaudRateControl? baud, int rate)
        {
            if (baud != null) baud.BaudRate = rate;
        }

        /// <summary>Writes a run of bytes in 128-byte sections, returning the XOR of every byte.</summary>
        private static async Task<byte> WriteRawAsync(Stream stream, ReadOnlyMemory<byte> data, CancellationToken cancellationToken)
        {
            byte checksum = 0;
            for (var i = 0; i < data.Length; i++) checksum ^= data.Span[i];

            for (var offset = 0; offset < data.Length; offset += SectionLength)
            {
                var length = Math.Min(SectionLength, data.Length - offset);
                var section = data.Slice(offset, length);
#if NETSTANDARD2_0
                var array = section.ToArray();
                await stream.WriteAsync(array, 0, array.Length, cancellationToken).ConfigureAwait(false);
#else
                await stream.WriteAsync(section, cancellationToken).ConfigureAwait(false);
#endif
            }

            await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            return checksum;
        }

        private static async Task WriteByteAsync(Stream stream, byte value, CancellationToken cancellationToken)
        {
            var buffer = new[] { value };
            await stream.WriteAsync(buffer, 0, 1, cancellationToken).ConfigureAwait(false);
            await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
        }

        private static async Task ExpectChecksumAsync(
            Stream stream, byte expected, SspDownloadOptions settings, string what, CancellationToken cancellationToken)
        {
            var got = await ReadRawByteAsync(stream, settings.RawResponseTimeout, cancellationToken).ConfigureAwait(false);
            if (got != expected)
            {
                throw new SspDownloadException(
                    $"The checksum for {what} did not match: the device made it 0x{got:X2} where the host made it 0x{expected:X2}. " +
                    "The block did not arrive intact, so the download has been stopped.");
            }
        }

        private static async Task ExpectAckAsync(
            Stream stream, SspDownloadOptions settings, string what, CancellationToken cancellationToken)
        {
            var got = await ReadRawByteAsync(stream, settings.RawResponseTimeout, cancellationToken).ConfigureAwait(false);
            if (got != Acknowledge)
            {
                throw new SspDownloadException(
                    $"The device did not acknowledge {what}: it answered 0x{got:X2} rather than 0x{Acknowledge:X2}.");
            }
        }

        private static async Task<byte> ReadRawByteAsync(Stream stream, TimeSpan timeout, CancellationToken cancellationToken)
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(timeout);

            var buffer = new byte[1];
            try
            {
                var read = await stream.ReadAsync(buffer, 0, 1, cts.Token).ConfigureAwait(false);
                if (read == 0)
                {
                    throw new SspDownloadException("The connection closed while waiting for the device during a download.");
                }
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                throw new SspDownloadException($"The device did not answer within {timeout} during a download.");
            }

            return buffer[0];
        }

        private static async Task<byte[]> ExchangeAsync(
            SspStreamWriter writer, SspStreamReader reader, SequenceFlag sequence,
            SspDownloadOptions settings, byte[] data, CancellationToken cancellationToken)
        {
            var flag = sequence.Advance();
            var request = new SspRawPacket(0x00, data);

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(settings.RawResponseTimeout);

            try
            {
                await writer.WritePacketAsync(request, flag, cts.Token).ConfigureAwait(false);
                var bytes = await reader.ReadRawPacketAsync(cts.Token).ConfigureAwait(false);
                return SspRawPacket.Parse(bytes, flag).Data;
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                throw new SspDownloadException($"The device did not answer a download command within {settings.RawResponseTimeout}.");
            }
        }

        private static async Task WaitForRestartAsync(
            SspStreamWriter writer, SspStreamReader reader, SspDownloadOptions settings, CancellationToken cancellationToken)
        {
            var deadline = DateTime.UtcNow + settings.RestartTimeout;
            var sequence = new SequenceFlag();

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    var reply = await ExchangeAsync(writer, reader, sequence, settings, new byte[] { 0x11 }, cancellationToken)
                        .ConfigureAwait(false);
                    if (reply.Length >= 1 && reply[0] == (byte)SspResponse.OK) return;
                }
                catch (SspDownloadException)
                {
                    // The device is still restarting and not yet answering; keep trying until the deadline.
                }
                catch (SspCommunicationException)
                {
                    // Same: a malformed or missing reply from a device that is still coming back.
                }

                if (DateTime.UtcNow >= deadline)
                {
                    throw new SspDownloadException(
                        $"The device did not come back within {settings.RestartTimeout} after the download. " +
                        "The transfer completed, but the device has not confirmed it restarted.");
                }

                await Task.Delay(TimeSpan.FromMilliseconds(200), cancellationToken).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// The sequence flag for the framed phase.  A class rather than a local because the method
        /// that advances it is async, and an async method cannot take a value by reference.
        /// </summary>
        private sealed class SequenceFlag
        {
            private bool flag;

            /// <summary>Toggles the flag and returns the new value, as each new command does.</summary>
            public bool Advance() => flag = !flag;
        }

        private static void EnsureOk(byte[] reply, string message)
        {
            if (reply.Length < 1 || reply[0] != (byte)SspResponse.OK)
            {
                var code = reply.Length >= 1 ? $"0x{reply[0]:X2}" : "an empty reply";
                throw new SspDownloadException($"{message}  The device answered {code}.");
            }
        }
    }
}

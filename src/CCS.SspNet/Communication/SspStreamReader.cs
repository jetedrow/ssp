using CCS.SspNet.Exceptions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace CCS.SspNet.Communication
{
    /// <summary>
    /// Reads SSP packets from a stream, removing byte stuffing as it goes.
    /// </summary>
    /// <remarks>
    /// Packets returned by this type are logical packets: a single leading
    /// <see cref="Constants.STX"/> followed by unstuffed bytes.  Nothing above this layer sees
    /// stuffing.  The reader holds no state between calls, so one instance can read any number of
    /// consecutive packets from the same stream.
    /// </remarks>
    internal sealed class SspStreamReader : IDisposable
    {
        private readonly byte[] singleByteBuffer = new byte[1];
        private bool disposed;

        public SspStreamReader(Stream stream)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            if (!stream.CanRead) throw new ArgumentException("Stream does not support reading.", nameof(stream));

            BaseStream = stream;
        }

        /// <summary>Gets the stream being read.</summary>
        public Stream BaseStream { get; }

        /// <summary>Closes the underlying stream.</summary>
        public void Close() => BaseStream.Close();

        /// <summary>
        /// Reads the next SSP packet from the stream.
        /// </summary>
        /// <param name="cancellationToken">Cancels the read.</param>
        /// <returns>
        /// The packet's logical bytes: STX, the combined sequence/address byte, the length, the
        /// data, and the two CRC bytes, with any byte stuffing removed.
        /// </returns>
        /// <exception cref="PacketFormatException">
        /// A <see cref="Constants.STX"/> inside the packet was not byte stuffed, which means the
        /// stream is out of step with the sender.
        /// </exception>
        /// <exception cref="SspConnectionClosedException">The stream ended mid-packet.</exception>
        public async Task<byte[]> ReadRawPacketAsync(CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            // Anything before the start marker is noise from a partial or foreign packet; skip it.
            // A lone STX can only be a start marker, because every STX within a packet is doubled.
            byte current;
            do
            {
                current = await ReadByteAsync(cancellationToken).ConfigureAwait(false);
            }
            while (current != Constants.STX);

            var packet = new List<byte>(Constants.MaxPacketLength) { Constants.STX };

            // The sequence/address byte, then the length, which tells us how much is left to read.
            packet.Add(await ReadUnstuffedByteAsync(cancellationToken).ConfigureAwait(false));

            var dataLength = await ReadUnstuffedByteAsync(cancellationToken).ConfigureAwait(false);
            packet.Add(dataLength);

            // The data, then the two CRC bytes.
            var remaining = dataLength + 2;
            for (var i = 0; i < remaining; i++)
            {
                packet.Add(await ReadUnstuffedByteAsync(cancellationToken).ConfigureAwait(false));
            }

            return packet.ToArray();
        }

        /// <summary>
        /// Reads one logical byte, collapsing a stuffed <see cref="Constants.STX"/> pair back into a
        /// single byte.
        /// </summary>
        private async Task<byte> ReadUnstuffedByteAsync(CancellationToken cancellationToken)
        {
            var b = await ReadByteAsync(cancellationToken).ConfigureAwait(false);
            if (b != Constants.STX) return b;

            // Inside a packet an STX must always be doubled. A lone one means the sender and the
            // reader disagree about where this packet starts.
            var second = await ReadByteAsync(cancellationToken).ConfigureAwait(false);
            if (second != Constants.STX)
            {
                throw new PacketFormatException("Non-byte-stuffed STX byte encountered within packet.");
            }

            return Constants.STX;
        }

        private async Task<byte> ReadByteAsync(CancellationToken cancellationToken)
        {
            var read = await BaseStream.ReadAsync(singleByteBuffer, 0, 1, cancellationToken).ConfigureAwait(false);
            if (read == 0) throw new SspConnectionClosedException("The stream ended while reading an SSP packet.");

            return singleByteBuffer[0];
        }

        private void ThrowIfDisposed()
        {
            if (disposed) throw new ObjectDisposedException(nameof(SspStreamReader));
        }

        /// <summary>
        /// Releases the reader.  The underlying stream is not owned by the reader and is left open.
        /// </summary>
        public void Dispose()
        {
            disposed = true;
        }
    }
}

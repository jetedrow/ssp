using CCS.SspNet.Interfaces;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace CCS.SspNet.Communication
{
    /// <summary>
    /// Writes SSP packets to a stream, applying byte stuffing as it goes.
    /// </summary>
    /// <remarks>
    /// Callers hand this type logical packets and never deal with stuffing themselves; see
    /// <see cref="SspByteStuffing"/>.
    /// </remarks>
    internal sealed class SspStreamWriter : IDisposable
    {
        private bool disposed;

        public SspStreamWriter(Stream stream)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            if (!stream.CanWrite) throw new ArgumentException("Stream does not support writing.", nameof(stream));

            BaseStream = stream;
        }

        /// <summary>Gets the stream being written to.</summary>
        public Stream BaseStream { get; }

        /// <summary>Closes the underlying stream.</summary>
        public void Close() => BaseStream.Close();

        /// <summary>
        /// Writes a packet, stuffing it on the way out, and flushes the stream.
        /// </summary>
        /// <param name="packet">The packet to send.</param>
        /// <param name="sequenceFlag">The sequence flag to send it with.</param>
        /// <param name="cancellationToken">Cancels the write.</param>
        public async Task WritePacketAsync(ISspRawPacket packet, bool sequenceFlag, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            if (packet == null) throw new ArgumentNullException(nameof(packet));

            var logical = new List<byte>(Constants.MaxPacketLength);
            foreach (var b in packet.GetPacketBytes(sequenceFlag)) logical.Add(b);

            await WriteRawPacketAsync(logical.ToArray(), cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Writes an already-assembled logical packet, stuffing it on the way out.
        /// </summary>
        /// <param name="logicalPacket">
        /// A complete, unstuffed packet beginning with a single <see cref="Constants.STX"/>.
        /// </param>
        /// <param name="cancellationToken">Cancels the write.</param>
        public async Task WriteRawPacketAsync(byte[] logicalPacket, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            if (logicalPacket == null) throw new ArgumentNullException(nameof(logicalPacket));

            var wire = SspByteStuffing.Stuff(logicalPacket);

            await BaseStream.WriteAsync(wire, 0, wire.Length, cancellationToken).ConfigureAwait(false);
            await BaseStream.FlushAsync(cancellationToken).ConfigureAwait(false);
        }

        private void ThrowIfDisposed()
        {
            if (disposed) throw new ObjectDisposedException(nameof(SspStreamWriter));
        }

        /// <summary>
        /// Releases the writer.  The underlying stream is not owned by the writer and is left open.
        /// </summary>
        public void Dispose()
        {
            disposed = true;
        }
    }
}

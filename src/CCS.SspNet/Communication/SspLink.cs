using CCS.SspNet.Exceptions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace CCS.SspNet.Communication
{
    /// <summary>
    /// A request/response link to one or more SSP devices over a single stream.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The link owns two things the layers above must not have to think about.
    /// </para>
    /// <para>
    /// The first is the sequence flag.  SSP's retransmission scheme depends on the host advancing
    /// the flag for each new command and <em>repeating</em> it when resending one, so a device can
    /// tell a retransmission from a fresh command and answer from its cache rather than acting
    /// twice.  That state is per device address and lives here.
    /// </para>
    /// <para>
    /// The second is serialization.  An SSP bus carries one exchange at a time, so every call is
    /// queued behind the one in progress.  That is what will later let a command issued from inside
    /// an event handler interleave safely with a running poll loop rather than racing it.
    /// </para>
    /// </remarks>
    internal sealed class SspLink : IDisposable
    {
        private readonly SspStreamReader reader;
        private readonly SspStreamWriter writer;
        private readonly SspLinkOptions options;
        private readonly SemaphoreSlim gate = new SemaphoreSlim(1, 1);
        private readonly Dictionary<byte, bool> sequenceFlags = new Dictionary<byte, bool>();
        private bool disposed;

        public SspLink(Stream stream, SspLinkOptions? options = null)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));

            this.options = options ?? new SspLinkOptions();
            reader = new SspStreamReader(stream);
            writer = new SspStreamWriter(stream);
        }

        /// <summary>
        /// Sends a command to a device and returns its reply.
        /// </summary>
        /// <param name="address">The device's address on the bus.</param>
        /// <param name="data">The command and its parameters.</param>
        /// <param name="cancellationToken">Cancels the exchange.</param>
        /// <exception cref="SspCommunicationException">
        /// Every attempt failed.  The inner exception carries the last one.
        /// </exception>
        public async Task<SspRawPacket> ExchangeAsync(byte address, byte[] data, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            if (data == null) throw new ArgumentNullException(nameof(data));

            await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                // Advanced once, for the exchange as a whole. Every retry below reuses it.
                var sequenceFlag = AdvanceSequenceFlag(address);
                var request = new SspRawPacket(address, data);

                Exception? lastFailure = null;

                for (var attempt = 0; attempt <= options.MaxRetries; attempt++)
                {
                    try
                    {
                        return await AttemptAsync(request, address, sequenceFlag, cancellationToken).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                    {
                        // The device did not answer in time. Resend with the same flag.
                        lastFailure = new TimeoutException($"No reply within {options.ResponseTimeout}.");
                    }
                    catch (PacketCrcException ex)
                    {
                        // A corrupted reply is indistinguishable from a lost one; resend.
                        lastFailure = ex;
                    }
                    catch (PacketFormatException ex)
                    {
                        lastFailure = ex;
                    }
                    catch (PacketLengthException ex)
                    {
                        lastFailure = ex;
                    }
                }

                var attempts = options.MaxRetries + 1;
                throw new SspCommunicationException(
                    $"No usable reply from the device at address {address} after {attempts} attempt{(attempts == 1 ? string.Empty : "s")}.",
                    lastFailure!);
            }
            finally
            {
                gate.Release();
            }
        }

        private async Task<SspRawPacket> AttemptAsync(SspRawPacket request, byte address, bool sequenceFlag, CancellationToken cancellationToken)
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(options.ResponseTimeout);

            await writer.WritePacketAsync(request, sequenceFlag, timeout.Token).ConfigureAwait(false);

            var bytes = await reader.ReadRawPacketAsync(timeout.Token).ConfigureAwait(false);

            // A reply carries back the flag it was sent with, so a mismatch means this is the
            // device answering an earlier command and the real reply is still to come.
            var response = SspRawPacket.Parse(bytes, sequenceFlag);

            if (response.Address != address)
            {
                throw new PacketFormatException(
                    $"Expected a reply from the device at address {address}, but the packet came from {response.Address}.");
            }

            return response;
        }

        /// <summary>
        /// Resets the sequence flag for a device, as a SYNC command does at the device end.
        /// </summary>
        /// <remarks>
        /// Call this whenever SYNC is sent, so that both ends agree on the next flag.
        /// </remarks>
        public void ResetSequence(byte address)
        {
            ThrowIfDisposed();
            sequenceFlags[address] = false;
        }

        private bool AdvanceSequenceFlag(byte address)
        {
            var next = !(sequenceFlags.TryGetValue(address, out var current) && current);
            sequenceFlags[address] = next;
            return next;
        }

        private void ThrowIfDisposed()
        {
            if (disposed) throw new ObjectDisposedException(nameof(SspLink));
        }

        /// <summary>
        /// Releases the link.  The stream it was given is not owned by the link and is left open.
        /// </summary>
        public void Dispose()
        {
            if (disposed) return;
            disposed = true;

            reader.Dispose();
            writer.Dispose();
            gate.Dispose();
        }
    }
}

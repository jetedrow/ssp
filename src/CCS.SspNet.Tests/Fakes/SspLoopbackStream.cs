using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace CCS.SspNet.Tests.Fakes
{
    /// <summary>
    /// A pair of in-memory streams wired back to back: what one writes, the other reads.
    /// </summary>
    /// <remarks>
    /// This stands in for a serial cable in tests.  Unlike a <see cref="MemoryStream"/> a read
    /// waits for the far end to write rather than reporting end of stream, so the ordinary
    /// request/response rhythm works without hardware, named pipes or sleeps.
    /// </remarks>
    public sealed class SspLoopbackStream : Stream
    {
        private readonly ByteChannel inbound;
        private readonly ByteChannel outbound;

        private SspLoopbackStream(ByteChannel inbound, ByteChannel outbound)
        {
            this.inbound = inbound;
            this.outbound = outbound;
        }

        /// <summary>
        /// Creates a connected pair.  Give <c>Device</c> to the simulator and <c>Host</c> to the
        /// code under test.
        /// </summary>
        public static (SspLoopbackStream Host, SspLoopbackStream Device) CreatePair()
        {
            var hostToDevice = new ByteChannel();
            var deviceToHost = new ByteChannel();

            return (
                new SspLoopbackStream(deviceToHost, hostToDevice),
                new SspLoopbackStream(hostToDevice, deviceToHost));
        }

        /// <summary>Gets the number of bytes waiting to be read.</summary>
        public int BytesAvailable => inbound.Count;

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => true;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush() { }

        public override Task FlushAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public override int Read(byte[] buffer, int offset, int count) =>
            ReadAsync(buffer, offset, count, CancellationToken.None).GetAwaiter().GetResult();

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            inbound.ReadAsync(buffer, offset, count, cancellationToken);

        public override void Write(byte[] buffer, int offset, int count) => outbound.Write(buffer, offset, count);

        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            outbound.Write(buffer, offset, count);
            return Task.CompletedTask;
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                // Closing this end means the far end's reads should report end of stream.
                outbound.Complete();
            }

            base.Dispose(disposing);
        }

        /// <summary>
        /// A one-way byte queue whose reads wait for data instead of reporting end of stream.
        /// </summary>
        private sealed class ByteChannel
        {
            private readonly Queue<byte> queue = new Queue<byte>();
            private readonly SemaphoreSlim dataArrived = new SemaphoreSlim(0);
            private readonly object sync = new object();
            private bool completed;

            public int Count
            {
                get { lock (sync) { return queue.Count; } }
            }

            public void Write(byte[] buffer, int offset, int count)
            {
                lock (sync)
                {
                    for (var i = 0; i < count; i++) queue.Enqueue(buffer[offset + i]);
                }

                dataArrived.Release();
            }

            public void Complete()
            {
                lock (sync)
                {
                    if (completed) return;
                    completed = true;
                }

                // Wake anyone waiting so they can observe the end of the stream.
                dataArrived.Release();
            }

            public async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            {
                if (count == 0) return 0;

                while (true)
                {
                    lock (sync)
                    {
                        if (queue.Count > 0)
                        {
                            var taken = Math.Min(count, queue.Count);
                            for (var i = 0; i < taken; i++) buffer[offset + i] = queue.Dequeue();
                            return taken;
                        }

                        // Only report end of stream once the queue is drained, so bytes written
                        // just before a close are still delivered.
                        if (completed) return 0;
                    }

                    await dataArrived.WaitAsync(cancellationToken).ConfigureAwait(false);
                }
            }
        }
    }
}

using System;
using System.Collections.Generic;

namespace CCS.SspNet.Protocol
{
    /// <summary>
    /// Reads the event list out of a poll reply.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A poll reply's data is a run of events laid end to end: a code, then that event's data
    /// bytes, then the next code.  Nothing on the wire says how long a payload is, so the decoder
    /// has to know in advance, which is what <see cref="SspEventTable"/> and the device's protocol
    /// version are for.
    /// </para>
    /// <para>
    /// Decoding the same bytes at two different versions can give two different answers, and one of
    /// them will be wrong.  That is not a flaw in the decoder — it is why SSP asks a host to fix the
    /// version at startup and never let a device run ahead of it.
    /// </para>
    /// </remarks>
    public static class SspPollDecoder
    {
        /// <summary>
        /// Reads a poll reply's event list.
        /// </summary>
        /// <param name="eventData">
        /// The reply's data with the leading response code already removed —
        /// <see cref="SspReply.Data"/> gives exactly this.
        /// </param>
        /// <param name="version">The protocol version the device is set to.</param>
        /// <param name="table">
        /// The payload lengths to read with, or <see langword="null"/> for
        /// <see cref="SspEventTable.Default"/>.
        /// </param>
        /// <returns>
        /// The events read.  Check <see cref="SspPollResult.IsComplete"/>: a reply containing an
        /// event the table has no length for is reported rather than guessed at.
        /// </returns>
        public static SspPollResult Decode(
            ReadOnlySpan<byte> eventData,
            SspProtocolVersion version,
            SspEventTable? table = null)
        {
            table ??= SspEventTable.Default;

            var events = new List<SspPollEvent>();
            var offset = 0;

            while (offset < eventData.Length)
            {
                var code = eventData[offset];
                var payloadStart = offset + 1;
                var rest = eventData.Slice(payloadStart);

                if (!table.TryGetPayload(code, version, out var payload))
                {
                    return Stopped(events, eventData, offset, code,
                        $"the event table has no payload length for 0x{code:X2} at protocol version {version}");
                }

                if (!payload.TryMeasure(rest, out var length))
                {
                    return Stopped(events, eventData, offset, code,
                        $"0x{code:X2} needs more data than the reply has left ({rest.Length} byte(s))");
                }

                events.Add(new SspPollEvent(code, rest.Slice(0, length).ToArray()));
                offset = payloadStart + length;
            }

            return new SspPollResult(events, stoppedAt: null, undecoded: ReadOnlyMemory<byte>.Empty, reason: null);
        }

        private static SspPollResult Stopped(
            List<SspPollEvent> events, ReadOnlySpan<byte> eventData, int offset, byte code, string reason) =>
            new SspPollResult(events, code, eventData.Slice(offset).ToArray(), reason);
    }
}

using System;
using System.Collections.Generic;

namespace CCS.SspNet.Protocol
{
    /// <summary>
    /// What a poll reply turned out to contain.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A decode can end part-way.  Payload lengths are not on the wire — the host is expected to
    /// know them — so the first event the table has no length for takes the rest of the reply with
    /// it: there is no way to tell where that event's payload stops and the next event's code
    /// starts.
    /// </para>
    /// <para>
    /// The events read before that point are still good, and are still here.  Throwing them away
    /// would lose credits that really happened, which is why a stopped decode is a result rather
    /// than an exception.
    /// </para>
    /// </remarks>
    public sealed class SspPollResult
    {
        internal SspPollResult(IReadOnlyList<SspPollEvent> events, byte? stoppedAt, ReadOnlyMemory<byte> undecoded, string? reason)
        {
            Events = events;
            StoppedAtCode = stoppedAt;
            UndecodedData = undecoded;
            StopReason = reason;
        }

        /// <summary>Gets the events read, in the order the device sent them.</summary>
        public IReadOnlyList<SspPollEvent> Events { get; }

        /// <summary>
        /// Gets a value indicating whether the whole reply was read.
        /// </summary>
        public bool IsComplete => StoppedAtCode is null;

        /// <summary>
        /// Gets the event code the decode stopped at, or <see langword="null"/> if it did not stop.
        /// </summary>
        public byte? StoppedAtCode { get; }

        /// <summary>
        /// Gets why the decode stopped, or <see langword="null"/> if it did not stop.
        /// </summary>
        public string? StopReason { get; }

        /// <summary>
        /// Gets the bytes from the code it stopped at to the end of the reply, for a caller that
        /// wants to log or decode them itself.  Empty when the decode ran to the end.
        /// </summary>
        public ReadOnlyMemory<byte> UndecodedData { get; }

        /// <inheritdoc />
        public override string ToString() =>
            IsComplete
                ? $"{Events.Count} event(s)"
                : $"{Events.Count} event(s), then stopped at 0x{StoppedAtCode:X2}: {StopReason}";
    }
}

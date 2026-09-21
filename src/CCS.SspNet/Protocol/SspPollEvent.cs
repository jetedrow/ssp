using System;

namespace CCS.SspNet.Protocol
{
    /// <summary>
    /// One event read out of a poll reply, with the data bytes that belonged to it.
    /// </summary>
    public readonly struct SspPollEvent : IEquatable<SspPollEvent>
    {
        /// <summary>Initializes a new instance.</summary>
        /// <param name="code">The event code as it appeared in the reply.</param>
        /// <param name="data">The bytes that followed it.</param>
        public SspPollEvent(byte code, ReadOnlyMemory<byte> data)
        {
            Code = code;
            Data = data;
        }

        /// <summary>Gets the event code exactly as the device sent it.</summary>
        public byte Code { get; }

        /// <summary>
        /// Gets the event as a named value.
        /// </summary>
        /// <remarks>
        /// A code the library has no name for still casts, and still compares equal to itself, so
        /// a caller switching on this should have a default arm.  <see cref="IsKnown"/> says
        /// whether the name means anything.
        /// </remarks>
        public SspEvent Event => (SspEvent)Code;

        /// <summary>Gets a value indicating whether this library has a name for the code.</summary>
        public bool IsKnown => Enum.IsDefined(typeof(SspEvent), Code);

        /// <summary>Gets the data bytes that followed the code, which may be empty.</summary>
        public ReadOnlyMemory<byte> Data { get; }

        /// <inheritdoc />
        public bool Equals(SspPollEvent other) => Code == other.Code && Data.Span.SequenceEqual(other.Data.Span);

        /// <inheritdoc />
        public override bool Equals(object? obj) => obj is SspPollEvent other && Equals(other);

        /// <inheritdoc />
        public override int GetHashCode() => (Code * 397) ^ Data.Length;

        /// <inheritdoc />
        public override string ToString() =>
            Data.Length == 0
                ? $"{(IsKnown ? Event.ToString() : "Unnamed")} (0x{Code:X2})"
                : $"{(IsKnown ? Event.ToString() : "Unnamed")} (0x{Code:X2}), {Data.Length} data byte(s)";

        /// <summary>Compares two events for equality.</summary>
        public static bool operator ==(SspPollEvent left, SspPollEvent right) => left.Equals(right);

        /// <summary>Compares two events for inequality.</summary>
        public static bool operator !=(SspPollEvent left, SspPollEvent right) => !left.Equals(right);
    }
}

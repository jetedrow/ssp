using System;

namespace CCS.SspNet.Protocol
{
    /// <summary>
    /// How many data bytes follow an event code in a poll reply.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Most events carry a fixed number of bytes.  The payout and float events on multi-currency
    /// devices instead carry a count byte followed by that many equal-sized blocks, one per
    /// currency in the device's dataset, and one of them adds a trailing byte after the last block.
    /// </para>
    /// <para>
    /// A handful of events are listed in the protocol manual with no size at all.  Those are
    /// <see cref="Unknown"/>: a decode stops when it reaches one rather than guessing a length and
    /// reading the rest of the reply out of step.  Registering a size for one is what
    /// <see cref="SspEventTable.WithEvent"/> is for.
    /// </para>
    /// </remarks>
    public readonly struct SspEventPayload : IEquatable<SspEventPayload>
    {
        private SspEventPayload(SspEventPayloadKind kind, int size, int blockSize, int trailerSize)
        {
            Kind = kind;
            Size = size;
            BlockSize = blockSize;
            TrailerSize = trailerSize;
        }

        /// <summary>Gets how the payload's length is arrived at.</summary>
        public SspEventPayloadKind Kind { get; }

        /// <summary>
        /// Gets the payload length, for <see cref="SspEventPayloadKind.Fixed"/>.  Zero otherwise.
        /// </summary>
        public int Size { get; }

        /// <summary>
        /// Gets the size of one repeated block, for <see cref="SspEventPayloadKind.CountPrefixed"/>.
        /// Zero otherwise.
        /// </summary>
        public int BlockSize { get; }

        /// <summary>
        /// Gets the number of bytes after the last block, for
        /// <see cref="SspEventPayloadKind.CountPrefixed"/>.  Zero otherwise.
        /// </summary>
        public int TrailerSize { get; }

        /// <summary>An event carrying no data at all.</summary>
        public static SspEventPayload None { get; } = new SspEventPayload(SspEventPayloadKind.Fixed, 0, 0, 0);

        /// <summary>An event whose payload length the protocol manual does not give.</summary>
        public static SspEventPayload Unknown { get; } = new SspEventPayload(SspEventPayloadKind.Unknown, 0, 0, 0);

        /// <summary>An event carrying a fixed number of data bytes.</summary>
        /// <param name="size">The payload length, from zero to <see cref="Constants.MaxDataLength"/>.</param>
        public static SspEventPayload Fixed(int size)
        {
            if (size < 0 || size > Constants.MaxDataLength)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(size), size, $"An event payload is between 0 and {Constants.MaxDataLength} bytes.");
            }

            return new SspEventPayload(SspEventPayloadKind.Fixed, size, 0, 0);
        }

        /// <summary>
        /// An event carrying a count byte, then that many equal-sized blocks, then an optional
        /// fixed trailer.
        /// </summary>
        /// <param name="blockSize">The size of one repeated block.</param>
        /// <param name="trailerSize">The number of bytes following the last block.</param>
        public static SspEventPayload CountPrefixed(int blockSize, int trailerSize = 0)
        {
            if (blockSize <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(blockSize), blockSize, "A repeated block has at least one byte.");
            }

            if (trailerSize < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(trailerSize), trailerSize, "A trailer cannot be a negative length.");
            }

            return new SspEventPayload(SspEventPayloadKind.CountPrefixed, 0, blockSize, trailerSize);
        }

        /// <summary>
        /// Works out how many bytes of <paramref name="data"/> this event's payload occupies.
        /// </summary>
        /// <param name="data">The reply from the byte immediately after the event code onwards.</param>
        /// <param name="length">The payload length, when this returns <see langword="true"/>.</param>
        /// <returns>
        /// <see langword="false"/> if the length cannot be established — either the payload is
        /// <see cref="Unknown"/>, or the reply ends before the payload does.
        /// </returns>
        public bool TryMeasure(ReadOnlySpan<byte> data, out int length)
        {
            length = 0;

            switch (Kind)
            {
                case SspEventPayloadKind.Fixed:
                    if (data.Length < Size) return false;
                    length = Size;
                    return true;

                case SspEventPayloadKind.CountPrefixed:
                    // The count byte itself is part of the payload.
                    if (data.Length < 1) return false;
                    var measured = 1 + (data[0] * BlockSize) + TrailerSize;
                    if (data.Length < measured) return false;
                    length = measured;
                    return true;

                default:
                    return false;
            }
        }

        /// <inheritdoc />
        public bool Equals(SspEventPayload other) =>
            Kind == other.Kind && Size == other.Size && BlockSize == other.BlockSize && TrailerSize == other.TrailerSize;

        /// <inheritdoc />
        public override bool Equals(object? obj) => obj is SspEventPayload other && Equals(other);

        /// <inheritdoc />
        public override int GetHashCode() =>
            (((((int)Kind * 397) ^ Size) * 397) ^ BlockSize) * 397 ^ TrailerSize;

        /// <inheritdoc />
        public override string ToString() => Kind switch
        {
            SspEventPayloadKind.Fixed => $"{Size} byte(s)",
            SspEventPayloadKind.CountPrefixed => TrailerSize == 0
                ? $"count + n x {BlockSize} byte(s)"
                : $"count + n x {BlockSize} byte(s) + {TrailerSize} byte(s)",
            _ => "unknown length",
        };

        /// <summary>Compares two payload shapes for equality.</summary>
        public static bool operator ==(SspEventPayload left, SspEventPayload right) => left.Equals(right);

        /// <summary>Compares two payload shapes for inequality.</summary>
        public static bool operator !=(SspEventPayload left, SspEventPayload right) => !left.Equals(right);
    }

    /// <summary>How an event payload's length is arrived at.</summary>
    public enum SspEventPayloadKind
    {
        /// <summary>The protocol manual gives no length for this event.</summary>
        Unknown = 0,

        /// <summary>A fixed number of bytes.</summary>
        Fixed,

        /// <summary>A count byte, then that many equal-sized blocks, then an optional trailer.</summary>
        CountPrefixed,
    }
}

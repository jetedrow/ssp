using System;

namespace CCS.SspNet.Protocol
{
    /// <summary>
    /// The SSP protocol version a device has been set to.
    /// </summary>
    /// <remarks>
    /// <para>
    /// SSP uses the protocol version to gate <em>events</em>, not commands.  A poll reply is a run
    /// of event codes each followed by a payload whose length the host is expected to know in
    /// advance; a device that emitted an event the host had never heard of would leave the host
    /// unable to tell where that payload ended and the next event began, and the rest of the reply
    /// would be read as garbage.  Raising the version is therefore a host saying "I know the events
    /// you are about to send me".
    /// </para>
    /// <para>
    /// Which version a device is at is discovered with a setup request and changed with a host
    /// protocol version command.  A host must never set a device higher than the version it can
    /// itself decode.
    /// </para>
    /// <para>
    /// The same event can carry a different payload at different versions, so a version is required
    /// to decode a poll reply at all.  <see cref="SspEventTable"/> holds those differences.
    /// </para>
    /// </remarks>
    public readonly struct SspProtocolVersion : IEquatable<SspProtocolVersion>, IComparable<SspProtocolVersion>
    {
        /// <summary>
        /// The oldest version the event table describes.  Older devices exist; their events are a
        /// subset of this one's, so decoding them at <see cref="Lowest"/> is safe.
        /// </summary>
        public static readonly SspProtocolVersion Lowest = new SspProtocolVersion(4);

        /// <summary>
        /// The newest version the event table describes.  A device may report something higher, in
        /// which case events added after this version will not be known to the table until it is
        /// extended — see <see cref="SspEventTable.WithEvent"/>.
        /// </summary>
        public static readonly SspProtocolVersion Highest = new SspProtocolVersion(9);

        /// <summary>Initializes a new instance from a raw version number.</summary>
        /// <param name="value">The version, as the device reports it.  Must not be zero.</param>
        public SspProtocolVersion(byte value)
        {
            if (value == 0)
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "A protocol version starts at 1.");
            }

            Value = value;
        }

        /// <summary>Gets the version number as it appears on the wire.</summary>
        public byte Value { get; }

        /// <summary>
        /// Gets a value indicating whether this version is one the event table describes in full.
        /// </summary>
        /// <remarks>
        /// A version above <see cref="Highest"/> is not an error.  It means the device may send
        /// events this library has no size rule for, which a decode will report rather than guess
        /// at.
        /// </remarks>
        public bool IsKnown => Value >= Lowest.Value && Value <= Highest.Value;

        /// <inheritdoc />
        public bool Equals(SspProtocolVersion other) => Value == other.Value;

        /// <inheritdoc />
        public override bool Equals(object? obj) => obj is SspProtocolVersion other && Equals(other);

        /// <inheritdoc />
        public override int GetHashCode() => Value.GetHashCode();

        /// <inheritdoc />
        public int CompareTo(SspProtocolVersion other) => Value.CompareTo(other.Value);

        /// <inheritdoc />
        public override string ToString() => Value.ToString(System.Globalization.CultureInfo.InvariantCulture);

        /// <summary>Compares two versions for equality.</summary>
        public static bool operator ==(SspProtocolVersion left, SspProtocolVersion right) => left.Equals(right);

        /// <summary>Compares two versions for inequality.</summary>
        public static bool operator !=(SspProtocolVersion left, SspProtocolVersion right) => !left.Equals(right);

        /// <summary>Determines whether one version is lower than another.</summary>
        public static bool operator <(SspProtocolVersion left, SspProtocolVersion right) => left.Value < right.Value;

        /// <summary>Determines whether one version is higher than another.</summary>
        public static bool operator >(SspProtocolVersion left, SspProtocolVersion right) => left.Value > right.Value;

        /// <summary>Determines whether one version is at or below another.</summary>
        public static bool operator <=(SspProtocolVersion left, SspProtocolVersion right) => left.Value <= right.Value;

        /// <summary>Determines whether one version is at or above another.</summary>
        public static bool operator >=(SspProtocolVersion left, SspProtocolVersion right) => left.Value >= right.Value;

        /// <summary>Converts a raw version number into an <see cref="SspProtocolVersion"/>.</summary>
        public static implicit operator SspProtocolVersion(byte value) => new SspProtocolVersion(value);

        /// <summary>Gets the raw version number.</summary>
        public static explicit operator byte(SspProtocolVersion version) => version.Value;
    }
}

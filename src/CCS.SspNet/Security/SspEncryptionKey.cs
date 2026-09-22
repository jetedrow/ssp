using System;

namespace CCS.SspNet.Security
{
    /// <summary>
    /// The 128-bit key an encrypted session runs on: a fixed half and a negotiated half.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The lower 64 bits are set by whoever builds the machine and are the same for every session,
    /// which is how a manufacturer controls what may be plugged into their equipment.  The upper 64
    /// bits are agreed afresh by <see cref="SspKeyExchange"/> each time a host connects, so two
    /// sessions never share a key even on the same pair of devices.
    /// </para>
    /// <para>
    /// A device leaves the factory with <see cref="DefaultFixedHalf"/> and will only talk to a host
    /// that knows it.  Changing it is <see cref="SspCommand.SetFixedEncryptionKey"/>, which is
    /// itself only accepted encrypted — so the old key has to be known to set a new one.
    /// </para>
    /// </remarks>
    public readonly struct SspEncryptionKey : IEquatable<SspEncryptionKey>
    {
        /// <summary>
        /// The fixed half a device ships with: the bytes <c>01 23 45 67 01 23 45 67</c>.
        /// </summary>
        public const ulong DefaultFixedHalf = 0x6745230167452301UL;

        /// <summary>Creates a key from its two halves.</summary>
        public SspEncryptionKey(ulong fixedHalf, ulong negotiatedHalf)
        {
            FixedHalf = fixedHalf;
            NegotiatedHalf = negotiatedHalf;
        }

        /// <summary>Gets the manufacturer's half, which does not change between sessions.</summary>
        public ulong FixedHalf { get; }

        /// <summary>Gets the half agreed with the device for this session.</summary>
        public ulong NegotiatedHalf { get; }

        /// <summary>Gets a key with the factory-default fixed half and nothing negotiated yet.</summary>
        public static SspEncryptionKey Default => new SspEncryptionKey(DefaultFixedHalf, 0);

        /// <summary>Returns this key with a different negotiated half.</summary>
        public SspEncryptionKey WithNegotiatedHalf(ulong negotiatedHalf) =>
            new SspEncryptionKey(FixedHalf, negotiatedHalf);

        /// <summary>
        /// Gets the sixteen bytes AES is keyed with: the fixed half first, then the negotiated
        /// half, each little endian.
        /// </summary>
        public byte[] ToArray()
        {
            var key = new byte[16];
            WriteLittleEndian(key, 0, FixedHalf);
            WriteLittleEndian(key, 8, NegotiatedHalf);
            return key;
        }

        private static void WriteLittleEndian(byte[] destination, int offset, ulong value)
        {
            for (var i = 0; i < 8; i++)
            {
                destination[offset + i] = (byte)(value >> (i * 8));
            }
        }

        /// <inheritdoc/>
        public bool Equals(SspEncryptionKey other) =>
            FixedHalf == other.FixedHalf && NegotiatedHalf == other.NegotiatedHalf;

        /// <inheritdoc/>
        public override bool Equals(object? obj) => obj is SspEncryptionKey other && Equals(other);

        /// <inheritdoc/>
        public override int GetHashCode() => unchecked((FixedHalf.GetHashCode() * 397) ^ NegotiatedHalf.GetHashCode());

        /// <summary>Compares two keys.</summary>
        public static bool operator ==(SspEncryptionKey left, SspEncryptionKey right) => left.Equals(right);

        /// <summary>Compares two keys.</summary>
        public static bool operator !=(SspEncryptionKey left, SspEncryptionKey right) => !left.Equals(right);

        /// <summary>
        /// Returns a description of the key that does not disclose it.
        /// </summary>
        public override string ToString() => "SspEncryptionKey(fixed=..., negotiated=...)";
    }
}

namespace CCS.SspNet.Security
{
    /// <summary>
    /// How the four-byte packet counter of an encrypted block is laid out on the wire.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This exists because the protocol documents do not settle it.  Every other multi-byte
    /// integer SSP sends is little endian, including the three numbers of the key exchange that
    /// immediately precedes the first encrypted packet, so that is the default — but the one
    /// worked example of an encrypted packet in the implementation guide prints its counter as
    /// <c>00 00 00 A7</c>, which is only a plausible counter value read the other way round, and
    /// the example cannot be decrypted to check because its key is not published.
    /// </para>
    /// <para>
    /// Getting it wrong is loud rather than subtle: the device decrypts the packet, finds a
    /// counter that does not match its own, and discards it, so every encrypted command times out
    /// from the very first one.  If that is what a device does, set
    /// <see cref="SspEncryptionOptions.CountByteOrder"/> to <see cref="BigEndian"/>.
    /// </para>
    /// </remarks>
    public enum SspCountByteOrder
    {
        /// <summary>Least significant byte first, as the rest of SSP sends integers.</summary>
        LittleEndian = 0,

        /// <summary>Most significant byte first.</summary>
        BigEndian = 1,
    }
}

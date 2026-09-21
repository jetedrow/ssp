using System;
using System.Collections.Generic;

namespace CCS.SspNet.Communication
{
    /// <summary>
    /// Byte stuffing, the escaping scheme SSP uses so that the start-of-packet marker can never
    /// appear inside a packet.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A packet begins with a single <see cref="Constants.STX"/>.  Everywhere after that, a
    /// <see cref="Constants.STX"/> in the packet — in the address byte, the length, the data or the
    /// CRC — is transmitted twice.  A receiver hunting for the start of a packet can therefore treat
    /// any lone <see cref="Constants.STX"/> as a start marker.
    /// </para>
    /// <para>
    /// This is the only place in the library that knows about stuffing, and it is applied on the way
    /// out and removed on the way in by <see cref="SspStreamWriter"/> and
    /// <see cref="SspStreamReader"/>.  Every layer above those two deals exclusively in unstuffed,
    /// logical packets.
    /// </para>
    /// </remarks>
    internal static class SspByteStuffing
    {
        /// <summary>
        /// Converts a logical packet into the bytes to put on the wire, doubling every
        /// <see cref="Constants.STX"/> after the leading start marker.
        /// </summary>
        /// <param name="logicalPacket">
        /// A complete packet, starting with a single <see cref="Constants.STX"/> and containing no
        /// stuffing.
        /// </param>
        /// <returns>The bytes to transmit.</returns>
        internal static byte[] Stuff(byte[] logicalPacket)
        {
            if (logicalPacket == null) throw new ArgumentNullException(nameof(logicalPacket));
            if (logicalPacket.Length == 0) throw new ArgumentException("A packet cannot be empty.", nameof(logicalPacket));

            // The leading STX is never doubled; every one after it is.
            var stuffed = new List<byte>(logicalPacket.Length + 4) { logicalPacket[0] };

            for (var i = 1; i < logicalPacket.Length; i++)
            {
                var b = logicalPacket[i];
                stuffed.Add(b);
                if (b == Constants.STX) stuffed.Add(b);
            }

            return stuffed.ToArray();
        }
    }
}

using System;
using System.Collections.Generic;
using System.Text;

namespace CCS.SspNet
{
    /// <summary>
    /// Fixed values from the SSP wire format.
    /// </summary>
    public static class Constants
    {
        /// <summary>
        /// The byte that starts every packet.  Anywhere else in a packet it is sent twice, so that a
        /// receiver hunting for the start of a packet can never mistake data for a start marker.
        /// </summary>
        public const byte STX = 0x7F;

        /// <summary>The largest data payload a single packet can carry.</summary>
        public const int MaxDataLength = 255;

        /// <summary>
        /// The shortest a packet can be: STX, SEQ/ADDR, LEN, and two CRC bytes, carrying no data.
        /// </summary>
        public const int MinPacketLength = 5;

        /// <summary>
        /// The longest a packet can be: <see cref="MinPacketLength"/> plus a full
        /// <see cref="MaxDataLength"/> payload.
        /// </summary>
        public const int MaxPacketLength = MinPacketLength + MaxDataLength;

        /// <summary>The mask selecting the address out of the combined sequence/address byte.</summary>
        public const byte AddressMask = 0x7F;

        /// <summary>The bit carrying the sequence flag in the combined sequence/address byte.</summary>
        public const byte SequenceFlagMask = 0x80;
    }
}

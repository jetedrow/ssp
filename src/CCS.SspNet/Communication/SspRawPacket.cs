using CCS.SspNet.Exceptions;
using CCS.SspNet.Interfaces;
using System;
using System.Collections.Generic;
using System.Text;
using static CCS.SspNet.Utilities.CrcUtilities;

namespace CCS.SspNet.Communication
{
    /// <summary>
    /// A single SSP packet: an address, a payload, and the framing around them.
    /// </summary>
    /// <remarks>
    /// This type deals only in logical packets.  Byte stuffing is applied and removed at the stream
    /// layer by <see cref="SspStreamWriter"/> and <see cref="SspStreamReader"/>, so the bytes handed
    /// to <see cref="Parse"/> and returned by <see cref="GetPacketBytes"/> never contain it.
    /// </remarks>
    internal class SspRawPacket : ISspRawPacket
    {
        internal SspRawPacket() { }

        internal SspRawPacket(byte address, byte[] data)
        {
            Address = address;
            Data = data ?? throw new ArgumentNullException(nameof(data));
        }

        public byte Address { get; set; }

        public byte[] Data { get; set; } = Array.Empty<byte>();

        /// <summary>
        /// Parses a logical packet's bytes.
        /// </summary>
        /// <param name="packet">
        /// The packet bytes, with byte stuffing already removed — which is what
        /// <see cref="SspStreamReader.ReadRawPacketAsync"/> returns.
        /// </param>
        /// <param name="sequenceFlag">
        /// When given, requires the packet's sequence flag to be set (<see langword="true"/>) or
        /// clear (<see langword="false"/>).  When <see langword="null"/> the flag is not checked.
        /// </param>
        public static SspRawPacket Parse(byte[] packet, bool? sequenceFlag = null)
        {
            if (packet == null) throw new ArgumentNullException(nameof(packet));

            if (packet.Length < Constants.MinPacketLength)
            {
                throw new PacketLengthException(
                    $"A packet is at least {Constants.MinPacketLength} bytes (STX + SEQ/ADDR + LEN + CRC LSB + CRC MSB), but this one is {packet.Length}.");
            }

            if (packet.Length > Constants.MaxPacketLength)
            {
                throw new PacketLengthException(
                    $"A packet is at most {Constants.MaxPacketLength} bytes ({Constants.MinPacketLength} of framing plus up to {Constants.MaxDataLength} data bytes), but this one is {packet.Length}.");
            }

            if (packet[0] != Constants.STX)
            {
                throw new PacketFormatException("Packet does not begin with STX (0x7F) character.");
            }

            // The length byte counts the data only; the packet also carries 3 framing bytes and 2
            // CRC bytes.
            var dataLength = packet[2];
            if (packet.Length != dataLength + Constants.MinPacketLength)
            {
                throw new PacketLengthException(
                    $"Packet data length was defined as {dataLength} bytes, but found {packet.Length - Constants.MinPacketLength} data bytes.");
            }

            var (expectedLsb, expectedMsb) = CalculatePacketCrcFor(packet);
            var actualLsb = packet[packet.Length - 2];
            var actualMsb = packet[packet.Length - 1];

            if (actualLsb != expectedLsb || actualMsb != expectedMsb)
            {
                throw new PacketCrcException(
                    $"Packet CRC-16 is invalid.  Found (0x{actualLsb:X2}, 0x{actualMsb:X2}) " +
                    $"but expected (0x{expectedLsb:X2}, 0x{expectedMsb:X2}).");
            }

            if (sequenceFlag != null)
            {
                var packetFlag = (packet[1] & Constants.SequenceFlagMask) != 0;
                if (sequenceFlag.Value != packetFlag)
                {
                    throw new PacketFormatException(
                        $"Expected sequence flag value of {(sequenceFlag.Value ? 1 : 0)}, found {(packetFlag ? 1 : 0)}.");
                }
            }

            var parsed = new SspRawPacket
            {
                Address = (byte)(packet[1] & Constants.AddressMask),
                Data = new byte[dataLength],
            };
            Array.Copy(packet, 3, parsed.Data, 0, dataLength);

            return parsed;
        }

        /// <summary>
        /// Gets the packet's logical bytes.  Byte stuffing is applied by the stream layer, not here.
        /// </summary>
        /// <param name="sequenceFlag">The sequence flag to encode into the address byte.</param>
        public IEnumerable<byte> GetPacketBytes(bool sequenceFlag = false)
        {
            if (Data.Length > Constants.MaxDataLength)
            {
                throw new PacketLengthException(
                    $"A packet carries at most {Constants.MaxDataLength} data bytes, but this one has {Data.Length}.");
            }

            yield return Constants.STX;

            var sequenceAndAddress = (byte)((Address & Constants.AddressMask) | (sequenceFlag ? Constants.SequenceFlagMask : 0));
            yield return sequenceAndAddress;

            yield return (byte)Data.Length;

            foreach (var b in Data) yield return b;

            // The CRC covers everything between the STX and the CRC itself.
            var crcData = new List<byte>(Data.Length + 2) { sequenceAndAddress, (byte)Data.Length };
            crcData.AddRange(Data);

            var (lsb, msb) = CalculatePacketCrc(crcData.ToArray());

            yield return lsb;
            yield return msb;
        }
    }
}

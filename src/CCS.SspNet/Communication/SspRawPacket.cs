using CCS.SspNet.Exceptions;
using CCS.SspNet.Interfaces;
using System;
using System.Collections.Generic;
using System.Text;
using static CCS.SspNet.Utilities.CrcUtilities;

namespace CCS.SspNet.Communication
{
    internal class SspRawPacket : ISspRawPacket
    {
        internal SspRawPacket() { }

        internal SspRawPacket(byte address, byte[] data)
        {
            Address = address;
            Data = data;
        }

        public byte Address { get; set; }

        public byte[] Data { get; set; }

        /// <summary>
        /// Parses the raw bytes to a raw SSP packet.
        /// </summary>
        /// <param name="packet">The packet bytes.</param>
        /// <param name="sequenceFlag">Optionally will check the sequence flag is 1 (true), 0 (false), or not checked (null).</param>
        /// <returns></returns>
        public static SspRawPacket Parse(byte[] packet, bool? sequenceFlag = null)
        {
            var rawPacket = (byte[])packet.Clone();
            var sspPacket = new SspRawPacket();

            // Packet length must be at least 5 bytes.
            if (rawPacket.Length < 5) throw new PacketLengthException("Raw packet length must be longer than 5 bytes (STX + SEQ/ADDR + LEN + DATA + CRC LSB + CRC MSB).");

            // Packet length can be no longer than 259 bytes.
            if (rawPacket.Length >= 259) throw new PacketLengthException("Raw packet length cannot be longer than 259 bytes (STX + SEQ/ADDR + LEN + DATA + CRC LSB + CRC MSB).");

            // Validate first byte is STX.
            if (rawPacket[0] != Constants.STX) throw new PacketFormatException("Packet does not begin with STX (0x7F) character.");

            // Remove any byte stuffing of STX.
            bool foundSTX = false;
            var newBytes = new List<byte>() { Constants.STX };
            for (short i = 1; i <= rawPacket.Length - 1; i++)
            {
                if (rawPacket[i] == Constants.STX && foundSTX)
                {
                    // Byte stuffing has occured. Do not copy byte.
                    foundSTX = false;
                    continue;
                } 
                else if (rawPacket[i] == Constants.STX)
                {
                    foundSTX = true;
                }
                // Byte stuffing not done correctly. Possible malformed packet.
                else if (foundSTX && rawPacket[i] != Constants.STX) throw new PacketFormatException($"Non-byte-stuffed STX byte encountered within packet (position {i + 1}).  Possible data communication issue.");
                //else
                //{
                //    foundSTX = false;
                //}
                newBytes.Add(rawPacket[i]);
            }
            // If last character is a newly-found STX, byte stuffing did not work correctly.
            if (foundSTX) throw new PacketFormatException($"Non-byte-stuffed STX byte encountered within packet (position {rawPacket.Length}).  Possible data communication issue.");

            rawPacket = newBytes.ToArray();

            // Validate Length
            var length = rawPacket[2];
            if (rawPacket.Length != length + 5) throw new PacketLengthException($"Packet data length was defined as {length} bytes, but found {rawPacket.Length - 5} data bytes.");

            // Validate CRC values.
            if (!ValidatePacketCrc(rawPacket))
            {
                var (lsb, msb) = CalculatePacketCrc(rawPacket);
                var packetCrc = new byte[] { rawPacket[rawPacket.Length - 2], rawPacket[rawPacket.Length - 1] };
                if (!(packetCrc[0] == lsb && packetCrc[1] == msb))
                    throw new PacketCrcException($"Packet CRC-16 is invalid.  Found (0x{packetCrc[0]:X2}, 0x{packetCrc[1]:X2}) " +
                        $"but expected (0x{lsb:X2}, 0x{msb:X2}).");
            }

            // Check sequence if provided.
            if (sequenceFlag != null)
            {
                byte dataFlag = (byte)(rawPacket[1] >> 7);
                if (sequenceFlag.Value && dataFlag != 1) throw new PacketFormatException("Expected sequence flag value of 1, found 0.");
                if (!sequenceFlag.Value && dataFlag != 0) throw new PacketFormatException("Expected sequence flag value of 0, found 1.");
            }

            // All validations complete.  Fill in values.
            sspPacket.Address = (byte)(rawPacket[1] & 0x7F);
            sspPacket.Data = new byte[rawPacket.Length - 5];
            Array.Copy(rawPacket, 3, sspPacket.Data, 0, rawPacket.Length - 5);

            return sspPacket;
        }

        public IEnumerable<byte> GetPacketBytes(bool sequenceFlag = false)
        {
            yield return Constants.STX;
            byte seq = sequenceFlag ? (byte)0x80 : (byte)0;

            var byte2 = (byte)((Address & 0x7F) | seq);
            yield return byte2;

            yield return (byte)Data.Length;

            foreach (byte b in Data)
            {
                yield return b;
            }

            var crcData = new List<byte> { byte2, (byte)Data.Length };
            crcData.AddRange(Data);

            (byte lsb, byte msb) = CalculatePacketCrc(crcData.ToArray());

            yield return lsb;
            yield return msb;

        }
    }
}

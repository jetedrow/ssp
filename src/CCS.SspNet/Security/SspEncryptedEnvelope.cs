using System;
using System.Security.Cryptography;
using CCS.SspNet.Exceptions;
using CCS.SspNet.Utilities;

namespace CCS.SspNet.Security
{
    /// <summary>
    /// The encrypted block that occupies an ordinary packet's data field.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The shape is <c>STEX | eLENGTH | eCOUNT | eDATA | ePACKING | eCRCL | eCRCH</c>, with
    /// everything after the STEX encrypted as one run of AES-128 blocks.  STEX stays in clear
    /// because it is how a receiver tells an encrypted packet from a plain one — the two share a
    /// transport and either may arrive at any time.
    /// </para>
    /// <para>
    /// The padding is random rather than zeroes, which matters: the commands being encrypted are
    /// short and highly repetitive — a poll is one byte — so constant padding would make every
    /// poll encrypt to the same ciphertext and hand an observer a crib.
    /// </para>
    /// </remarks>
    internal static class SspEncryptedEnvelope
    {
        internal const byte Stex = 0x7E;
        internal const int BlockSize = 16;

        // eLENGTH, four count bytes, and the two CRC bytes surround the data in every block.
        private const int Overhead = 1 + 4 + 2;

        /// <summary>
        /// The largest command this can carry.  A packet's data field holds 255 bytes, one goes to
        /// the STEX, and what is left has to be a whole number of AES blocks.
        /// </summary>
        internal const int MaxDataLength = 240 - Overhead;

        internal static byte[] Wrap(byte[] data, byte[] key, uint count, SspCountByteOrder order, RandomNumberGenerator random)
        {
            if (data.Length > MaxDataLength)
            {
                throw new SspEncryptionException(
                    $"An encrypted packet carries at most {MaxDataLength} bytes of command, but this one has {data.Length}.");
            }

            var unpadded = Overhead + data.Length;
            var padding = (BlockSize - (unpadded % BlockSize)) % BlockSize;
            var block = new byte[unpadded + padding];

            block[0] = (byte)data.Length;
            WriteCount(block, count, order);
            Array.Copy(data, 0, block, 5, data.Length);

            if (padding > 0)
            {
                var noise = new byte[padding];
                random.GetBytes(noise);
                Array.Copy(noise, 0, block, 5 + data.Length, padding);
            }

            var (lsb, msb) = CrcUtilities.CalculatePacketCrc(Slice(block, 0, block.Length - 2));
            block[block.Length - 2] = lsb;
            block[block.Length - 1] = msb;

            var encrypted = Transform(block, key, encrypting: true);

            var packet = new byte[1 + encrypted.Length];
            packet[0] = Stex;
            Array.Copy(encrypted, 0, packet, 1, encrypted.Length);
            return packet;
        }

        internal static (uint Count, byte[] Data) Unwrap(byte[] packetData, byte[] key, SspCountByteOrder order)
        {
            if (packetData.Length < 1 || packetData[0] != Stex)
            {
                throw new SspEncryptionException("The packet does not start with STEX, so it is not an encrypted block.");
            }

            var body = Slice(packetData, 1, packetData.Length - 1);
            if (body.Length == 0 || body.Length % BlockSize != 0)
            {
                throw new SspEncryptionException(
                    $"An encrypted block is a whole number of {BlockSize}-byte units, but this one is {body.Length} bytes.");
            }

            var block = Transform(body, key, encrypting: false);

            // The manual describes this check as running the CRC over the whole decrypted block,
            // CRC bytes included, and expecting zero.  That property only holds when the remainder
            // is appended high byte first, and eSSP sends eCRCL before eCRCH like the rest of the
            // protocol, so recalculating and comparing is the same test done the way the bytes
            // actually arrive.
            var (lsb, msb) = CrcUtilities.CalculatePacketCrc(Slice(block, 0, block.Length - 2));
            if (block[block.Length - 2] != lsb || block[block.Length - 1] != msb)
            {
                throw new SspEncryptionException(
                    "The decrypted block failed its CRC, which means it was encrypted with a different key. " +
                    "A device treats this as tampering and goes out of service until it is power cycled, so it is not worth retrying.");
            }

            var length = block[0];
            if (Overhead + length > block.Length)
            {
                throw new SspEncryptionException(
                    $"The decrypted block claims {length} bytes of data, which does not fit in {block.Length} bytes.");
            }

            return (ReadCount(block, order), Slice(block, 5, length));
        }

        private static void WriteCount(byte[] block, uint count, SspCountByteOrder order)
        {
            for (var i = 0; i < 4; i++)
            {
                var shift = order == SspCountByteOrder.LittleEndian ? i * 8 : (3 - i) * 8;
                block[1 + i] = (byte)(count >> shift);
            }
        }

        private static uint ReadCount(byte[] block, SspCountByteOrder order)
        {
            uint count = 0;
            for (var i = 0; i < 4; i++)
            {
                var shift = order == SspCountByteOrder.LittleEndian ? i * 8 : (3 - i) * 8;
                count |= (uint)block[1 + i] << shift;
            }

            return count;
        }

        private static byte[] Transform(byte[] input, byte[] key, bool encrypting)
        {
            using var aes = Aes.Create();
            if (aes == null) throw new SspEncryptionException("This platform provides no AES implementation.");

            aes.Key = key;
            aes.Mode = CipherMode.ECB;
            aes.Padding = PaddingMode.None;

            using var transform = encrypting ? aes.CreateEncryptor() : aes.CreateDecryptor();
            return transform.TransformFinalBlock(input, 0, input.Length);
        }

        private static byte[] Slice(byte[] source, int offset, int length)
        {
            var slice = new byte[length];
            Array.Copy(source, offset, slice, 0, length);
            return slice;
        }
    }
}

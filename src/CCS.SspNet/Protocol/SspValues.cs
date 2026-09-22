using System;
using System.Text;

namespace CCS.SspNet.Protocol
{
    /// <summary>
    /// Reads and writes the few value encodings SSP uses inside command parameters and event
    /// payloads.
    /// </summary>
    /// <remarks>
    /// Monetary amounts are little-endian, serial numbers are big-endian, and country codes are
    /// three ASCII letters.  Mixing the two byte orders up is the easiest mistake to make against
    /// this protocol, so both are named here rather than written out at each call site.
    /// </remarks>
    public static class SspValues
    {
        /// <summary>The length of a country code in a payload.</summary>
        public const int CountryCodeLength = 3;

        /// <summary>
        /// Reads a four-byte little-endian amount, in the dataset's smallest unit — pennies or
        /// cents, not whole currency.
        /// </summary>
        /// <param name="data">The payload, positioned at the amount.</param>
        /// <exception cref="ArgumentException">There are fewer than four bytes left.</exception>
        public static uint ReadAmount(ReadOnlySpan<byte> data)
        {
            if (data.Length < 4) throw new ArgumentException("An amount is four bytes.", nameof(data));

            return (uint)(data[0] | (data[1] << 8) | (data[2] << 16) | (data[3] << 24));
        }

        /// <summary>Writes a four-byte little-endian amount.</summary>
        /// <param name="value">The amount, in the dataset's smallest unit.</param>
        public static byte[] WriteAmount(uint value) => new[]
        {
            (byte)value,
            (byte)(value >> 8),
            (byte)(value >> 16),
            (byte)(value >> 24),
        };

        /// <summary>
        /// Reads a four-byte big-endian number.  Serial numbers are sent this way round, unlike
        /// amounts.
        /// </summary>
        /// <param name="data">The payload, positioned at the number.</param>
        /// <exception cref="ArgumentException">There are fewer than four bytes left.</exception>
        public static uint ReadBigEndian(ReadOnlySpan<byte> data)
        {
            if (data.Length < 4) throw new ArgumentException("A big-endian number is four bytes.", nameof(data));

            return (uint)((data[0] << 24) | (data[1] << 16) | (data[2] << 8) | data[3]);
        }

        /// <summary>
        /// Reads an eight-byte little-endian number.  The three numbers of the encryption key
        /// exchange are sent this way round.
        /// </summary>
        /// <param name="data">The payload, positioned at the number.</param>
        /// <exception cref="ArgumentException">There are fewer than eight bytes left.</exception>
        public static ulong ReadUInt64(ReadOnlySpan<byte> data)
        {
            if (data.Length < 8) throw new ArgumentException("An eight-byte number is eight bytes.", nameof(data));

            ulong value = 0;
            for (var i = 0; i < 8; i++) value |= (ulong)data[i] << (i * 8);
            return value;
        }

        /// <summary>Writes an eight-byte little-endian number.</summary>
        public static byte[] WriteUInt64(ulong value)
        {
            var bytes = new byte[8];
            for (var i = 0; i < 8; i++) bytes[i] = (byte)(value >> (i * 8));
            return bytes;
        }

        /// <summary>
        /// Reads a three-letter ASCII country code, as in <c>EUR</c> or <c>GBP</c>.
        /// </summary>
        /// <param name="data">The payload, positioned at the code.</param>
        /// <exception cref="ArgumentException">There are fewer than three bytes left.</exception>
        public static string ReadCountryCode(ReadOnlySpan<byte> data)
        {
            if (data.Length < CountryCodeLength)
            {
                throw new ArgumentException($"A country code is {CountryCodeLength} bytes.", nameof(data));
            }

#if NETSTANDARD2_0
            var bytes = data.Slice(0, CountryCodeLength).ToArray();
            return Encoding.ASCII.GetString(bytes);
#else
            return Encoding.ASCII.GetString(data.Slice(0, CountryCodeLength));
#endif
        }

        /// <summary>Writes a three-letter ASCII country code.</summary>
        /// <param name="countryCode">The code, which must be exactly three characters.</param>
        /// <exception cref="ArgumentException"><paramref name="countryCode"/> is the wrong length.</exception>
        public static byte[] WriteCountryCode(string countryCode)
        {
            if (countryCode == null) throw new ArgumentNullException(nameof(countryCode));

            if (countryCode.Length != CountryCodeLength)
            {
                throw new ArgumentException(
                    $"A country code is {CountryCodeLength} characters; '{countryCode}' is {countryCode.Length}.",
                    nameof(countryCode));
            }

            return Encoding.ASCII.GetBytes(countryCode);
        }
    }
}

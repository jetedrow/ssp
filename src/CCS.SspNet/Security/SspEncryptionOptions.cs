using System;

namespace CCS.SspNet.Security
{
    /// <summary>
    /// Settings for the encrypted layer: the key half a host already knows, how the key exchange
    /// picks its numbers, and the one wire detail the protocol documents leave open.
    /// </summary>
    public sealed class SspEncryptionOptions
    {
        private int primeBits = 62;

        /// <summary>
        /// Gets or sets the fixed half of the key, which the machine's manufacturer sets.  Defaults
        /// to <see cref="SspEncryptionKey.DefaultFixedHalf"/>, which is what an untouched device
        /// expects.
        /// </summary>
        public ulong FixedKey { get; set; } = SspEncryptionKey.DefaultFixedHalf;

        /// <summary>
        /// Gets or sets how the encrypted block's counter is written.  Defaults to
        /// <see cref="SspCountByteOrder.LittleEndian"/>; see that type for why this is a setting
        /// rather than a constant.
        /// </summary>
        public SspCountByteOrder CountByteOrder { get; set; } = SspCountByteOrder.LittleEndian;

        /// <summary>
        /// Gets or sets how many bits the generated generator and modulus should have.  Defaults to
        /// 62.
        /// </summary>
        /// <remarks>
        /// Both numbers travel as 64-bit integers, but a device raises one to a random power modulo
        /// the other using its own 64-bit arithmetic, so leaving headroom below 63 bits keeps that
        /// out of overflow.  Lowering this weakens the exchange and is only worth doing for a
        /// device that rejects larger primes.
        /// </remarks>
        /// <exception cref="ArgumentOutOfRangeException">The value is below 16 or above 62.</exception>
        public int PrimeBits
        {
            get => primeBits;
            set
            {
                if (value < 16 || value > 62)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(value), value,
                        "The exchange primes are between 16 and 62 bits: smaller is not worth encrypting, and larger risks overflowing a device's 64-bit arithmetic.");
                }

                primeBits = value;
            }
        }

        /// <summary>
        /// Gets or sets the generator and modulus to negotiate with, or <see langword="null"/> to
        /// generate a fresh pair each time.
        /// </summary>
        /// <remarks>
        /// Generating them is the default and costs a few milliseconds.  Fixing them makes a test
        /// repeatable, and suits a host too small to look for primes at startup.
        /// </remarks>
        public SspKeyExchangeParameters? Parameters { get; set; }
    }
}

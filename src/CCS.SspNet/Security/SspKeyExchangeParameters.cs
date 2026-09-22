using System;

namespace CCS.SspNet.Security
{
    /// <summary>
    /// The two public numbers a key exchange runs on.
    /// </summary>
    /// <remarks>
    /// Both must be prime — a device checks and refuses one that is not — and, on eSSP
    /// specifically, the generator must be the larger of the two.  That is unusual for
    /// Diffie-Hellman and is not a detail to discover against real hardware: it comes from the
    /// implementation guide, and a device will simply not agree a key without it.
    /// </remarks>
    public readonly struct SspKeyExchangeParameters : IEquatable<SspKeyExchangeParameters>
    {
        /// <summary>Creates a pair, checking both numbers.</summary>
        /// <exception cref="ArgumentException">
        /// One of the numbers is not prime, or the generator is not larger than the modulus.
        /// </exception>
        public SspKeyExchangeParameters(ulong generator, ulong modulus)
        {
            if (modulus < 3) throw new ArgumentOutOfRangeException(nameof(modulus), modulus, "The modulus must be an odd prime.");
            if (generator <= modulus)
            {
                throw new ArgumentException(
                    $"eSSP requires the generator to be larger than the modulus, but {generator} is not larger than {modulus}.",
                    nameof(generator));
            }

            if (!SspPrimes.IsPrime(modulus)) throw new ArgumentException($"The modulus {modulus} is not prime.", nameof(modulus));
            if (!SspPrimes.IsPrime(generator)) throw new ArgumentException($"The generator {generator} is not prime.", nameof(generator));

            Generator = generator;
            Modulus = modulus;
        }

        /// <summary>Gets the generator, the larger of the two primes.</summary>
        public ulong Generator { get; }

        /// <summary>Gets the modulus.</summary>
        public ulong Modulus { get; }

        /// <summary>
        /// Generates a fresh pair of primes of the given width.
        /// </summary>
        /// <param name="bits">How wide each prime should be.  See <see cref="SspEncryptionOptions.PrimeBits"/>.</param>
        public static SspKeyExchangeParameters Generate(int bits = 62)
        {
            if (bits < 16 || bits > 62)
            {
                throw new ArgumentOutOfRangeException(nameof(bits), bits, "The exchange primes are between 16 and 62 bits wide.");
            }

            ulong first, second;
            do
            {
                first = SspPrimes.RandomPrime(bits);
                second = SspPrimes.RandomPrime(bits);
            }
            while (first == second);

            // The larger one has to be the generator, so which is which is decided here rather
            // than by which was drawn first.
            return first > second
                ? new SspKeyExchangeParameters(first, second)
                : new SspKeyExchangeParameters(second, first);
        }

        /// <inheritdoc/>
        public bool Equals(SspKeyExchangeParameters other) => Generator == other.Generator && Modulus == other.Modulus;

        /// <inheritdoc/>
        public override bool Equals(object? obj) => obj is SspKeyExchangeParameters other && Equals(other);

        /// <inheritdoc/>
        public override int GetHashCode() => unchecked((Generator.GetHashCode() * 397) ^ Modulus.GetHashCode());

        /// <summary>Compares two pairs.</summary>
        public static bool operator ==(SspKeyExchangeParameters left, SspKeyExchangeParameters right) => left.Equals(right);

        /// <summary>Compares two pairs.</summary>
        public static bool operator !=(SspKeyExchangeParameters left, SspKeyExchangeParameters right) => !left.Equals(right);

        /// <inheritdoc/>
        public override string ToString() => $"generator {Generator}, modulus {Modulus}";
    }
}

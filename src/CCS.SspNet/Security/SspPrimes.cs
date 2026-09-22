using System;
using System.Numerics;
using System.Security.Cryptography;

namespace CCS.SspNet.Security
{
    /// <summary>
    /// Prime generation and testing for the key exchange.
    /// </summary>
    /// <remarks>
    /// The protocol asks both ends to check that the generator and modulus are prime, and a device
    /// answers PARAMETER_OUT_OF_RANGE for one that is not, so a host needs this as much as it needs
    /// to make the numbers in the first place.
    /// </remarks>
    internal static class SspPrimes
    {
        // Testing against these bases decides primality outright for every 64-bit number, so
        // nothing here is probabilistic despite being a Miller-Rabin test.
        private static readonly uint[] Witnesses = { 2, 3, 5, 7, 11, 13, 17, 19, 23, 29, 31, 37 };

        internal static bool IsPrime(ulong candidate)
        {
            if (candidate < 2) return false;

            foreach (var witness in Witnesses)
            {
                if (candidate == witness) return true;
                if (candidate % witness == 0) return false;
            }

            var n = new BigInteger(candidate);
            var d = n - 1;
            var r = 0;
            while (d.IsEven)
            {
                d /= 2;
                r++;
            }

            foreach (var witness in Witnesses)
            {
                var x = BigInteger.ModPow(witness, d, n);
                if (x.IsOne || x == n - 1) continue;

                var composite = true;
                for (var i = 0; i < r - 1; i++)
                {
                    x = BigInteger.ModPow(x, 2, n);
                    if (x == n - 1)
                    {
                        composite = false;
                        break;
                    }
                }

                if (composite) return false;
            }

            return true;
        }

        /// <summary>
        /// Finds a prime of exactly <paramref name="bits"/> bits.
        /// </summary>
        internal static ulong RandomPrime(int bits)
        {
            var low = 1UL << (bits - 1);
            var span = low; // the half-open range [low, 2 * low) is every number of this width

            using var random = RandomNumberGenerator.Create();
            var buffer = new byte[8];

            for (var attempt = 0; attempt < 100000; attempt++)
            {
                random.GetBytes(buffer);
                var candidate = low + (ToUInt64(buffer) % span);
                candidate |= 1; // an even number is never the one

                if (IsPrime(candidate)) return candidate;
            }

            throw new InvalidOperationException($"Could not find a {bits}-bit prime.");
        }

        private static ulong ToUInt64(byte[] bytes)
        {
            ulong value = 0;
            for (var i = 0; i < 8; i++) value |= (ulong)bytes[i] << (i * 8);
            return value;
        }
    }
}

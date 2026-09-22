using System;
using System.Numerics;
using System.Security.Cryptography;

namespace CCS.SspNet.Security
{
    /// <summary>
    /// One side of the Diffie-Hellman exchange that agrees the negotiated half of a session key.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The host makes two primes and a secret random number, sends the primes and
    /// <see cref="HostIntermediate"/>, and gets the device's intermediate back.  Raising each
    /// other's intermediate to one's own secret arrives at the same number on both sides without
    /// that number ever being on the wire.
    /// </para>
    /// <para>
    /// The exchange is worth exactly as much as the secret staying secret, so an instance is
    /// single-use: make one per session and let it go afterwards.
    /// </para>
    /// </remarks>
    public sealed class SspKeyExchange
    {
        private readonly BigInteger secret;
        private readonly BigInteger modulus;

        private SspKeyExchange(SspKeyExchangeParameters parameters, ulong secret)
        {
            Parameters = parameters;
            this.secret = secret;
            modulus = parameters.Modulus;

            HostIntermediate = (ulong)BigInteger.ModPow(parameters.Generator, secret, modulus);
        }

        /// <summary>Starts an exchange, generating fresh primes.</summary>
        /// <param name="bits">How wide the primes should be.  See <see cref="SspEncryptionOptions.PrimeBits"/>.</param>
        public static SspKeyExchange Create(int bits = 62) => Create(SspKeyExchangeParameters.Generate(bits));

        /// <summary>Starts an exchange on a given pair of primes.</summary>
        public static SspKeyExchange Create(SspKeyExchangeParameters parameters) =>
            new SspKeyExchange(parameters, RandomSecret(parameters.Modulus));

        /// <summary>
        /// Starts an exchange with the secret supplied rather than drawn, which is what makes an
        /// exchange reproducible in a test.
        /// </summary>
        /// <remarks>Do not use this on a real connection: a predictable secret is no secret.</remarks>
        public static SspKeyExchange CreateForTesting(SspKeyExchangeParameters parameters, ulong secret) =>
            new SspKeyExchange(parameters, secret);

        /// <summary>Gets the primes this exchange runs on.  Both are sent to the device.</summary>
        public SspKeyExchangeParameters Parameters { get; }

        /// <summary>
        /// Gets the number sent to the device with
        /// <see cref="SspCommand.RequestKeyExchange"/>.  Public by design: knowing it does not
        /// give away the key.
        /// </summary>
        public ulong HostIntermediate { get; }

        /// <summary>
        /// Completes the exchange, returning the negotiated half of the session key.
        /// </summary>
        /// <param name="deviceIntermediate">The number the device replied with.</param>
        public ulong CreateSharedSecret(ulong deviceIntermediate) =>
            (ulong)BigInteger.ModPow(deviceIntermediate, secret, modulus);

        private static ulong RandomSecret(ulong modulus)
        {
            using var random = RandomNumberGenerator.Create();
            var buffer = new byte[8];

            while (true)
            {
                random.GetBytes(buffer);

                ulong value = 0;
                for (var i = 0; i < 8; i++) value |= (ulong)buffer[i] << (i * 8);

                // Anything below two makes the intermediate the generator itself or one, which
                // would hand the key to anyone watching.
                value = (value % (modulus - 3)) + 2;
                if (value > 1) return value;
            }
        }
    }
}

using System;
using System.Security.Cryptography;
using CCS.SspNet.Exceptions;

namespace CCS.SspNet.Security
{
    /// <summary>
    /// The key and packet counter one device's encrypted conversation runs on.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The counter is what stops an observer replaying a packet it captured.  It starts at zero
    /// when the key is agreed and climbs from there, and a packet whose counter is not the expected
    /// one is discarded rather than acted on — which is why a session cannot be shared between
    /// devices, and why losing one exchange entirely means renegotiating rather than carrying on.
    /// </para>
    /// <para>
    /// The documents describe the counter twice and not quite identically: once as incrementing on
    /// every packet encrypted and every packet decrypted, and once as incrementing only on
    /// transmission, with the received value compared against the internal one.  Under the first
    /// reading a device's reply carries one more than the command it answers; under the second it
    /// carries the same.  Both are accepted here, and the session then follows whichever the
    /// device used, so neither reading breaks a real conversation.
    /// </para>
    /// </remarks>
    internal sealed class SspEncryptionSession : IDisposable
    {
        private readonly byte[] keyBytes;
        private readonly SspCountByteOrder order;
        private readonly RandomNumberGenerator random = RandomNumberGenerator.Create();
        private bool disposed;

        internal SspEncryptionSession(SspEncryptionKey key, SspCountByteOrder order = SspCountByteOrder.LittleEndian)
        {
            Key = key;
            keyBytes = key.ToArray();
            this.order = order;
        }

        /// <summary>Gets the key this session encrypts with.</summary>
        internal SspEncryptionKey Key { get; }

        /// <summary>Gets the counter as it stands, which is the value the last packet carried.</summary>
        internal uint Count { get; private set; }

        /// <summary>Wraps a command for sending.</summary>
        internal byte[] Encrypt(byte[] data)
        {
            ThrowIfDisposed();
            if (data == null) throw new ArgumentNullException(nameof(data));

            Count++;
            return SspEncryptedEnvelope.Wrap(data, keyBytes, Count, order, random);
        }

        /// <summary>Unwraps a reply.</summary>
        /// <exception cref="SspEncryptionException">
        /// The block would not decrypt, or carried a counter that does not follow on from the
        /// command it answers.
        /// </exception>
        internal byte[] Decrypt(byte[] packetData)
        {
            ThrowIfDisposed();
            if (packetData == null) throw new ArgumentNullException(nameof(packetData));

            var (received, data) = SspEncryptedEnvelope.Unwrap(packetData, keyBytes, order);

            if (received != Count && received != Count + 1)
            {
                throw new SspEncryptionException(
                    $"The reply's packet counter was {received}, but the command it answers was sent as {Count}. " +
                    "A counter that has drifted stays drifted, so the keys need negotiating again.");
            }

            Count = received;
            return data;
        }

        /// <summary>Tells an encrypted packet from a plain one.</summary>
        internal static bool IsEncrypted(byte[] packetData) =>
            packetData != null && packetData.Length > 0 && packetData[0] == SspEncryptedEnvelope.Stex;

        private void ThrowIfDisposed()
        {
            if (disposed) throw new ObjectDisposedException(nameof(SspEncryptionSession));
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            if (disposed) return;
            disposed = true;

            Array.Clear(keyBytes, 0, keyBytes.Length);
            random.Dispose();
        }
    }
}

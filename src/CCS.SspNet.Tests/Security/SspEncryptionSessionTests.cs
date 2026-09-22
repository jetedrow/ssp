using CCS.SspNet.Exceptions;
using CCS.SspNet.Security;
using FluentAssertions;
using System;
using System.Security.Cryptography;
using Xunit;

namespace CCS.SspNet.Tests.Security
{
    /// <summary>
    /// The packet counter that stops a captured packet being replayed.
    /// </summary>
    [Trait(TestCategories.Name, TestCategories.Fast)]
    public class SspEncryptionSessionTests : IDisposable
    {
        private static readonly SspEncryptionKey Key =
            new SspEncryptionKey(SspEncryptionKey.DefaultFixedHalf, 0x1122334455667788);

        private readonly RandomNumberGenerator random = RandomNumberGenerator.Create();
        private readonly SspEncryptionSession session = new SspEncryptionSession(Key);

        [Fact]
        public void TheFirstCommandOfASessionIsCountOne()
        {
            session.Encrypt(new byte[] { 0x07 });

            session.Count.Should().Be(1);
        }

        [Fact]
        public void EachCommandCarriesTheNextCount()
        {
            session.Encrypt(new byte[] { 0x07 });
            session.Decrypt(DeviceReply(new byte[] { 0xF0 }, 1));
            session.Encrypt(new byte[] { 0x07 });

            session.Count.Should().Be(2);
        }

        /// <summary>
        /// The documents describe the counter rule twice and not identically: on one reading a
        /// reply repeats the count of the command it answers, on the other it carries one more.
        /// Both are accepted, because guessing wrong would mean a library that cannot talk to the
        /// device at all.
        /// </summary>
        [Fact]
        public void AReplyMayRepeatTheCountOfTheCommandItAnswers()
        {
            session.Encrypt(new byte[] { 0x07 });

            session.Decrypt(DeviceReply(new byte[] { 0xF0 }, 1)).Should().Equal(0xF0);
            session.Count.Should().Be(1);
        }

        [Fact]
        public void AReplyMayInsteadCountItselfSeparately()
        {
            session.Encrypt(new byte[] { 0x07 });

            session.Decrypt(DeviceReply(new byte[] { 0xF0 }, 2)).Should().Equal(0xF0);
            session.Count.Should().Be(2, "the session follows whichever reading the device turned out to use");
        }

        [Fact]
        public void AReplyOutOfSequenceIsRefused()
        {
            session.Encrypt(new byte[] { 0x07 });

            new Func<object>(() => session.Decrypt(DeviceReply(new byte[] { 0xF0 }, 9)))
                .Should().Throw<SspEncryptionException>().WithMessage("*negotiating again*");
        }

        /// <summary>
        /// A device that has not been keyed yet answers KEY_NOT_SET in clear, so a plain reply has
        /// to be recognised rather than run through the decrypt and rejected.
        /// </summary>
        [Fact]
        public void APlainReplyIsRecognisedAsPlain()
        {
            SspEncryptionSession.IsEncrypted(new byte[] { 0xFA }).Should().BeFalse();
            SspEncryptionSession.IsEncrypted(new byte[] { 0x7E, 0x01 }).Should().BeTrue();
            SspEncryptionSession.IsEncrypted(Array.Empty<byte>()).Should().BeFalse();
        }

        private byte[] DeviceReply(byte[] data, uint count) =>
            SspEncryptedEnvelope.Wrap(data, Key.ToArray(), count, SspCountByteOrder.LittleEndian, random);

        public void Dispose()
        {
            session.Dispose();
            random.Dispose();
        }
    }
}

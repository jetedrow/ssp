using CCS.SspNet.Exceptions;
using CCS.SspNet.Security;
using FluentAssertions;
using System;
using System.Linq;
using System.Security.Cryptography;
using Xunit;

namespace CCS.SspNet.Tests.Security
{
    /// <summary>
    /// The encrypted block that sits inside an ordinary packet's data field.
    /// </summary>
    [Trait(TestCategories.Name, TestCategories.Fast)]
    public class SspEncryptedEnvelopeTests : IDisposable
    {
        private readonly RandomNumberGenerator random = RandomNumberGenerator.Create();
        private readonly byte[] key = new SspEncryptionKey(SspEncryptionKey.DefaultFixedHalf, 0x0123456789ABCDEF).ToArray();

        [Fact]
        public void ACommandSurvivesTheRoundTrip()
        {
            var command = new byte[] { 0x0A, 0x01, 0x02, 0x03 };

            var packet = SspEncryptedEnvelope.Wrap(command, key, 42, SspCountByteOrder.LittleEndian, random);
            var (count, data) = SspEncryptedEnvelope.Unwrap(packet, key, SspCountByteOrder.LittleEndian);

            count.Should().Be(42);
            data.Should().Equal(command);
        }

        /// <summary>
        /// The one encrypted packet the implementation guide prints in full carries a single
        /// command byte and declares seventeen data bytes: the STEX, then one AES block.  Anything
        /// else here would mean the padding rule is wrong.
        /// </summary>
        [Fact]
        public void AOneBytePollFillsExactlyOneBlockBehindTheStex()
        {
            var packet = SspEncryptedEnvelope.Wrap(new byte[] { 0x07 }, key, 0xA7, SspCountByteOrder.LittleEndian, random);

            packet.Length.Should().Be(0x11);
            packet[0].Should().Be(0x7E);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(8)]
        [InlineData(9)]
        [InlineData(100)]
        [InlineData(233)]
        public void EveryLengthPadsUpToABlockBoundaryAndBackAgain(int commandLength)
        {
            var command = new byte[commandLength];
            random.GetBytes(command);

            var packet = SspEncryptedEnvelope.Wrap(command, key, 7, SspCountByteOrder.LittleEndian, random);

            ((packet.Length - 1) % 16).Should().Be(0);
            packet.Length.Should().BeLessThanOrEqualTo(255, "the block still has to fit in one packet");

            SspEncryptedEnvelope.Unwrap(packet, key, SspCountByteOrder.LittleEndian).Data.Should().Equal(command);
        }

        /// <summary>
        /// The padding is random rather than zeroes, and it matters.  A poll is one byte and goes
        /// out several times a second; constant padding would make every one of them encrypt to
        /// the same ciphertext, which tells an observer exactly where the polls are.
        /// </summary>
        [Fact]
        public void TheSameCommandDoesNotEncryptToTheSameBytesTwice()
        {
            var first = SspEncryptedEnvelope.Wrap(new byte[] { 0x07 }, key, 5, SspCountByteOrder.LittleEndian, random);
            var second = SspEncryptedEnvelope.Wrap(new byte[] { 0x07 }, key, 5, SspCountByteOrder.LittleEndian, random);

            first.Should().NotEqual(second);
        }

        [Fact]
        public void ABlockEncryptedWithAnotherKeyFailsItsCrc()
        {
            var other = new SspEncryptionKey(SspEncryptionKey.DefaultFixedHalf, 0xFEDCBA9876543210).ToArray();
            var packet = SspEncryptedEnvelope.Wrap(new byte[] { 0x0A }, other, 1, SspCountByteOrder.LittleEndian, random);

            new Func<object>(() => SspEncryptedEnvelope.Unwrap(packet, key, SspCountByteOrder.LittleEndian))
                .Should().Throw<SspEncryptionException>().WithMessage("*different key*");
        }

        [Fact]
        public void ABlockWrittenOneWayRoundDoesNotReadTheOther()
        {
            var packet = SspEncryptedEnvelope.Wrap(new byte[] { 0x07 }, key, 1, SspCountByteOrder.LittleEndian, random);

            var asWritten = SspEncryptedEnvelope.Unwrap(packet, key, SspCountByteOrder.LittleEndian).Count;
            var theOtherWay = SspEncryptedEnvelope.Unwrap(packet, key, SspCountByteOrder.BigEndian).Count;

            asWritten.Should().Be(1);
            theOtherWay.Should().Be(0x01000000, "reading the counter the wrong way round is the whole risk");
        }

        [Fact]
        public void ACommandTooLongForOnePacketIsRefused()
        {
            new Func<object>(() => SspEncryptedEnvelope.Wrap(
                    new byte[SspEncryptedEnvelope.MaxDataLength + 1], key, 1, SspCountByteOrder.LittleEndian, random))
                .Should().Throw<SspEncryptionException>();
        }

        [Fact]
        public void APlainPacketIsNotMistakenForAnEncryptedOne()
        {
            new Func<object>(() => SspEncryptedEnvelope.Unwrap(new byte[] { 0xF0 }, key, SspCountByteOrder.LittleEndian))
                .Should().Throw<SspEncryptionException>().WithMessage("*STEX*");
        }

        [Fact]
        public void AnEncryptedRunThatIsNotAWholeNumberOfBlocksIsRefused()
        {
            var packet = new byte[] { 0x7E }.Concat(new byte[20]).ToArray();

            new Func<object>(() => SspEncryptedEnvelope.Unwrap(packet, key, SspCountByteOrder.LittleEndian))
                .Should().Throw<SspEncryptionException>();
        }

        public void Dispose() => random.Dispose();
    }
}

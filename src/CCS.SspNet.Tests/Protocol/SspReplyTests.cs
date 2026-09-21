using CCS.SspNet;
using CCS.SspNet.Exceptions;
using CCS.SspNet.Protocol;
using FluentAssertions;
using System;
using Xunit;

namespace CCS.SspNet.Tests.Protocol
{
    /// <summary>
    /// Covers splitting a reply into its response code and data, building commands, and the value
    /// encodings both sides share.
    /// </summary>
    [Trait(TestCategories.Name, TestCategories.Fast)]
    public class SspReplyTests
    {
        [Fact]
        public void AReplySplitsIntoItsResponseCodeAndTheRest()
        {
            // The manual's Get Serial Number example: OK, then the serial big-endian.
            var reply = SspReply.Parse(new byte[] { 0xF0, 0x00, 0x1C, 0x96, 0x2C });

            reply.Code.Should().Be(SspResponse.OK);
            reply.IsOk.Should().BeTrue();
            SspValues.ReadBigEndian(reply.Data.Span).Should().Be(1873452);
        }

        [Fact]
        public void AnEmptyReplyIsRejected()
        {
            Action parse = () => SspReply.Parse(Array.Empty<byte>());

            parse.Should().Throw<PacketFormatException>();
        }

        [Theory]
        [InlineData(SspResponse.CommandNotKnown)]
        [InlineData(SspResponse.WrongParameterCount)]
        [InlineData(SspResponse.KeyNotSet)]
        public void ARefusalThrowsCarryingTheCodeTheDeviceGave(SspResponse code)
        {
            var reply = new SspReply(code, ReadOnlyMemory<byte>.Empty);

            reply.IsOk.Should().BeFalse();
            reply.Invoking(r => r.EnsureOk())
                 .Should().Throw<SspResponseException>()
                 .Which.Response.Should().Be(code);
        }

        [Fact]
        public void AResponseCodeThisLibraryDoesNotNameStillReportsItsValue()
        {
            var reply = new SspReply((SspResponse)0xFC, ReadOnlyMemory<byte>.Empty);

            reply.Invoking(r => r.EnsureOk())
                 .Should().Throw<SspResponseException>()
                 .WithMessage("*0xFC*");
        }

        [Fact]
        public void ACommandWithNoParametersIsJustItsCode()
        {
            SspMessage.Create(SspCommand.Poll).Should().Equal(0x07);
        }

        [Fact]
        public void ACommandCarriesItsParametersAfterItsCode()
        {
            SspMessage.Create(SspCommand.HostProtocolVersion, 0x06).Should().Equal(0x06, 0x06);
        }

        /// <summary>
        /// A device newer than this library still has to be drivable, so a command code that has
        /// no name here can be sent by value.
        /// </summary>
        [Fact]
        public void ACommandThisLibraryDoesNotNameCanStillBeBuilt()
        {
            SspMessage.Create(0x7C, 0x01, 0x02).Should().Equal(0x7C, 0x01, 0x02);
        }

        [Fact]
        public void ACommandTooLongForAPacketIsRejected()
        {
            Action build = () => SspMessage.Create(SspCommand.Poll, new byte[Constants.MaxDataLength]);

            build.Should().Throw<ArgumentException>();
        }

        /// <summary>
        /// Amounts are little-endian and serial numbers are big-endian, which is the easiest thing
        /// to get backwards against this protocol.  Both examples are the manual's own.
        /// </summary>
        [Fact]
        public void AmountsAreLittleEndianAndSerialNumbersAreBigEndian()
        {
            SspValues.ReadAmount(new byte[] { 0x88, 0x13, 0x00, 0x00 }).Should().Be(5000);
            SspValues.ReadBigEndian(new byte[] { 0x00, 0x1C, 0x96, 0x2C }).Should().Be(1873452);
        }

        [Fact]
        public void AnAmountSurvivesARoundTrip()
        {
            SspValues.ReadAmount(SspValues.WriteAmount(1234567)).Should().Be(1234567);
        }

        [Fact]
        public void ACountryCodeSurvivesARoundTrip()
        {
            SspValues.ReadCountryCode(SspValues.WriteCountryCode("EUR")).Should().Be("EUR");
        }

        [Fact]
        public void ACountryCodeOfTheWrongLengthIsRejected()
        {
            Action write = () => SspValues.WriteCountryCode("EU");

            write.Should().Throw<ArgumentException>();
        }
    }
}

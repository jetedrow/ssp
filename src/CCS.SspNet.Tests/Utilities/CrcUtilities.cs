using Xunit;
using FluentAssertions;
using static CCS.SspNet.Utilities.CrcUtilities;

namespace CCS.SspNet.Tests.Utilities
{
    public class CrcUtilities
    {
        [Fact]
        public void Crc16Calculation()
        {
            var data = new byte[] { 0x80, 0x01, 0x11 };

            (byte LSB, byte MSB) expectedCrc = (0x65, 0x82);

            CalculatePacketCrc(data).Should().Be(expectedCrc);
        }

        [Fact]
        public void Crc16Validation()
        {
            // Simple SYNC command with sequence flag ON, address 0x00, and command of 0x11.
            var simpleCommand = new byte[] { 0x7F, 0x80, 0x01, 0x11, 0x65, 0x82 };
            var badCrc = new byte[] { 0x7F, 0x80, 0x01, 0x11, 0x0B, 0xAD };

            ValidatePacketCrc(simpleCommand).Should().BeTrue();
            ValidatePacketCrc(badCrc).Should().BeFalse();
        }
    }
}

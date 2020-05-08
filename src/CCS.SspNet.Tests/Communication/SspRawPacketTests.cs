using CCS.SspNet.Communication;
using CCS.SspNet.Interfaces;
using System;
using Xunit;
using FluentAssertions;
using CCS.SspNet.Exceptions;

namespace CCS.SSPNet.Tests.Communication
{
    public class SspRawPacketTests
    {
        // Simple SYNC command with sequence flag ON, address 0x00, and command of 0x11.
        private readonly byte[] simpleCommand = { 0x7F, 0x80, 0x01, 0x11, 0x65, 0x82 };

        [Fact]
        public void SimpleSspRawCommandParse()
        {
            var packet = SspRawPacket.Parse(simpleCommand);

            packet.Address.Should().Be(0x00);
            packet.Data.Should().Equal(0x11);
        }

        [Fact]
        public void SimpleSspRawCommandParseWithSequenceFlag()
        {
            var packet = SspRawPacket.Parse(simpleCommand, true);

            packet.Address.Should().Be(0x00);
            packet.Data.Should().Equal(0x11);

            Action badParse = () => SspRawPacket.Parse(simpleCommand, false);
            badParse.Should().Throw<PacketFormatException>();

        }

        [Fact]
        public void SimpleSspRawCommandGetBytesFromParse()
        {
            var packet = SspRawPacket.Parse(simpleCommand);
            packet.GetPacketBytes(true).Should().Equal(simpleCommand);
        }

        [Fact]
        public void SimpleSspRawCommandConstructor()
        {
            var packet = new SspRawPacket(0x00, new byte[] { 0x11 });

            packet.Address.Should().Be(0x00);
            packet.Data.Should().Equal(0x11);
        }

        [Fact]
        public void SimpleSspRawCommandGetBytesFromConstructor()
        {
            var packet = new SspRawPacket(0x00, new byte[] { 0x11 });
            packet.GetPacketBytes(true).Should().Equal(simpleCommand);
        }
    }
}

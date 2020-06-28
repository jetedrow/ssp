using CCS.SspNet.Communication;
using CCS.SspNet.Exceptions;
using FluentAssertions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace CCS.SspNet.Tests.Communication
{
    public class SspStreamReaderTests
    {
        // Simple SYNC command with sequence flag ON, address 0x00, and command of 0x11.
        private readonly byte[] simplePacket = { 0x7F, 0x80, 0x01, 0x11, 0x65, 0x82 };
        private readonly byte[] simplePacketXtraData = { 0xB6, 0xC2, 0x7F, 0x80, 0x01, 0x11, 0x65, 0x82, 0xED, 0xAA };

        private readonly byte[] byteStuffedPacket = { 0x7F, 0x80, 0x02, 0x11, 0x7F, 0x7F, 0x65, 0x82 };
        private readonly byte[] byteUnstuffedPacket = { 0x7F, 0x80, 0x02, 0x11, 0x7F, 0x65, 0x82 };

        [Fact]
        public void TestBasicPacketReadTimeout()
        {
            using var ms = new MemoryStream();
            using var sr = new SspStreamReader(ms);

            ms.Write(simplePacket, 0, simplePacket.Length);
            ms.Seek(0, SeekOrigin.Begin);

            sr.Awaiting(r => r.ReadRawPacketAsync()).Should().CompleteWithin(TimeSpan.FromSeconds(1));

        }

        [Fact]
        public async Task TestBasicPacketRead()
        {
            using var ms = new MemoryStream();
            using var sr = new SspStreamReader(ms);

            ms.Write(simplePacket, 0, simplePacket.Length);
            ms.Seek(0, SeekOrigin.Begin);

            var packet = await sr.ReadRawPacketAsync();

            packet.Should().BeEquivalentTo(simplePacket);

        }

        [Fact]
        public async Task TestBasicPacketReadExtraData()
        {
            using var ms = new MemoryStream();
            using var sr = new SspStreamReader(ms);

            ms.Write(simplePacketXtraData, 0, simplePacketXtraData.Length);
            ms.Seek(0, SeekOrigin.Begin);

            var packet = await sr.ReadRawPacketAsync();

            packet.Should().BeEquivalentTo(simplePacket);

        }

        [Fact]
        public async Task TestByteStuffedPacketRead()
        {
            using var ms = new MemoryStream();
            using var sr = new SspStreamReader(ms);

            ms.Write(byteStuffedPacket, 0, byteStuffedPacket.Length);
            ms.Seek(0, SeekOrigin.Begin);

            var packet = await sr.ReadRawPacketAsync();

            packet.Should().BeEquivalentTo(byteUnstuffedPacket); 

        }

        [Fact]
        public void TestByteUnstuffedPacketException()
        {
            using var ms = new MemoryStream();
            using var sr = new SspStreamReader(ms);

            ms.Write(byteUnstuffedPacket, 0, byteUnstuffedPacket.Length);
            ms.Seek(0, SeekOrigin.Begin);

            sr.Awaiting(r => r.ReadRawPacketAsync()).Should().Throw<PacketFormatException>().WithMessage("Non-byte-stuffed STX byte encountered within packet.");

        }
    }
}

using CCS.SspNet.Communication;
using CCS.SspNet.Exceptions;
using CCS.SspNet.Tests.Fakes;
using FluentAssertions;
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace CCS.SspNet.Tests.Communication
{
    /// <summary>
    /// Covers the framing layer end to end: stuffing on the way out, unstuffing on the way in, and
    /// the packet bounds in between.
    /// </summary>
    [Trait(TestCategories.Name, TestCategories.Fast)]
    public class SspFramingTests
    {
        /// <summary>
        /// The defect this whole layer was rewritten for.  Stuffing used to be removed twice — once
        /// by the reader and again by the parser — and never applied on the way out, so a payload
        /// containing the start marker could not survive a round trip.
        /// </summary>
        [Fact]
        public async Task PayloadContainingStxSurvivesARoundTrip()
        {
            var payload = new byte[] { 0x11, Constants.STX, 0x22, Constants.STX, Constants.STX, 0x33 };

            var parsed = await RoundTripAsync(new SspRawPacket(0x00, payload), sequenceFlag: true);

            parsed.Data.Should().Equal(payload);
        }

        [Fact]
        public async Task EveryPayloadByteValueSurvivesARoundTrip()
        {
            // 0x7F is the interesting one, but sweep the whole byte range so nothing else is
            // quietly mangled either.
            var payload = Enumerable.Range(0, 256).Select(i => (byte)i).ToArray();

            var parsed = await RoundTripAsync(new SspRawPacket(0x07, payload.Take(255).ToArray()), sequenceFlag: false);

            parsed.Data.Should().Equal(payload.Take(255));
            parsed.Address.Should().Be(0x07);
        }

        [Fact]
        public void WriterDoublesEveryStxAfterTheStartMarker()
        {
            var packet = new SspRawPacket(0x00, new byte[] { Constants.STX });
            var logical = packet.GetPacketBytes(true).ToArray();

            var wire = WriteToMemory(logical);

            // Exactly one lone STX — the start marker. Every other one is doubled.
            wire[0].Should().Be(Constants.STX);
            CountLoneStx(wire).Should().Be(1);
            wire.Length.Should().BeGreaterThan(logical.Length, "the payload's STX has to be doubled");
        }

        [Fact]
        public async Task ReaderReadsConsecutivePacketsFromOneInstance()
        {
            // The reader used to keep its buffer position in a field that was never reset, so a
            // second read returned the previous packet glued onto the new one.
            var first = new SspRawPacket(0x00, new byte[] { 0x11 });
            var second = new SspRawPacket(0x00, new byte[] { 0x07, 0x2A });

            using var ms = new MemoryStream();
            using (var writer = new SspStreamWriter(ms))
            {
                await writer.WritePacketAsync(first, true);
                await writer.WritePacketAsync(second, false);
            }

            ms.Seek(0, SeekOrigin.Begin);
            using var reader = new SspStreamReader(ms);

            var firstRead = SspRawPacket.Parse(await reader.ReadRawPacketAsync(), true);
            var secondRead = SspRawPacket.Parse(await reader.ReadRawPacketAsync(), false);

            firstRead.Data.Should().Equal(0x11);
            secondRead.Data.Should().Equal(0x07, 0x2A);
        }

        [Fact]
        public async Task AMaximumSizedPacketIsAccepted()
        {
            // 255 data bytes plus 5 of framing is 260. The old bound rejected anything from 259 up.
            var payload = Enumerable.Repeat((byte)0xA5, Constants.MaxDataLength).ToArray();

            var parsed = await RoundTripAsync(new SspRawPacket(0x00, payload), sequenceFlag: false);

            parsed.Data.Should().HaveCount(Constants.MaxDataLength);
        }

        [Fact]
        public void APacketLongerThanTheMaximumIsRejected()
        {
            var tooLong = new byte[Constants.MaxPacketLength + 1];
            tooLong[0] = Constants.STX;

            Action parse = () => SspRawPacket.Parse(tooLong);

            parse.Should().Throw<PacketLengthException>().WithMessage($"*at most {Constants.MaxPacketLength} bytes*");
        }

        [Fact]
        public void APacketShorterThanTheMinimumIsRejected()
        {
            Action parse = () => SspRawPacket.Parse(new byte[] { Constants.STX, 0x80, 0x00, 0x00 });

            parse.Should().Throw<PacketLengthException>().WithMessage($"*at least {Constants.MinPacketLength} bytes*");
        }

        [Fact]
        public void ACorruptCrcReportsTheCrcThePacketShouldHaveCarried()
        {
            // The old message recomputed over the whole packet, CRC bytes included, so the
            // "expected" value it printed was meaningless.
            var packet = new SspRawPacket(0x00, new byte[] { 0x11 });
            var bytes = packet.GetPacketBytes(true).ToArray();

            var expectedLsb = bytes[bytes.Length - 2];
            var expectedMsb = bytes[bytes.Length - 1];
            bytes[bytes.Length - 1] ^= 0xFF;

            Action parse = () => SspRawPacket.Parse(bytes);

            parse.Should().Throw<PacketCrcException>()
                .WithMessage($"*expected (0x{expectedLsb:X2}, 0x{expectedMsb:X2})*");
        }

        [Fact]
        public async Task ReadingIsCancellable()
        {
            // A silent device used to block the caller forever: the old loop polled and slept with
            // no way out.
            var (host, _) = SspLoopbackStream.CreatePair();
            using var reader = new SspStreamReader(host);
            using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

            await reader.Awaiting(r => r.ReadRawPacketAsync(cts.Token))
                .Should().ThrowAsync<OperationCanceledException>();
        }

        [Fact]
        public async Task AStreamThatEndsMidPacketIsReported()
        {
            using var ms = new MemoryStream(new byte[] { Constants.STX, 0x80 });
            using var reader = new SspStreamReader(ms);

            await reader.Awaiting(r => r.ReadRawPacketAsync())
                .Should().ThrowAsync<SspConnectionClosedException>();
        }

        private static async Task<SspRawPacket> RoundTripAsync(SspRawPacket packet, bool sequenceFlag)
        {
            using var ms = new MemoryStream();
            using (var writer = new SspStreamWriter(ms))
            {
                await writer.WritePacketAsync(packet, sequenceFlag);
            }

            ms.Seek(0, SeekOrigin.Begin);
            using var reader = new SspStreamReader(ms);

            return SspRawPacket.Parse(await reader.ReadRawPacketAsync(), sequenceFlag);
        }

        private static byte[] WriteToMemory(byte[] logicalPacket)
        {
            using var ms = new MemoryStream();
            using (var writer = new SspStreamWriter(ms))
            {
                writer.WriteRawPacketAsync(logicalPacket).GetAwaiter().GetResult();
            }

            return ms.ToArray();
        }

        private static int CountLoneStx(byte[] wire)
        {
            var lone = 0;
            for (var i = 0; i < wire.Length; i++)
            {
                if (wire[i] != Constants.STX) continue;

                if (i + 1 < wire.Length && wire[i + 1] == Constants.STX)
                {
                    i++; // A stuffed pair; skip both.
                    continue;
                }

                lone++;
            }

            return lone;
        }
    }
}

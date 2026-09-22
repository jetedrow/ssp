using CCS.SspNet.Exceptions;
using CCS.SspNet.Firmware;
using FluentAssertions;
using System;
using System.Linq;
using Xunit;

namespace CCS.SspNet.Tests.Firmware
{
    /// <summary>
    /// Parsing an ITL firmware file into its header, RAM block and payload.
    /// </summary>
    [Trait(TestCategories.Name, TestCategories.Fast)]
    public class SspFirmwareFileTests
    {
        /// <summary>Builds a well-formed file: ITL marker, update code at 6, RAM size big-endian at 7-10.</summary>
        internal static byte[] Build(int ramSize, int payloadSize, byte updateCode = 0x80)
        {
            var file = new byte[SspFirmwareFile.HeaderLength + ramSize + payloadSize];
            file[0] = 0x49; file[1] = 0x54; file[2] = 0x4C; // ITL
            file[6] = updateCode;
            file[7] = (byte)(ramSize >> 24);
            file[8] = (byte)(ramSize >> 16);
            file[9] = (byte)(ramSize >> 8);
            file[10] = (byte)ramSize;

            // Make every region distinguishable so a mis-split shows up.
            for (var i = SspFirmwareFile.HeaderLength; i < file.Length; i++) file[i] = (byte)(i & 0xFF);
            return file;
        }

        [Fact]
        public void ThePartsAreSplitWhereTheHeaderSaysTheyAre()
        {
            var raw = Build(ramSize: 300, payloadSize: 1000, updateCode: 0x8A);

            var file = SspFirmwareFile.Parse(raw);

            file.RamBlockLength.Should().Be(300);
            file.PayloadLength.Should().Be(1000);
            file.UpdateCode.Should().Be(0x8A);
            file.Header.Length.Should().Be(128);
            file.RamBlock.Length.Should().Be(300);
            file.Payload.Length.Should().Be(1000);
        }

        [Fact]
        public void TheRamSizeIsReadBigEndian()
        {
            // 0x00000200 = 512, so the low byte is at index 10.
            var raw = Build(ramSize: 512, payloadSize: 16);

            SspFirmwareFile.Parse(raw).RamBlockLength.Should().Be(512);
        }

        [Fact]
        public void ThePartsAreTheActualBytesOfTheFile()
        {
            var raw = Build(ramSize: 4, payloadSize: 4);

            var file = SspFirmwareFile.Parse(raw);

            file.Header.ToArray().Take(3).Should().Equal(0x49, 0x54, 0x4C);
            file.RamBlock.ToArray().Should().Equal(raw.Skip(128).Take(4));
            file.Payload.ToArray().Should().Equal(raw.Skip(132).Take(4));
        }

        [Fact]
        public void AFileNotBeginningWithITLIsRefused()
        {
            var raw = Build(ramSize: 4, payloadSize: 4);
            raw[0] = 0x00;

            new Func<object>(() => SspFirmwareFile.Parse(raw))
                .Should().Throw<SspDownloadException>().WithMessage("*ITL*");
        }

        [Fact]
        public void AFileShorterThanItsHeaderIsRefused()
        {
            new Func<object>(() => SspFirmwareFile.Parse(new byte[64]))
                .Should().Throw<SspDownloadException>().WithMessage("*header*");
        }

        [Fact]
        public void ARamSizeThatRunsOffTheEndIsRefused()
        {
            var raw = Build(ramSize: 4, payloadSize: 4);
            // Claim a RAM block far larger than the file.
            raw[7] = 0x00; raw[8] = 0x10; raw[9] = 0x00; raw[10] = 0x00; // 0x00100000

            new Func<object>(() => SspFirmwareFile.Parse(raw))
                .Should().Throw<SspDownloadException>().WithMessage("*does not fit*");
        }

        [Fact]
        public void ParsingCopiesTheBytesSoALaterMutationDoesNotShowThrough()
        {
            var raw = Build(ramSize: 4, payloadSize: 4);
            var file = SspFirmwareFile.Parse(raw);

            raw[6] = 0xFF;

            file.UpdateCode.Should().Be(0x80, "the parsed file holds its own copy");
        }
    }
}

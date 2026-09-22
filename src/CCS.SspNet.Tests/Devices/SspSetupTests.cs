using CCS.SspNet;
using CCS.SspNet.Exceptions;
using CCS.SspNet.Protocol;
using FluentAssertions;
using System;
using System.Linq;
using Xunit;

namespace CCS.SspNet.Tests.Devices
{
    /// <summary>
    /// Reads the setup request reply worked through in ITL's SSP implementation guide.
    /// </summary>
    /// <remarks>
    /// The guide prints this reply and then annotates every field of it, which makes it the one
    /// piece of independent evidence that the offsets and — more easily got wrong — the byte
    /// orders are right.  A setup reply mixes both: the two multipliers are three-byte big endian,
    /// and the per-channel values a few bytes later are four-byte little endian.
    /// </remarks>
    [Trait(TestCategories.Name, TestCategories.Fast)]
    public class SspSetupTests
    {
        /// <summary>
        /// A four-channel EUR validator at protocol version 6, from the implementation guide's
        /// "setup and enable validator" walkthrough.
        /// </summary>
        private const string GuideExample =
            "00 " +                                  // unit type: banknote validator
            "30 33 33 35 " +                         // firmware 0335
            "45 55 52 " +                            // EUR
            "00 00 01 " +                            // value multiplier 1, big endian
            "04 " +                                  // four channels
            "05 0A 14 32 " +                         // legacy channel values
            "02 02 02 02 " +                         // channel security, obsolete
            "00 00 64 " +                            // real value multiplier 100, big endian
            "06 " +                                  // protocol version 6
            "45 55 52 45 55 52 45 55 52 45 55 52 " + // a country code per channel
            "05 00 00 00 " +                         // channel values, little endian
            "0A 00 00 00 " +
            "14 00 00 00 " +
            "32 00 00 00";

        [Fact]
        public void TheGuidesWorkedSetupReplyReadsBackAsTheGuideAnnotatesIt()
        {
            var setup = SspSetup.Parse(ParseHex(GuideExample));

            setup.UnitType.Should().Be(SspUnitType.BanknoteValidator);
            setup.FirmwareVersion.Should().Be("0335");
            setup.CountryCode.Should().Be("EUR");
            setup.ProtocolVersion.Should().Be(new SspProtocolVersion(6));
            setup.ValueMultiplier.Should().Be(1, "the multiplier is big endian, so 00 00 01 is one");
        }

        /// <summary>
        /// The values are the point of the whole reply: a poll reports a credit by channel number,
        /// and this is the only thing that turns that number into an amount of money.
        /// </summary>
        [Fact]
        public void EachChannelCarriesItsOwnValueAndCurrency()
        {
            var setup = SspSetup.Parse(ParseHex(GuideExample));

            setup.Channels.Select(c => c.Number).Should().Equal(1, 2, 3, 4);
            setup.Channels.Select(c => c.Value).Should().Equal(5u, 10u, 20u, 50u);
            setup.Channels.Select(c => c.CountryCode).Should().AllBe("EUR");
        }

        /// <summary>
        /// Reading the four-byte channel values big endian would turn 5 into 83,886,080, which is
        /// the kind of mistake that only shows up as a wildly wrong credit on real money.
        /// </summary>
        [Fact]
        public void ChannelValuesAreLittleEndianEvenThoughTheMultipliersAreNot()
        {
            var setup = SspSetup.Parse(ParseHex(GuideExample));

            setup.Channels[0].Value.Should().Be(5);
            setup.Channels[3].Value.Should().Be(50);
        }

        /// <summary>
        /// Before protocol version 6 there is no expanded segment: a channel's value is one byte
        /// that has to be multiplied up, and every channel is in the dataset's single currency.
        /// </summary>
        [Fact]
        public void AReplyWithoutTheExpandedSegmentFallsBackToTheLegacyLayout()
        {
            var legacy = ParseHex(
                "00 " +
                "30 31 31 30 " +      // firmware 0110
                "47 42 50 " +         // GBP
                "00 00 64 " +         // value multiplier 100
                "03 " +               // three channels
                "05 0A 14 " +         // values 5, 10, 20 before multiplying
                "02 02 02 " +         // channel security
                "00 00 64 " +         // real value multiplier
                "05");                // protocol version 5

            var setup = SspSetup.Parse(legacy);

            setup.ProtocolVersion.Should().Be(new SspProtocolVersion(5));
            setup.ValueMultiplier.Should().Be(100);
            setup.Channels.Select(c => c.Value).Should().Equal(500u, 1000u, 2000u);
            setup.Channels.Select(c => c.CountryCode).Should().AllBe("GBP");
        }

        /// <summary>
        /// Every device type lays this reply out differently past the first eight bytes, so a
        /// device this library does not parse still reports what it is, and hands over the rest.
        /// </summary>
        [Fact]
        public void ADeviceWithAnUnparsedLayoutStillReportsWhatItIsAndKeepsItsBytes()
        {
            var printer = ParseHex("08 30 31 30 30 46 50 31 00 01 00 46 50 31 00 00 02 0E");

            var setup = SspSetup.Parse(printer);

            setup.UnitType.Should().Be(SspUnitType.AddonPrinter);
            setup.FirmwareVersion.Should().Be("0100");
            setup.Channels.Should().BeEmpty();
            setup.ProtocolVersion.Should().BeNull();
            setup.Data.ToArray().Should().Equal(printer);
        }

        [Fact]
        public void AReplyTooShortToBeASetupReplyIsRejected()
        {
            Action parse = () => SspSetup.Parse(new byte[] { 0x00, 0x30 });

            parse.Should().Throw<PacketFormatException>();
        }

        private static byte[] ParseHex(string hex) =>
            hex.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
               .Select(b => Convert.ToByte(b, 16))
               .ToArray();
    }
}

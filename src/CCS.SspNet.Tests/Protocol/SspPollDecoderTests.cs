using CCS.SspNet.Protocol;
using FluentAssertions;
using System.Linq;
using Xunit;

namespace CCS.SspNet.Tests.Protocol
{
    /// <summary>
    /// Covers the decoder's behaviour at the edges: where the version changes the answer, and
    /// where the reply cannot be read to the end.
    /// </summary>
    [Trait(TestCategories.Name, TestCategories.Fast)]
    public class SspPollDecoderTests
    {
        [Fact]
        public void AnEmptyReplyDecodesToNoEvents()
        {
            var result = SspPollDecoder.Decode(System.Array.Empty<byte>(), version: 4);

            result.IsComplete.Should().BeTrue();
            result.Events.Should().BeEmpty();
        }

        /// <summary>
        /// The reason the protocol has versions at all.  A credit event carries one byte at
        /// version 4 and seven at version 9, so the same bytes are two credits at one version and
        /// one credit at the other — and a host reading at the wrong version does not get an error,
        /// it gets a wrong answer.  That is why the version is a required argument rather than
        /// something with a default.
        /// </summary>
        [Fact]
        public void TheSameBytesDecodeDifferentlyAtDifferentVersions()
        {
            // A credit, then seven more bytes.  At version 4 the credit takes one of them as its
            // channel and the other six are six further events; at version 9 the credit swallows
            // all seven as a country code and value.  Both readings are complete and consistent,
            // and only one of them is what the device meant.
            var stream = new byte[] { 0xEE, 0x01, 0xE8, 0xEB, 0xEC, 0xED, 0xE7, 0xE9 };

            var atFour = SspPollDecoder.Decode(stream, version: 4);
            var atNine = SspPollDecoder.Decode(stream, version: 9);

            atFour.IsComplete.Should().BeTrue();
            atFour.Events.Select(e => e.Code).Should().Equal(0xEE, 0xE8, 0xEB, 0xEC, 0xED, 0xE7, 0xE9);
            atFour.Events[0].Data.ToArray().Should().Equal(new byte[] { 0x01 });

            atNine.IsComplete.Should().BeTrue();
            atNine.Events.Should().ContainSingle();
            atNine.Events[0].Event.Should().Be(SspEvent.NoteCredit);
            atNine.Events[0].Data.ToArray().Should().Equal(new byte[] { 0x01, 0xE8, 0xEB, 0xEC, 0xED, 0xE7, 0xE9 });
        }

        /// <summary>
        /// An event added after the version the device is set to should never arrive, so the table
        /// declining to decode it is the right answer rather than a gap.
        /// </summary>
        [Fact]
        public void AnEventOlderThanItsFirstVersionIsNotDecoded()
        {
            // Value Added arrives from version 7 onwards.
            var result = SspPollDecoder.Decode(new byte[] { 0xBF, 0x00 }, version: 6);

            result.IsComplete.Should().BeFalse();
            result.StoppedAtCode.Should().Be(0xBF);
        }

        /// <summary>
        /// The failure mode the protocol manual warns about.  Nothing on the wire says how long a
        /// payload is, so an unrecognised code leaves no way to find the next one — the rest of the
        /// reply has to be abandoned.  What must not happen is the decoder guessing and reporting
        /// events that did not occur.
        /// </summary>
        [Fact]
        public void AnUnknownCodeStopsTheDecodeAndKeepsWhatCameBefore()
        {
            var stream = new byte[] { 0xEB, 0x7C, 0x11, 0x22, 0xEC };

            var result = SspPollDecoder.Decode(stream, version: 4);

            result.IsComplete.Should().BeFalse();
            result.StoppedAtCode.Should().Be(0x7C);
            result.StopReason.Should().Contain("0x7C");

            result.Events.Should().ContainSingle();
            result.Events[0].Event.Should().Be(SspEvent.Stacked);

            // Everything from the unknown code on is handed back rather than dropped.
            result.UndecodedData.ToArray().Should().Equal(new byte[] { 0x7C, 0x11, 0x22, 0xEC });
        }

        /// <summary>
        /// Five events are named by the manual with no payload size anywhere, and no worked packet
        /// to read one off.  Guessing zero would read the rest of the reply out of step whenever
        /// the guess was wrong, so the decoder stops instead — and says which code it stopped at,
        /// so a host that does know can register it.
        /// </summary>
        [Theory]
        [InlineData(0xD3)] // Coins Low
        [InlineData(0xC0)] // Maintenance Required
        [InlineData(0xBA)] // Coin Rejected
        [InlineData(0xA7)] // Ticket In Bezel At Startup
        [InlineData(0x8B)] // Escrow Active
        public void AnEventWithNoDocumentedSizeStopsTheDecode(byte code)
        {
            var result = SspPollDecoder.Decode(new[] { code }, version: 9);

            result.IsComplete.Should().BeFalse();
            result.StoppedAtCode.Should().Be(code);
        }

        [Fact]
        public void RegisteringASizeLetsAPreviouslyUndecodableEventThrough()
        {
            var table = SspEventTable.Default
                .WithEvent((byte)SspEvent.CoinsLow, SspProtocolVersion.Lowest, SspEventPayload.None);

            var result = SspPollDecoder.Decode(new byte[] { 0xD3, 0xEB }, version: 4, table);

            result.IsComplete.Should().BeTrue();
            result.Events.Select(e => e.Event).Should().Equal(SspEvent.CoinsLow, SspEvent.Stacked);
        }

        /// <summary>
        /// A reply that ends in the middle of a payload is a framing problem, not an unknown
        /// event, but it has to be reported the same way: whatever came before is still good.
        /// </summary>
        [Fact]
        public void APayloadRunningPastTheEndOfTheReplyStopsTheDecode()
        {
            // Fraud Attempt carries one byte, and there is none.
            var result = SspPollDecoder.Decode(new byte[] { 0xEB, 0xE6 }, version: 4);

            result.IsComplete.Should().BeFalse();
            result.StoppedAtCode.Should().Be(0xE6);
            result.StopReason.Should().Contain("more data than the reply has left");
            result.Events.Should().ContainSingle();
        }

        /// <summary>
        /// The multi-currency payout events size themselves from a count byte, so the same event
        /// is a different length on a device with one currency loaded than on one with three.
        /// </summary>
        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(3)]
        public void ACountPrefixedPayloadSizesItselfFromItsCountByte(byte currencies)
        {
            var stream = new byte[] { (byte)SspEvent.Dispensed, currencies }
                .Concat(Enumerable.Repeat((byte)0x00, currencies * 7))
                .Concat(new[] { (byte)SspEvent.Stacked })
                .ToArray();

            var result = SspPollDecoder.Decode(stream, version: 6);

            result.IsComplete.Should().BeTrue();
            result.Events.Select(e => e.Event).Should().Equal(SspEvent.Dispensed, SspEvent.Stacked);
            result.Events[0].Data.Length.Should().Be(1 + (currencies * 7));
        }

        /// <summary>
        /// Error During Payout is the only count-prefixed event with anything after the last
        /// currency block — a byte saying what went wrong.  Missing it would put every later event
        /// in the reply one byte out.
        /// </summary>
        [Fact]
        public void ErrorDuringPayoutCarriesATrailingCauseByteAfterItsCurrencyBlocks()
        {
            var stream = new byte[]
            {
                (byte)SspEvent.ErrorDuringPayout,
                0x01,                                     // one currency
                0x88, 0x13, 0x00, 0x00, 0x47, 0x42, 0x50, // GBP 50.00
                0x03,                                     // cause: payout stalled
                (byte)SspEvent.Stacked,
            };

            var result = SspPollDecoder.Decode(stream, version: 7);

            result.IsComplete.Should().BeTrue();
            result.Events.Select(e => e.Event).Should().Equal(SspEvent.ErrorDuringPayout, SspEvent.Stacked);

            var payload = result.Events[0].Data.Span;
            payload.Length.Should().Be(9);
            SspValues.ReadAmount(payload.Slice(1)).Should().Be(5000);
            SspValues.ReadCountryCode(payload.Slice(5)).Should().Be("GBP");
            payload[8].Should().Be(0x03);
        }

        [Fact]
        public void AnUnnamedCodeIsStillReportedWithItsRawValue()
        {
            var table = SspEventTable.Default.WithEvent(0x7C, SspProtocolVersion.Lowest, SspEventPayload.Fixed(1));

            var result = SspPollDecoder.Decode(new byte[] { 0x7C, 0x42 }, version: 4, table);

            result.IsComplete.Should().BeTrue();
            result.Events[0].Code.Should().Be(0x7C);
            result.Events[0].IsKnown.Should().BeFalse();
            result.Events[0].ToString().Should().Contain("0x7C");
        }
    }
}

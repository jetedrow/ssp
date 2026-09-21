using CCS.SspNet.Protocol;
using FluentAssertions;
using Xunit;

namespace CCS.SspNet.Tests.Protocol
{
    /// <summary>
    /// Covers how the table picks a payload shape, and how it is extended for devices newer than
    /// this library.
    /// </summary>
    [Trait(TestCategories.Name, TestCategories.Fast)]
    public class SspEventTableTests
    {
        [Theory]
        [InlineData(4, 1)]
        [InlineData(6, 1)]
        [InlineData(8, 1)]
        [InlineData(9, 7)]
        [InlineData(20, 7)] // A version past anything documented still gets the newest rule.
        public void AVersionedEventResolvesToTheNewestShapeAtOrBelowTheVersion(byte version, int expectedSize)
        {
            SspEventTable.Default
                .TryGetPayload((byte)SspEvent.NoteCredit, version, out var payload)
                .Should().BeTrue();

            payload.Size.Should().Be(expectedSize);
        }

        [Fact]
        public void AnEventIsNotResolvedBelowTheVersionItWasIntroducedAt()
        {
            SspEventTable.Default
                .TryGetPayload((byte)SspEvent.CoinCredit, version: 4, out _)
                .Should().BeFalse("coin credit arrives at version 5");
        }

        [Fact]
        public void AnEventWithNoDocumentedSizeIsNotResolved()
        {
            SspEventTable.Default
                .TryGetPayload((byte)SspEvent.CoinsLow, SspProtocolVersion.Highest, out _)
                .Should().BeFalse();
        }

        [Fact]
        public void AnEventTheTableHasNeverHeardOfIsNotResolved()
        {
            SspEventTable.Default.TryGetPayload(0x7C, SspProtocolVersion.Highest, out _).Should().BeFalse();
        }

        /// <summary>
        /// The reason the table is data rather than a switch: a device shipping a new event should
        /// not need a new release of this library to be usable.
        /// </summary>
        [Fact]
        public void AnEventCanBeAddedForADeviceNewerThanThisLibrary()
        {
            var table = SspEventTable.Default.WithEvent(0x7C, firstVersion: 10, SspEventPayload.Fixed(4));

            table.TryGetPayload(0x7C, version: 10, out var payload).Should().BeTrue();
            payload.Size.Should().Be(4);

            table.TryGetPayload(0x7C, version: 9, out _).Should().BeFalse("it does not exist before version 10");
        }

        [Fact]
        public void ExtendingTheTableLeavesTheOriginalAlone()
        {
            var extended = SspEventTable.Default.WithEvent(0x7C, SspProtocolVersion.Lowest, SspEventPayload.None);

            extended.TryGetPayload(0x7C, SspProtocolVersion.Lowest, out _).Should().BeTrue();
            SspEventTable.Default.TryGetPayload(0x7C, SspProtocolVersion.Lowest, out _).Should().BeFalse();
        }

        [Fact]
        public void RegisteringAtAVersionThatAlreadyHasAShapeReplacesIt()
        {
            var table = SspEventTable.Default
                .WithEvent((byte)SspEvent.NoteCredit, firstVersion: 4, SspEventPayload.Fixed(2));

            table.TryGetPayload((byte)SspEvent.NoteCredit, version: 4, out var atFour).Should().BeTrue();
            atFour.Size.Should().Be(2);

            table.TryGetPayload((byte)SspEvent.NoteCredit, version: 9, out var atNine).Should().BeTrue();
            atNine.Size.Should().Be(7, "the version 9 shape is untouched");
        }

        [Fact]
        public void AnEventCanBeRemoved()
        {
            var table = SspEventTable.Default.WithoutEvent((byte)SspEvent.Stacked);

            table.TryGetPayload((byte)SspEvent.Stacked, SspProtocolVersion.Lowest, out _).Should().BeFalse();
        }

        /// <summary>
        /// Neither of these appears in issue 2.2 of the protocol manual, but both are defined by
        /// earlier issues and sent by devices in the field, so dropping them would break replies
        /// that decode today.
        /// </summary>
        [Theory]
        [InlineData(SspEvent.SafeJam)]
        [InlineData(SspEvent.CashboxTamper)]
        public void EventsDroppedFromTheCurrentManualAreStillDecodable(SspEvent code)
        {
            SspEventTable.Default
                .TryGetPayload((byte)code, SspProtocolVersion.Lowest, out var payload)
                .Should().BeTrue();

            payload.Size.Should().Be(0);
        }
    }
}

using CCS.SspNet.Protocol;
using FluentAssertions;
using System;
using System.Linq;
using Xunit;

namespace CCS.SspNet.Tests.Protocol
{
    /// <summary>
    /// Decodes every poll reply printed in issue 2.2 of the SSP protocol manual.
    /// </summary>
    /// <remarks>
    /// <para>
    /// These are the manual's own worked packets, copied byte for byte, and each one's CRC checks
    /// out against <see cref="CCS.SspNet.Utilities.CrcUtilities"/>.  They are the only independent
    /// evidence available that the payload lengths in <see cref="SspEventTable"/> are right: get a
    /// length wrong by one and the events after it in a multi-event reply decode as nonsense, which
    /// is exactly what this asserts does not happen.
    /// </para>
    /// <para>
    /// The version against each packet is the lowest one that decodes it, and it agrees with the
    /// manual's caption wherever the manual gives one.
    /// </para>
    /// <para>
    /// Eight of the manual's poll examples are not here, because the manual prints them wrongly:
    /// six drop the leading event code byte, one prints the FAIL response code 0xF8 where the
    /// Disabled event 0xE8 belongs, and two drop a country code out of the middle of a payload.
    /// All eight carry a length and CRC consistent with their own wrong bytes, so they cannot be
    /// distinguished from correct packets by checking — only by decoding them.  See
    /// <c>docs/protocol-support.md</c>.
    /// </para>
    /// </remarks>
    [Trait(TestCategories.Name, TestCategories.Fast)]
    public class ManualPollExampleTests
    {
        [Theory]
        [InlineData("7F 80 02 F0 C6 AB A2", 4, new byte[] { 0xC6 }, new[] { 0 })] // Payout Out Of Service
        [InlineData("7F 80 02 F0 D1 D9 A2", 4, new byte[] { 0xD1 }, new[] { 0 })] // Barcode Ticket Ack
        [InlineData("7F 80 02 F0 DB E5 A2", 4, new byte[] { 0xDB }, new[] { 0 })] // Note Stored In Payout
        [InlineData("7F 80 02 F0 E5 62 22", 4, new byte[] { 0xE5 }, new[] { 0 })] // Barcode Ticket Validated
        [InlineData("7F 80 02 F0 E7 6D A2", 4, new byte[] { 0xE7 }, new[] { 0 })] // Stacker Full
        [InlineData("7F 80 02 F0 E8 4F A2", 4, new byte[] { 0xE8 }, new[] { 0 })] // Disabled
        [InlineData("7F 80 02 F0 E9 4A 22", 4, new byte[] { 0xE9 }, new[] { 0 })] // Unsafe Jam
        [InlineData("7F 80 02 F0 EB 45 A2", 4, new byte[] { 0xEB }, new[] { 0 })] // Stacked
        [InlineData("7F 80 02 F0 EC 54 22", 4, new byte[] { 0xEC }, new[] { 0 })] // Rejected
        [InlineData("7F 80 02 F0 ED 51 A2", 4, new byte[] { 0xED }, new[] { 0 })] // Rejecting
        [InlineData("7F 80 03 F0 E1 00 CC 6E", 4, new byte[] { 0xE1 }, new[] { 1 })] // Note Cleared From Front
        [InlineData("7F 80 03 F0 E6 02 C0 7C", 4, new byte[] { 0xE6 }, new[] { 1 })] // Fraud Attempt
        [InlineData("7F 80 03 F0 EE 04 D7 CC", 4, new byte[] { 0xEE }, new[] { 1 })] // Note Credit
        [InlineData("7F 80 03 F0 EF 00 CF CA", 4, new byte[] { 0xEF }, new[] { 1 })] // Read
        [InlineData("7F 80 03 F0 EF 03 C5 CA", 4, new byte[] { 0xEF }, new[] { 1 })] // Read
        [InlineData("7F 80 04 F0 EE 01 EB B9 48", 4, new byte[] { 0xEE, 0xEB }, new[] { 1, 0 })] // Poll
        [InlineData("7F 80 02 F0 C2 B0 22", 5, new byte[] { 0xC2 }, new[] { 0 })] // Emptying
        [InlineData("7F 80 02 F0 C3 B5 A2", 5, new byte[] { 0xC3 }, new[] { 0 })] // Emptied
        [InlineData("7F 80 02 F0 C7 AE 22", 5, new byte[] { 0xC7 }, new[] { 0 })] // Note Float Removed
        [InlineData("7F 80 02 F0 C8 8C 22", 5, new byte[] { 0xC8 }, new[] { 0 })] // Note Float Attached
        [InlineData("7F 80 02 F0 E3 76 22", 5, new byte[] { 0xE3 }, new[] { 0 })] // Cashbox Removed
        [InlineData("7F 80 02 F0 E4 67 A2", 5, new byte[] { 0xE4 }, new[] { 0 })] // Cashbox Replaced
        [InlineData("7F 80 03 F0 E2 02 C3 E4", 5, new byte[] { 0xE2 }, new[] { 1 })] // Note Cleared Into Cashbox
        [InlineData("7F 80 06 F0 D5 E6 00 00 00 49 DB", 5, new byte[] { 0xD5 }, new[] { 4 })] // Hopper Jammed
        [InlineData("7F 90 02 F0 C4 A2 62", 5, new byte[] { 0xC4 }, new[] { 0 })] // Coin Mech Jammed
        [InlineData("7F 90 06 F0 DE C8 00 00 00 68 00", 5, new byte[] { 0xDE }, new[] { 4 })] // Cashbox Paid
        [InlineData("7F 80 02 F0 92 50 23", 6, new byte[] { 0x92 }, new[] { 0 })] // Cashbox Back In Service
        [InlineData("7F 80 02 F0 93 55 A3", 6, new byte[] { 0x93 }, new[] { 0 })] // Cashbox Unlock Enabled
        [InlineData("7F 80 02 F0 A0 FF A3", 6, new byte[] { 0xA0 }, new[] { 0 })] // Tickets Low
        [InlineData("7F 80 02 F0 A1 FA 23", 6, new byte[] { 0xA1 }, new[] { 0 })] // Tickets Replaced
        [InlineData("7F 80 02 F0 A2 F0 23", 6, new byte[] { 0xA2 }, new[] { 0 })] // Printer Head Removed
        [InlineData("7F 80 02 F0 A4 E4 23", 6, new byte[] { 0xA4 }, new[] { 0 })] // Ticket Jam
        [InlineData("7F 80 02 F0 A5 E1 A3", 6, new byte[] { 0xA5 }, new[] { 0 })] // Ticket Printing
        [InlineData("7F 80 02 F0 A6 EB A3", 6, new byte[] { 0xA6 }, new[] { 0 })] // Ticket Printed
        [InlineData("7F 80 02 F0 A9 C9 A3", 6, new byte[] { 0xA9 }, new[] { 0 })] // Printer Head Replaced
        [InlineData("7F 80 02 F0 AA C3 A3", 6, new byte[] { 0xAA }, new[] { 0 })] // Ticket Path Closed
        [InlineData("7F 80 02 F0 AB C6 23", 6, new byte[] { 0xAB }, new[] { 0 })] // No Paper
        [InlineData("7F 80 02 F0 AC D7 A3", 6, new byte[] { 0xAC }, new[] { 0 })] // Paper Replaced
        [InlineData("7F 80 02 F0 AD D2 23", 6, new byte[] { 0xAD }, new[] { 0 })] // Ticket In Bezel
        [InlineData("7F 80 02 F0 AE D8 23", 6, new byte[] { 0xAE }, new[] { 0 })] // Print Halted
        [InlineData("7F 80 02 F0 AF DD A3", 6, new byte[] { 0xAF }, new[] { 0 })] // Printed To Cashbox
        [InlineData("7F 80 02 F0 E0 7C 22", 6, new byte[] { 0xE0 }, new[] { 0 })] // Note Path Open
        [InlineData("7F 80 03 F0 90 04 D2 48", 6, new byte[] { 0x90 }, new[] { 1 })] // Cashbox Out Of Service
        [InlineData("7F 80 03 F0 A8 08 F9 58", 6, new byte[] { 0xA8 }, new[] { 1 })] // Ticket Printing Error
        [InlineData("7F 80 09 F0 C9 F4 01 00 00 45 55 52 DA C9", 6, new byte[] { 0xC9 }, new[] { 7 })] // Note Transfered To Stacker
        [InlineData("7F 80 09 F0 CD E8 03 00 00 45 55 52 02 64", 6, new byte[] { 0xCD }, new[] { 7 })] // Note Dispensed At Reset
        [InlineData("7F 80 0A F0 B3 01 D4 08 00 00 45 55 52 44 F6", 6, new byte[] { 0xB3 }, new[] { 8 })] // Smart Emptying
        [InlineData("7F 80 0A F0 D6 01 FA 05 00 00 45 55 52 4D 49", 6, new byte[] { 0xD6 }, new[] { 8 })] // Halted
        [InlineData("7F 80 0A F0 D8 01 02 08 00 00 45 55 52 81 C0", 6, new byte[] { 0xD8 }, new[] { 8 })] // Floated
        [InlineData("7F 90 02 F0 BD B7 E3", 6, new byte[] { 0xBD }, new[] { 0 })] // Attached Coin Mech Disabled
        [InlineData("7F 90 02 F0 BE BD E3", 6, new byte[] { 0xBE }, new[] { 0 })] // Attached Coin Mech Enabled
        [InlineData("7F 90 09 F0 DF F4 01 00 00 47 42 50 89 0F", 6, new byte[] { 0xDF }, new[] { 7 })] // Coin Credit
        [InlineData("7F 90 11 F0 DE 02 12 02 00 00 47 42 50 14 00 00 00 45 55 52 3A 50", 6, new byte[] { 0xDE }, new[] { 15 })] // Cashbox Paid
        [InlineData("7F 80 02 F0 B0 9C 23", 7, new byte[] { 0xB0 }, new[] { 0 })] // Jam Recovery
        [InlineData("7F 80 02 F0 B5 82 23", 7, new byte[] { 0xB5 }, new[] { 0 })] // Channel Disable
        [InlineData("7F 80 02 F0 B6 88 23", 7, new byte[] { 0xB6 }, new[] { 0 })] // Initialising
        [InlineData("7F 80 03 F0 83 03 C0 22", 7, new byte[] { 0x83 }, new[] { 1 })] // Calibration Failed
        [InlineData("7F 80 03 F0 B7 14 B1 1A", 7, new byte[] { 0xB7 }, new[] { 1 })] // Coin Mech Error
        [InlineData("7F 80 0A F0 BF 01 26 02 00 00 45 55 52 ED 91", 7, new byte[] { 0xBF }, new[] { 8 })] // Value Added
        [InlineData("7F 80 11 F0 BF 02 DC 00 00 00 45 55 52 68 01 00 00 47 42 50 D1 05", 7, new byte[] { 0xBF }, new[] { 15 })] // Value Added
        [InlineData("7F 80 09 F0 CA F4 01 00 00 45 55 52 D0 F9", 8, new byte[] { 0xCA }, new[] { 7 })] // Note Into Stacker At Reset
        [InlineData("7F 80 09 F0 CB D0 07 00 00 47 42 50 B7 2D", 8, new byte[] { 0xCB }, new[] { 7 })] // Note Into Store At Reset
        [InlineData("7F 80 09 F0 CE E8 03 00 00 45 55 52 08 54", 8, new byte[] { 0xCE }, new[] { 7 })] // Note Held In Bezel
        public void ManualExampleDecodesToTheEventsItDocuments(
            string packetHex, byte version, byte[] expectedCodes, int[] expectedDataSizes)
        {
            var packet = ParseHex(packetHex);
            var reply = SspReply.Parse(PayloadOf(packet));

            reply.IsOk.Should().BeTrue("every poll example in the manual is an OK reply");

            var result = SspPollDecoder.Decode(reply.Data.Span, version);

            result.IsComplete.Should().BeTrue(
                "the whole reply should decode, but it stopped: {0}", result.StopReason);
            result.Events.Select(e => e.Code).Should().Equal(expectedCodes);
            result.Events.Select(e => e.Data.Length).Should().Equal(expectedDataSizes);
        }

        /// <summary>
        /// The version recorded against each packet is the lowest one it decodes at, so decoding
        /// it one version lower must fail.
        /// </summary>
        /// <remarks>
        /// This is what makes the version column mean something.  Without it, a table that had
        /// every event arriving at version 4 would pass every other test in this class — each
        /// packet would still decode, just at a version it should not have been readable at.  That
        /// was the state of the table at one point, and this is the test that catches it.
        /// </remarks>
        [Theory]
        [MemberData(nameof(ExamplesAboveTheLowestVersion))]
        public void AManualExampleDoesNotDecodeOneVersionBelowTheOneItNeeds(string packetHex, byte version)
        {
            var packet = ParseHex(packetHex);
            var eventData = SspReply.Parse(PayloadOf(packet)).Data;

            var lower = SspPollDecoder.Decode(eventData.Span, (byte)(version - 1));

            lower.IsComplete.Should().BeFalse(
                "{0} carries an event that is not sent below protocol version {1}", packetHex, version);
        }

        public static TheoryData<string, byte> ExamplesAboveTheLowestVersion()
        {
            var data = new TheoryData<string, byte>();

            foreach (var row in typeof(ManualPollExampleTests)
                         .GetMethod(nameof(ManualExampleDecodesToTheEventsItDocuments))!
                         .GetCustomAttributes(typeof(InlineDataAttribute), false)
                         .Cast<InlineDataAttribute>()
                         .Select(a => a.GetData(null!).Single()))
            {
                var version = Convert.ToByte(row[1], System.Globalization.CultureInfo.InvariantCulture);
                if (version > SspProtocolVersion.Lowest.Value)
                {
                    data.Add((string)row[0]!, version);
                }
            }

            return data;
        }

        /// <summary>
        /// The manual's headline poll example, which is the clearest statement of what a reply
        /// looks like when more than one thing has happened since the last poll: a credit carrying
        /// the channel it was on, then a stack carrying nothing.
        /// </summary>
        [Fact]
        public void TwoEventsInOneReplyAreReadInOrderWithTheirOwnData()
        {
            var packet = ParseHex("7F 80 04 F0 EE 01 EB B9 48");

            var result = SspPollDecoder.Decode(SspReply.Parse(PayloadOf(packet)).Data.Span, version: 4);

            result.Events.Should().HaveCount(2);
            result.Events[0].Event.Should().Be(SspEvent.NoteCredit);
            result.Events[0].Data.ToArray().Should().Equal(new byte[] { 0x01 });
            result.Events[1].Event.Should().Be(SspEvent.Stacked);
            result.Events[1].Data.ToArray().Should().BeEmpty();
        }

        /// <summary>
        /// Every packet the manual prints carries its own CRC, so the corpus doubles as a check on
        /// the CRC implementation against a source outside this repository.
        /// </summary>
        [Theory]
        [InlineData("7F 80 01 07 12 02")]
        [InlineData("7F 80 04 F0 EE 01 EB B9 48")]
        [InlineData("7F 80 11 F0 BF 02 DC 00 00 00 45 55 52 68 01 00 00 47 42 50 D1 05")]
        public void ManualPacketsCarryACrcThisLibraryAgreesWith(string packetHex)
        {
            var packet = ParseHex(packetHex);

            CCS.SspNet.Utilities.CrcUtilities.ValidatePacketCrc(packet).Should().BeTrue();
        }

        private static byte[] ParseHex(string hex) =>
            hex.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
               .Select(b => Convert.ToByte(b, 16))
               .ToArray();

        /// <summary>Takes the data field out of a whole packet, dropping STX, SEQ/ADDR, LEN and the CRC.</summary>
        private static byte[] PayloadOf(byte[] packet) => packet.Skip(3).Take(packet[2]).ToArray();
    }
}

using CCS.SspNet;
using CCS.SspNet.Exceptions;
using CCS.SspNet.Protocol;
using CCS.SspNet.Tests.Fakes;
using FluentAssertions;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace CCS.SspNet.Tests.Devices
{
    /// <summary>
    /// Drives the procedural API against a simulated device over the real framing and link layers.
    /// </summary>
    [Trait(TestCategories.Name, TestCategories.Fast)]
    public class SspDeviceTests
    {
        private static readonly byte[] Ok = { (byte)SspResponse.OK };
        private static readonly byte[] Fail = { (byte)SspResponse.Failure };

        [Fact]
        public async Task AnAcceptedCommandCompletesQuietly()
        {
            using var harness = new Harness();
            harness.Device.Respond((byte)SspCommand.Enable, Ok);

            await harness.Ssp.EnableAsync();

            harness.Device.ReceivedCommands.Should().ContainSingle()
                   .Which.Should().Equal((byte)SspCommand.Enable);
        }

        [Fact]
        public async Task ARefusedCommandThrowsCarryingTheCodeTheDeviceGave()
        {
            using var harness = new Harness();
            harness.Device.Respond((byte)SspCommand.Enable, (byte)SspResponse.KeyNotSet);

            await harness.Ssp.Invoking(d => d.EnableAsync())
                         .Should().ThrowAsync<SspResponseException>()
                         .Where(e => e.Response == SspResponse.KeyNotSet);
        }

        /// <summary>
        /// The protocol offers no way to ask a device which versions it supports — only to ask it
        /// to change, which it answers OK or FAIL.  So negotiation walks down from the highest
        /// version this host can decode until one is accepted.
        /// </summary>
        [Fact]
        public async Task NegotiationWalksDownUntilTheDeviceAcceptsAVersion()
        {
            using var harness = new Harness();
            harness.Device.Respond((byte)SspCommand.HostProtocolVersion,
                payload => payload[1] <= 7 ? Ok : Fail);

            var agreed = await harness.Ssp.NegotiateProtocolVersionAsync();

            agreed.Should().Be(new SspProtocolVersion(7));
            harness.Ssp.ProtocolVersion.Should().Be(new SspProtocolVersion(7));

            // 9, 8, then 7 -- it tried the highest first rather than settling for the lowest.
            harness.Device.ReceivedCommands.Select(c => c[1]).Should().Equal(9, 8, 7);
        }

        /// <summary>
        /// Never setting a device above what this host can decode is the point of the ceiling; a
        /// device left running ahead of its host sends events the host cannot measure, and the
        /// rest of every poll reply after one becomes unreadable.
        /// </summary>
        [Fact]
        public async Task NegotiationNeverGoesAboveTheHostsOwnCeiling()
        {
            using var harness = new Harness(new SspDeviceOptions { HighestProtocolVersion = 6 });
            harness.Device.Respond((byte)SspCommand.HostProtocolVersion, _ => Ok);

            var agreed = await harness.Ssp.NegotiateProtocolVersionAsync();

            agreed.Should().Be(new SspProtocolVersion(6));
            harness.Device.ReceivedCommands.Should().ContainSingle()
                   .Which.Should().Equal((byte)SspCommand.HostProtocolVersion, 6);
        }

        [Fact]
        public async Task ADeviceThatAcceptsNoVersionAtAllIsReported()
        {
            using var harness = new Harness();
            harness.Device.Respond((byte)SspCommand.HostProtocolVersion, _ => Fail);

            await harness.Ssp.Invoking(d => d.NegotiateProtocolVersionAsync())
                         .Should().ThrowAsync<SspResponseException>();
        }

        /// <summary>
        /// A refusal that is not FAIL means something other than "not that version", so stepping
        /// down will not help and the real reason should surface instead.
        /// </summary>
        [Fact]
        public async Task ARefusalOtherThanFailStopsTheNegotiationImmediately()
        {
            using var harness = new Harness();
            harness.Device.Respond((byte)SspCommand.HostProtocolVersion, (byte)SspResponse.KeyNotSet);

            await harness.Ssp.Invoking(d => d.NegotiateProtocolVersionAsync())
                         .Should().ThrowAsync<SspResponseException>()
                         .Where(e => e.Response == SspResponse.KeyNotSet);

            harness.Device.ReceivedCommands.Should().ContainSingle("it should not have kept trying");
        }

        /// <summary>
        /// The startup sequence from the implementation guide, in order: synchronise, settle the
        /// protocol version, then read the setup — the version first, because it decides how the
        /// setup reply is laid out.
        /// </summary>
        [Fact]
        public async Task ConnectingSynchronisesThenSettlesTheVersionThenReadsTheSetup()
        {
            using var harness = new Harness();
            harness.Device.Respond((byte)SspCommand.Sync, Ok);
            harness.Device.Respond((byte)SspCommand.HostProtocolVersion, payload => payload[1] <= 6 ? Ok : Fail);
            harness.Device.Respond((byte)SspCommand.SetupRequest, GuideSetupReply());

            var setup = await harness.Ssp.ConnectAsync();

            harness.Device.ReceivedCommands.Select(c => c[0]).Should().Equal(
                (byte)SspCommand.Sync,
                (byte)SspCommand.HostProtocolVersion,   // 9
                (byte)SspCommand.HostProtocolVersion,   // 8
                (byte)SspCommand.HostProtocolVersion,   // 7
                (byte)SspCommand.HostProtocolVersion,   // 6, accepted
                (byte)SspCommand.SetupRequest);

            setup.UnitType.Should().Be(SspUnitType.BanknoteValidator);
            setup.Channels.Select(c => c.Value).Should().Equal(5u, 10u, 20u, 50u);
            harness.Ssp.ProtocolVersion.Should().Be(new SspProtocolVersion(6));
            harness.Ssp.Setup.Should().BeSameAs(setup);
        }

        /// <summary>
        /// A poll reply is read at whatever version the device is set to, so connecting first is
        /// what makes a poll mean anything.  Here the same reply would be two events at version 4
        /// and one at version 9.
        /// </summary>
        [Fact]
        public async Task PollingReadsTheReplyAtTheNegotiatedVersion()
        {
            using var harness = new Harness();
            harness.Device.Respond((byte)SspCommand.HostProtocolVersion, _ => Ok);
            harness.Device.Respond((byte)SspCommand.Poll,
                (byte)SspResponse.OK, (byte)SspEvent.NoteCredit, 0x04, (byte)SspEvent.Stacked);

            var atFour = await harness.Ssp.PollAsync();
            atFour.Events.Select(e => e.Event).Should().Equal(SspEvent.NoteCredit, SspEvent.Stacked);

            await harness.Ssp.SetProtocolVersionAsync(9);

            var atNine = await harness.Ssp.PollAsync();
            atNine.IsComplete.Should().BeFalse("a version 9 credit wants seven data bytes and there are two");
        }

        /// <summary>
        /// A device newer than this library must not stop a host: the poll reports what it read
        /// and where it stopped rather than throwing away the credit that came before.
        /// </summary>
        [Fact]
        public async Task APollCarryingAnUnknownEventKeepsTheEventsBeforeIt()
        {
            using var harness = new Harness();
            harness.Device.Respond((byte)SspCommand.Poll,
                (byte)SspResponse.OK, (byte)SspEvent.NoteCredit, 0x02, 0x7C, 0x11);

            var result = await harness.Ssp.PollAsync();

            result.IsComplete.Should().BeFalse();
            result.StoppedAtCode.Should().Be(0x7C);
            result.Events.Should().ContainSingle()
                  .Which.Event.Should().Be(SspEvent.NoteCredit);
        }

        [Fact]
        public async Task AnEventTableSuppliedInOptionsIsWhatPollsAreReadWith()
        {
            var options = new SspDeviceOptions
            {
                EventTable = SspEventTable.Default.WithEvent(0x7C, SspProtocolVersion.Lowest, SspEventPayload.Fixed(1)),
            };

            using var harness = new Harness(options);
            harness.Device.Respond((byte)SspCommand.Poll, (byte)SspResponse.OK, 0x7C, 0x11);

            var result = await harness.Ssp.PollAsync();

            result.IsComplete.Should().BeTrue();
            result.Events.Should().ContainSingle().Which.Code.Should().Be(0x7C);
        }

        /// <summary>
        /// The manual's own example: channels 1 to 3 enabled and the rest inhibited is 07 00, so
        /// the lowest bit of the first byte is channel 1.
        /// </summary>
        [Fact]
        public async Task ChannelInhibitsGoOutLowestChannelFirst()
        {
            using var harness = new Harness();
            harness.Device.Respond((byte)SspCommand.SetChannelInhibits, Ok);

            await harness.Ssp.SetChannelInhibitsAsync(0b0000_0000_0000_0111);

            harness.Device.ReceivedCommands.Should().ContainSingle()
                   .Which.Should().Equal((byte)SspCommand.SetChannelInhibits, 0x07, 0x00);
        }

        [Fact]
        public async Task AllSixteenChannelsEnabledIsTwoFullBytes()
        {
            using var harness = new Harness();
            harness.Device.Respond((byte)SspCommand.SetChannelInhibits, Ok);

            await harness.Ssp.SetChannelInhibitsAsync(ushort.MaxValue);

            harness.Device.ReceivedCommands.Should().ContainSingle()
                   .Which.Should().Equal((byte)SspCommand.SetChannelInhibits, 0xFF, 0xFF);
        }

        /// <summary>
        /// Serial numbers are big endian while the amounts in event payloads are little endian;
        /// this is the manual's own worked example of the former.
        /// </summary>
        [Fact]
        public async Task TheSerialNumberIsReadBigEndian()
        {
            using var harness = new Harness();
            harness.Device.Respond((byte)SspCommand.GetSerialNumber,
                (byte)SspResponse.OK, 0x00, 0x1C, 0x96, 0x2C);

            (await harness.Ssp.GetSerialNumberAsync()).Should().Be(1873452);
        }

        /// <summary>
        /// A device zeroes its own sequence flag on SYNC, so the host has to zero its copy too or
        /// the next command carries a flag the device is not expecting.
        /// </summary>
        [Fact]
        public async Task SyncPutsTheSequenceFlagBackInStepAtBothEnds()
        {
            using var harness = new Harness();
            harness.Device.Respond((byte)SspCommand.Sync, Ok);
            harness.Device.Respond((byte)SspCommand.Poll, Ok);

            await harness.Ssp.PollAsync();   // flag advances to true
            await harness.Ssp.SyncAsync();   // sync itself, then reset
            await harness.Ssp.PollAsync();   // must start again from true

            harness.Device.ReceivedSequenceFlags.Should().Equal(true, true, true);
        }

        /// <summary>
        /// A device capability this library has not modelled must not be a dead end, so a raw code
        /// goes out and the reply comes back unexamined.
        /// </summary>
        [Fact]
        public async Task ACommandThisLibraryDoesNotNameCanStillBeSent()
        {
            using var harness = new Harness();
            harness.Device.Respond(0x7C, (byte)SspResponse.OK, 0xAB);

            var reply = await harness.Ssp.SendAsync(0x7C, new byte[] { 0x01 });

            reply.IsOk.Should().BeTrue();
            reply.Data.ToArray().Should().Equal(new byte[] { 0xAB });
            harness.Device.ReceivedCommands.Should().ContainSingle()
                   .Which.Should().Equal(0x7C, 0x01);
        }

        private static byte[] GuideSetupReply() => new byte[]
        {
            (byte)SspResponse.OK,
            0x00,
            0x30, 0x33, 0x33, 0x35,
            0x45, 0x55, 0x52,
            0x00, 0x00, 0x01,
            0x04,
            0x05, 0x0A, 0x14, 0x32,
            0x02, 0x02, 0x02, 0x02,
            0x00, 0x00, 0x64,
            0x06,
            0x45, 0x55, 0x52, 0x45, 0x55, 0x52, 0x45, 0x55, 0x52, 0x45, 0x55, 0x52,
            0x05, 0x00, 0x00, 0x00,
            0x0A, 0x00, 0x00, 0x00,
            0x14, 0x00, 0x00, 0x00,
            0x32, 0x00, 0x00, 0x00,
        };

        private sealed class Harness : IDisposable
        {
            private readonly SspLoopbackStream hostStream;
            private readonly SspLoopbackStream deviceStream;
            private readonly CancellationTokenSource cts = new CancellationTokenSource();
            private readonly Task deviceLoop;

            public Harness(SspDeviceOptions? options = null)
            {
                (hostStream, deviceStream) = SspLoopbackStream.CreatePair();
                Device = new SspDeviceSimulator(deviceStream);
                deviceLoop = Device.RunAsync(cts.Token);
                Ssp = SspDevice.Attach(hostStream, 0x00, options);
            }

            public SspDeviceSimulator Device { get; }

            public SspDevice Ssp { get; }

            public void Dispose()
            {
                cts.Cancel();
                try
                {
                    deviceLoop.Wait(TimeSpan.FromSeconds(5));
                }
                catch (AggregateException)
                {
                    // Cancellation on shutdown.
                }

                Ssp.Bus.Dispose();
                hostStream.Dispose();
                deviceStream.Dispose();
                cts.Dispose();
            }
        }
    }
}

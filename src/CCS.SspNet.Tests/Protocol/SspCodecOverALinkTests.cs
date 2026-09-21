using CCS.SspNet.Communication;
using CCS.SspNet.Exceptions;
using CCS.SspNet.Protocol;
using CCS.SspNet.Tests.Fakes;
using FluentAssertions;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace CCS.SspNet.Tests.Protocol
{
    /// <summary>
    /// Runs the codec over the real framing and link layers against a simulated device, so that
    /// building a command and reading the reply is exercised end to end rather than in pieces.
    /// </summary>
    [Trait(TestCategories.Name, TestCategories.Fast)]
    public class SspCodecOverALinkTests
    {
        [Fact]
        public async Task APollIsBuiltSentAndDecodedBackIntoEvents()
        {
            using var harness = new Harness();
            harness.Device.Respond(
                (byte)SspCommand.Poll,
                (byte)SspResponse.OK,
                (byte)SspEvent.NoteCredit, 0x04,
                (byte)SspEvent.Stacked);

            var packet = await harness.Link.ExchangeAsync(0x00, SspMessage.Create(SspCommand.Poll));
            var reply = SspReply.Parse(packet.Data);
            reply.EnsureOk();

            var result = SspPollDecoder.Decode(reply.Data.Span, version: 4);

            result.IsComplete.Should().BeTrue();
            result.Events.Select(e => e.Event).Should().Equal(SspEvent.NoteCredit, SspEvent.Stacked);
            result.Events[0].Data.ToArray().Should().Equal(new byte[] { 0x04 });
        }

        /// <summary>
        /// An event payload can contain the start-of-packet marker like any other data, so a poll
        /// reply has to survive byte stuffing the same way a command does.
        /// </summary>
        [Fact]
        public async Task AnEventPayloadContainingTheStartMarkerSurvivesTheWire()
        {
            using var harness = new Harness();
            harness.Device.Respond(
                (byte)SspCommand.Poll,
                (byte)SspResponse.OK,
                (byte)SspEvent.NoteCredit, Constants.STX);

            var packet = await harness.Link.ExchangeAsync(0x00, SspMessage.Create(SspCommand.Poll));
            var result = SspPollDecoder.Decode(SspReply.Parse(packet.Data).Data.Span, version: 4);

            result.IsComplete.Should().BeTrue();
            result.Events[0].Data.ToArray().Should().Equal(new byte[] { Constants.STX });
        }

        [Fact]
        public async Task ADeviceRefusingACommandSurfacesAsTheCodeItSent()
        {
            using var harness = new Harness();
            harness.Device.Respond((byte)SspCommand.Enable, (byte)SspResponse.KeyNotSet);

            var packet = await harness.Link.ExchangeAsync(0x00, SspMessage.Create(SspCommand.Enable));
            var reply = SspReply.Parse(packet.Data);

            reply.Invoking(r => r.EnsureOk())
                 .Should().Throw<SspResponseException>()
                 .Which.Response.Should().Be(SspResponse.KeyNotSet);
        }

        [Fact]
        public async Task ACommandsParametersReachTheDeviceUnchanged()
        {
            using var harness = new Harness();
            harness.Device.Respond((byte)SspCommand.HostProtocolVersion, (byte)SspResponse.OK);

            await harness.Link.ExchangeAsync(
                0x00, SspMessage.Create(SspCommand.HostProtocolVersion, 0x06));

            harness.Device.ReceivedCommands.Should().ContainSingle()
                   .Which.Should().Equal((byte)SspCommand.HostProtocolVersion, 0x06);
        }

        private sealed class Harness : IDisposable
        {
            private readonly SspLoopbackStream hostStream;
            private readonly SspLoopbackStream deviceStream;
            private readonly CancellationTokenSource cts = new CancellationTokenSource();
            private readonly Task deviceLoop;

            public Harness()
            {
                (hostStream, deviceStream) = SspLoopbackStream.CreatePair();
                Device = new SspDeviceSimulator(deviceStream);
                deviceLoop = Device.RunAsync(cts.Token);
                Link = new SspLink(hostStream);
            }

            public SspDeviceSimulator Device { get; }

            public SspLink Link { get; }

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

                Link.Dispose();
                hostStream.Dispose();
                deviceStream.Dispose();
                cts.Dispose();
            }
        }
    }
}

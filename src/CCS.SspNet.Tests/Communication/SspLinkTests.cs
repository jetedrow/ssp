using CCS.SspNet.Communication;
using CCS.SspNet.Exceptions;
using CCS.SspNet.Tests.Fakes;
using FluentAssertions;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace CCS.SspNet.Tests.Communication
{
    /// <summary>
    /// Covers the link layer against a simulated device: sequence flag handling, retries, and
    /// serialization of concurrent callers.
    /// </summary>
    [Trait(TestCategories.Name, TestCategories.Fast)]
    public class SspLinkTests
    {
        private const byte Sync = 0x11;
        private const byte Poll = 0x07;

        [Fact]
        public async Task ACommandGetsItsReply()
        {
            using var harness = new Harness();
            harness.Device.Respond(Sync, (byte)SspResponse.OK);

            var reply = await harness.Link.ExchangeAsync(0x00, new byte[] { Sync });

            reply.Data.Should().Equal((byte)SspResponse.OK);
            reply.Address.Should().Be(0x00);
        }

        [Fact]
        public async Task TheSequenceFlagAlternatesBetweenCommands()
        {
            // SSP's duplicate detection depends on the flag advancing for each new command.
            using var harness = new Harness();
            harness.Device.Respond(Poll, (byte)SspResponse.OK);

            await harness.Link.ExchangeAsync(0x00, new byte[] { Poll });
            await harness.Link.ExchangeAsync(0x00, new byte[] { Poll });
            await harness.Link.ExchangeAsync(0x00, new byte[] { Poll });

            harness.Device.ReceivedSequenceFlags.Should().Equal(true, false, true);
        }

        [Fact]
        public async Task ARetryRepeatsTheSequenceFlagRatherThanAdvancingIt()
        {
            // This is the point of the flag: a device seeing the same one twice knows it is a
            // retransmission and answers from its cache instead of acting on the command again.
            using var harness = new Harness(new SspLinkOptions
            {
                ResponseTimeout = TimeSpan.FromMilliseconds(150),
                MaxRetries = 2,
            });
            harness.Device.Respond(Sync, (byte)SspResponse.OK);
            harness.Device.DropNextReplies = 1;

            var reply = await harness.Link.ExchangeAsync(0x00, new byte[] { Sync });

            reply.Data.Should().Equal((byte)SspResponse.OK);
            harness.Device.ReceivedCommands.Should().HaveCount(2, "the first attempt drew no reply");
            harness.Device.ReceivedSequenceFlags.Should().Equal(true, true);
        }

        [Fact]
        public async Task ACorruptedReplyIsRetried()
        {
            using var harness = new Harness(new SspLinkOptions
            {
                ResponseTimeout = TimeSpan.FromMilliseconds(250),
                MaxRetries = 2,
            });
            harness.Device.Respond(Sync, (byte)SspResponse.OK);
            harness.Device.CorruptNextReplies = 1;

            var reply = await harness.Link.ExchangeAsync(0x00, new byte[] { Sync });

            reply.Data.Should().Equal((byte)SspResponse.OK);
            harness.Device.ReceivedCommands.Should().HaveCount(2);
        }

        [Fact]
        public async Task ASilentDeviceFailsOnceTheRetriesAreSpent()
        {
            using var harness = new Harness(new SspLinkOptions
            {
                ResponseTimeout = TimeSpan.FromMilliseconds(100),
                MaxRetries = 1,
            });
            harness.Device.Respond(Sync, (byte)SspResponse.OK);
            harness.Device.DropNextReplies = 5;

            await harness.Link.Awaiting(l => l.ExchangeAsync(0x00, new byte[] { Sync }))
                .Should().ThrowAsync<SspCommunicationException>()
                .WithMessage("*after 2 attempts*");
        }

        [Fact]
        public async Task ResetSequenceStartsTheFlagOverAsSyncDoes()
        {
            using var harness = new Harness();
            harness.Device.Respond(Poll, (byte)SspResponse.OK);

            await harness.Link.ExchangeAsync(0x00, new byte[] { Poll });
            harness.Link.ResetSequence(0x00);
            await harness.Link.ExchangeAsync(0x00, new byte[] { Poll });

            harness.Device.ReceivedSequenceFlags.Should().Equal(true, true);
        }

        [Fact]
        public async Task ConcurrentCallersAreSerialized()
        {
            // The bus carries one exchange at a time. Without the gate these would interleave and
            // each would read the other's reply.
            using var harness = new Harness();
            harness.Device.Respond(Poll, (byte)SspResponse.OK);

            var calls = new Task[8];
            for (var i = 0; i < calls.Length; i++)
            {
                calls[i] = harness.Link.ExchangeAsync(0x00, new byte[] { Poll });
            }

            await Task.WhenAll(calls);

            harness.Device.ReceivedCommands.Should().HaveCount(8);
            harness.Device.ReceivedSequenceFlags.Should().Equal(true, false, true, false, true, false, true, false);
        }

        [Fact]
        public async Task APayloadContainingStxReachesTheDeviceIntact()
        {
            using var harness = new Harness();
            var payload = new byte[] { 0x0B, Constants.STX, Constants.STX, 0x01 };
            harness.Device.Respond(0x0B, (byte)SspResponse.OK, Constants.STX, 0x02);

            var reply = await harness.Link.ExchangeAsync(0x00, payload);

            harness.Device.ReceivedCommands[0].Should().Equal(payload);
            reply.Data.Should().Equal((byte)SspResponse.OK, Constants.STX, 0x02);
        }

        /// <summary>Wires a link to a simulated device over a loopback pair and runs it.</summary>
        private sealed class Harness : IDisposable
        {
            private readonly SspLoopbackStream hostStream;
            private readonly SspLoopbackStream deviceStream;
            private readonly CancellationTokenSource cts = new CancellationTokenSource();
            private readonly Task deviceLoop;

            public Harness(SspLinkOptions? options = null)
            {
                (hostStream, deviceStream) = SspLoopbackStream.CreatePair();
                Device = new SspDeviceSimulator(deviceStream);
                deviceLoop = Device.RunAsync(cts.Token);
                Link = new SspLink(hostStream, options);
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

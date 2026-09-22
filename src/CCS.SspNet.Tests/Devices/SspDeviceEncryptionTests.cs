using CCS.SspNet.Exceptions;
using CCS.SspNet.Security;
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
    /// Drives the encrypted layer end to end: a real key exchange, then real AES over the real
    /// framing, against a device that checks everything a device checks.
    /// </summary>
    [Trait(TestCategories.Name, TestCategories.Fast)]
    public class SspDeviceEncryptionTests
    {
        private static readonly byte[] Ok = { (byte)SspResponse.OK };

        [Fact]
        public async Task BothEndsFinishTheExchangeHoldingTheSameKey()
        {
            using var harness = new Harness();

            var key = await harness.Ssp.NegotiateKeysAsync();

            harness.Ssp.IsEncrypted.Should().BeTrue();
            harness.Device.NegotiatedKey.Should().Be(key);
        }

        [Fact]
        public async Task TheKeyIsDifferentEverySession()
        {
            using var first = new Harness();
            using var second = new Harness();

            var one = await first.Ssp.NegotiateKeysAsync();
            var other = await second.Ssp.NegotiateKeysAsync();

            one.NegotiatedHalf.Should().NotBe(other.NegotiatedHalf);
        }

        /// <summary>
        /// The three numbers of the exchange go out in clear, and nothing before the key exists
        /// could have been encrypted anyway.  Everything after it is.
        /// </summary>
        [Fact]
        public async Task CommandsGoOutEncryptedOnceThereIsAKeyAndNotBefore()
        {
            using var harness = new Harness();
            harness.Device.Respond((byte)SspCommand.Enable, Ok);

            await harness.Ssp.EnableAsync();
            await harness.Ssp.NegotiateKeysAsync();
            await harness.Ssp.EnableAsync();

            harness.Device.ReceivedEncrypted.Should().Equal(false, false, false, false, true);
        }

        [Fact]
        public async Task TheDeviceReadsTheCommandThroughTheEncryption()
        {
            using var harness = new Harness();
            harness.Device.Respond((byte)SspCommand.SetChannelInhibits, Ok);

            await harness.Ssp.NegotiateKeysAsync();
            await harness.Ssp.SetChannelInhibitsAsync(0x0005);

            harness.Device.ReceivedCommands.Last().Should()
                   .Equal((byte)SspCommand.SetChannelInhibits, 0x05, 0x00);
        }

        [Fact]
        public async Task AnEncryptedReplyComesBackDecodedAsAnyOtherWould()
        {
            using var harness = new Harness();
            harness.Device.Respond((byte)SspCommand.Poll, (byte)SspResponse.OK, (byte)SspEvent.NoteCredit, 0x03);

            await harness.Ssp.NegotiateKeysAsync();
            var poll = await harness.Ssp.PollAsync();

            poll.Events.Should().ContainSingle()
                .Which.Event.Should().Be(SspEvent.NoteCredit);
        }

        /// <summary>
        /// A device with encryption fitted does not quietly ignore commands before a key exists —
        /// it answers every one of them KEY_NOT_SET, without doing what it was asked.
        /// </summary>
        [Fact]
        public async Task ADeviceThatDemandsAKeyRefusesEverythingUntilItHasOne()
        {
            using var harness = new Harness(requireKey: true);
            harness.Device.Respond((byte)SspCommand.Enable, Ok);

            await harness.Ssp.Invoking(d => d.EnableAsync())
                         .Should().ThrowAsync<SspResponseException>()
                         .Where(e => e.Response == SspResponse.KeyNotSet);

            await harness.Ssp.NegotiateKeysAsync();
            await harness.Ssp.EnableAsync();

            harness.Device.ReceivedEncrypted.Last().Should().BeTrue();
        }

        /// <summary>
        /// Whichever way the device reads the counter rule — a reply repeating the command's count
        /// or counting itself separately — the conversation has to keep working, because the
        /// documents describe both and a device only does one.
        /// </summary>
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task ALongConversationSurvivesEitherReadingOfTheCounterRule(bool countsReplySeparately)
        {
            using var harness = new Harness(countsReplySeparately: countsReplySeparately);
            harness.Device.Respond((byte)SspCommand.Poll, Ok);

            await harness.Ssp.NegotiateKeysAsync();

            for (var i = 0; i < 20; i++)
            {
                await harness.Ssp.PollAsync();
            }

            harness.Device.ReceivedEncrypted.Count(e => e).Should().Be(20);
        }

        /// <summary>
        /// A host that does not know the manufacturer's half of the key gets nothing: the device
        /// finds a block that will not decrypt and stops answering, which is what a device does
        /// when it decides it is being tampered with.
        /// </summary>
        [Fact]
        public async Task AHostWithTheWrongFixedKeyIsNotAnswered()
        {
            using var harness = new Harness();
            harness.Device.Respond((byte)SspCommand.Enable, Ok);

            await harness.Ssp.NegotiateKeysAsync(new SspEncryptionOptions { FixedKey = 0xDEADBEEFDEADBEEF });

            await harness.Ssp.Invoking(d => d.EnableAsync())
                         .Should().ThrowAsync<SspCommunicationException>();
        }

        /// <summary>
        /// Encryption is a decision made per device and per session, so dropping it leaves the
        /// device keyed and simply stops using it.
        /// </summary>
        [Fact]
        public async Task GivingUpEncryptionLeavesPlainCommandsWorking()
        {
            using var harness = new Harness();
            harness.Device.Respond((byte)SspCommand.Enable, Ok);

            await harness.Ssp.NegotiateKeysAsync();
            harness.Ssp.StopEncrypting();

            await harness.Ssp.EnableAsync();

            harness.Ssp.IsEncrypted.Should().BeFalse();
            harness.Device.ReceivedEncrypted.Last().Should().BeFalse();
        }

        [Fact]
        public async Task TheFixedKeyCannotBeChangedInClear()
        {
            using var harness = new Harness();

            await harness.Ssp.Invoking(d => d.SetFixedKeyAsync(0x1122334455667788))
                         .Should().ThrowAsync<InvalidOperationException>();
        }

        /// <summary>
        /// Changing the manufacturer's half ends the session: the two ends no longer share a key,
        /// so the only way on is to negotiate again with the new one.
        /// </summary>
        [Fact]
        public async Task ChangingTheFixedKeyEndsTheSession()
        {
            using var harness = new Harness();
            harness.Device.Respond((byte)SspCommand.SetFixedEncryptionKey, Ok);

            await harness.Ssp.NegotiateKeysAsync();
            await harness.Ssp.SetFixedKeyAsync(0x1122334455667788);

            harness.Ssp.IsEncrypted.Should().BeFalse();
        }

        /// <summary>
        /// Renegotiating is how a host gets back a conversation whose counter has drifted, so it
        /// has to work while a session is already running — which means the exchange goes in
        /// clear.  Sent through the old key it would strand the host: the device changes key as it
        /// answers the third command, and the reply to that command arrives under the new one.
        /// </summary>
        [Fact]
        public async Task RenegotiatingGoesInClearEvenWithASessionAlreadyRunning()
        {
            using var harness = new Harness();
            harness.Device.Respond((byte)SspCommand.Poll, Ok);

            await harness.Ssp.NegotiateKeysAsync();
            await harness.Ssp.PollAsync();

            var before = harness.Device.ReceivedEncrypted.Count;
            await harness.Ssp.NegotiateKeysAsync();
            await harness.Ssp.PollAsync();

            harness.Device.ReceivedEncrypted.Skip(before).Should()
                   .Equal(new[] { false, false, false, true }, "only the poll after the new key is encrypted");

            harness.Ssp.IsEncrypted.Should().BeTrue();
        }

        private sealed class Harness : IDisposable
        {
            private readonly SspLoopbackStream hostStream;
            private readonly SspLoopbackStream deviceStream;
            private readonly CancellationTokenSource cts = new CancellationTokenSource();
            private readonly Task deviceLoop;

            public Harness(bool requireKey = false, bool countsReplySeparately = false)
            {
                (hostStream, deviceStream) = SspLoopbackStream.CreatePair();

                Device = new SspDeviceSimulator(deviceStream)
                    .WithEncryption(
                        requireKey: requireKey,
                        countsReplySeparately: countsReplySeparately);

                deviceLoop = Device.RunAsync(cts.Token);

                // A device that has stopped answering should fail the test quickly rather than
                // spend three seconds proving it.
                Ssp = SspDevice.Attach(hostStream, 0x00, new SspDeviceOptions
                {
                    ResponseTimeout = TimeSpan.FromMilliseconds(200),
                    MaxRetries = 0,
                    Encryption = new SspEncryptionOptions { PrimeBits = 32 },
                });
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

using CCS.SspNet;
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
    /// Covers the multi-drop case: several devices answering on one stream.
    /// </summary>
    [Trait(TestCategories.Name, TestCategories.Fast)]
    public class SspBusTests
    {
        [Fact]
        public void AskingForTheSameAddressTwiceGivesTheSameDevice()
        {
            using var stream = SspLoopbackStream.CreatePair().Item1;
            using var bus = SspBus.Open(stream);

            bus.Device(0x10).Should().BeSameAs(bus.Device(0x10));
            bus.Device(0x10).Should().NotBeSameAs(bus.Device(0x00));
        }

        [Fact]
        public void AnAddressWiderThanSevenBitsIsRejected()
        {
            using var stream = SspLoopbackStream.CreatePair().Item1;
            using var bus = SspBus.Open(stream);

            bus.Invoking(b => b.Device(0x80)).Should().Throw<ArgumentOutOfRangeException>();
        }

        /// <summary>
        /// Each device on the bus has its own sequence flag, because the flag is how one device
        /// tells a retransmission from a new command.  A single flag shared across the bus would
        /// alternate on traffic to other addresses, so a device would see two consecutive commands
        /// carrying the same flag and answer the second from its cache without acting on it.
        /// </summary>
        [Fact]
        public async Task EachAddressKeepsItsOwnSequenceFlag()
        {
            var (hostStream, deviceStream) = SspLoopbackStream.CreatePair();
            using var cts = new CancellationTokenSource();

            var validatorAddress = (byte)0x00;
            var hopperAddress = (byte)0x10;

            var devices = new SspDeviceSimulator(deviceStream, validatorAddress, hopperAddress);
            devices.Respond((byte)SspCommand.Poll, (byte)SspResponse.OK);
            var loop = devices.RunAsync(cts.Token);

            using (var bus = SspBus.Open(hostStream))
            {
                // Two commands to the validator with one to the hopper in between.
                await bus.Device(validatorAddress).PollAsync();
                await bus.Device(hopperAddress).PollAsync();
                await bus.Device(validatorAddress).PollAsync();
            }

            FlagsSentTo(devices, validatorAddress).Should().Equal(new[] { true, false },
                "the validator's own flag alternated across its own two exchanges");
            FlagsSentTo(devices, hopperAddress).Should().Equal(new[] { true },
                "the hopper started from its own flag, not from wherever the validator had got to");

            cts.Cancel();
            await WaitForShutdown(loop);
            hostStream.Dispose();
            deviceStream.Dispose();
        }

        private static bool[] FlagsSentTo(SspDeviceSimulator devices, byte address) =>
            devices.ReceivedAddresses
                   .Select((a, i) => (Address: a, Flag: devices.ReceivedSequenceFlags[i]))
                   .Where(x => x.Address == address)
                   .Select(x => x.Flag)
                   .ToArray();

        private static async Task WaitForShutdown(Task deviceLoop)
        {
            try
            {
                await deviceLoop.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Expected on shutdown.
            }
        }
    }
}

using CCS.SspNet.Exceptions;
using CCS.SspNet.Firmware;
using CCS.SspNet.Interfaces;
using CCS.SspNet.Tests.Fakes;
using FluentAssertions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace CCS.SspNet.Tests.Firmware
{
    /// <summary>
    /// Drives a whole download end to end against a simulated device: the framed opening, the raw
    /// block transfer with its checksums, and the wait for the device to come back.
    /// </summary>
    [Trait(TestCategories.Name, TestCategories.Fast)]
    public class SspFirmwareDownloaderTests
    {
        private static readonly SspDownloadOptions Fast = new SspDownloadOptions
        {
            RamExecutionDelay = TimeSpan.Zero,
            RawResponseTimeout = TimeSpan.FromSeconds(2),
            RestartTimeout = TimeSpan.FromSeconds(5),
        };

        [Fact]
        public async Task TheDeviceReceivesEveryByteOfTheFileIntact()
        {
            var raw = SspFirmwareFileTests.Build(ramSize: 260, payloadSize: 5000, updateCode: 0x8C);
            var file = SspFirmwareFile.Parse(raw);

            using var harness = new Harness(file, blockSize: 4096);

            await SspFirmwareDownloader.DownloadAsync(harness.HostStream, file, Fast);
            await harness.WaitForDevice();

            harness.Device.RamReceived.Should().Equal(file.RamBlock.ToArray());
            harness.Device.PayloadReceived.Should().Equal(file.Payload.ToArray());
            harness.Device.ReceivedUpdateCode.Should().Be(0x8C);
            harness.Device.AllPayloadChecksumsMatched.Should().BeTrue();
            harness.Device.Completed.Should().BeTrue();
        }

        /// <summary>
        /// The header is sent twice — once framed to be accepted or rejected, once raw before the
        /// payload — and both times it is the file's first 128 bytes.
        /// </summary>
        [Fact]
        public async Task TheHeaderGoesToTheDeviceTwiceAndIsAlwaysTheFirst128Bytes()
        {
            var raw = SspFirmwareFileTests.Build(ramSize: 128, payloadSize: 256);
            var file = SspFirmwareFile.Parse(raw);

            using var harness = new Harness(file, blockSize: 256);

            await SspFirmwareDownloader.DownloadAsync(harness.HostStream, file, Fast);
            await harness.WaitForDevice();

            harness.Device.HeadersReceived.Should().HaveCount(2);
            harness.Device.HeadersReceived[0].Should().Equal(file.Header.ToArray());
            harness.Device.HeadersReceived[1].Should().Equal(file.Header.ToArray());
        }

        /// <summary>
        /// A payload that is not a whole number of blocks is sent as full blocks and then a short
        /// final block, and the device still ends up with every byte.
        /// </summary>
        [Fact]
        public async Task APayloadThatDoesNotDivideEvenlyStillArrivesWhole()
        {
            var raw = SspFirmwareFileTests.Build(ramSize: 64, payloadSize: 4096 + 17);
            var file = SspFirmwareFile.Parse(raw);

            using var harness = new Harness(file, blockSize: 4096);

            await SspFirmwareDownloader.DownloadAsync(harness.HostStream, file, Fast);
            await harness.WaitForDevice();

            harness.Device.PayloadReceived.Should().Equal(file.Payload.ToArray());
            harness.Device.AllPayloadChecksumsMatched.Should().BeTrue();
        }

        [Fact]
        public async Task ABlockSizeThatIsNotAMultipleOf128StillTransfersEveryByte()
        {
            var raw = SspFirmwareFileTests.Build(ramSize: 32, payloadSize: 1000);
            var file = SspFirmwareFile.Parse(raw);

            // 300 is not a multiple of the 128-byte section size, so the last section of each block
            // is short.
            using var harness = new Harness(file, blockSize: 300);

            await SspFirmwareDownloader.DownloadAsync(harness.HostStream, file, Fast);
            await harness.WaitForDevice();

            harness.Device.PayloadReceived.Should().Equal(file.Payload.ToArray());
        }

        /// <summary>
        /// A file for a different device is caught by the header exchange, before any of it has
        /// been written, so nothing has been overwritten when it fails.
        /// </summary>
        [Fact]
        public async Task AFileTheDeviceRejectsFailsBeforeAnythingIsWritten()
        {
            var raw = SspFirmwareFileTests.Build(ramSize: 64, payloadSize: 256);
            var file = SspFirmwareFile.Parse(raw);

            using var harness = new Harness(file, blockSize: 256, acceptHeader: false);

            await new Func<Task>(() => SspFirmwareDownloader.DownloadAsync(harness.HostStream, file, Fast))
                .Should().ThrowAsync<SspDownloadException>().WithMessage("*not a firmware or dataset for this device*");

            await harness.WaitForDevice();
            harness.Device.RamReceived.Should().BeEmpty();
            harness.Device.PayloadReceived.Should().BeEmpty();
        }

        [Fact]
        public async Task ProgressIsReportedFromStartToFinish()
        {
            var raw = SspFirmwareFileTests.Build(ramSize: 128, payloadSize: 4096);
            var file = SspFirmwareFile.Parse(raw);

            using var harness = new Harness(file, blockSize: 4096);
            var stages = new List<SspDownloadStage>();
            var lastFraction = 0.0;
            var progress = new Progress<SspDownloadProgress>(p =>
            {
                lock (stages) { stages.Add(p.Stage); lastFraction = p.Fraction; }
            });

            await SspFirmwareDownloader.DownloadAsync(harness.HostStream, file, Fast, progress);
            await harness.WaitForDevice();

            // Progress is raised on the captured context; give the callbacks a beat to drain.
            await Task.Delay(50);

            lock (stages)
            {
                stages.Should().Contain(SspDownloadStage.Synchronising);
                stages.Should().Contain(SspDownloadStage.SendingPayload);
                stages.Should().EndWith(SspDownloadStage.Complete);
            }

            lastFraction.Should().Be(1.0);
        }

        /// <summary>
        /// The device is not there the instant it resets; the download keeps syncing until it
        /// answers rather than giving up on the first silence.
        /// </summary>
        [Fact]
        public async Task TheDownloadWaitsOutTheDeviceRestart()
        {
            var raw = SspFirmwareFileTests.Build(ramSize: 64, payloadSize: 256);
            var file = SspFirmwareFile.Parse(raw);

            using var harness = new Harness(file, blockSize: 256, syncsToFailAfterReset: 3);

            await SspFirmwareDownloader.DownloadAsync(harness.HostStream, file, Fast);
            await harness.WaitForDevice();

            harness.Device.Completed.Should().BeTrue();
        }

        /// <summary>
        /// When the transport can change speed, the download raises it for the transfer and puts it
        /// back once the device has restarted.
        /// </summary>
        [Fact]
        public async Task TheLineSpeedIsRaisedForTheTransferAndRestoredAfter()
        {
            var raw = SspFirmwareFileTests.Build(ramSize: 64, payloadSize: 256);
            var file = SspFirmwareFile.Parse(raw);

            using var harness = new Harness(file, blockSize: 256);
            var baud = new RecordingBaud();

            var options = new SspDownloadOptions
            {
                RamExecutionDelay = TimeSpan.Zero,
                RawResponseTimeout = TimeSpan.FromSeconds(2),
                RestartTimeout = TimeSpan.FromSeconds(5),
                BaudRateControl = baud,
                TransferBaudRate = 38400,
                NormalBaudRate = 9600,
            };

            await SspFirmwareDownloader.DownloadAsync(harness.HostStream, file, options);
            await harness.WaitForDevice();

            baud.Rates.Should().Equal(38400, 9600);
            baud.DiscardCount.Should().BeGreaterThan(0);
        }

        private sealed class RecordingBaud : ISspBaudRateControl
        {
            private int rate = 9600;
            public List<int> Rates { get; } = new List<int>();
            public int DiscardCount { get; private set; }

            public int BaudRate
            {
                get => rate;
                set { rate = value; Rates.Add(value); }
            }

            public void DiscardBuffers() => DiscardCount++;
        }

        private sealed class Harness : IDisposable
        {
            private readonly SspLoopbackStream hostStream;
            private readonly SspLoopbackStream deviceStream;
            private readonly CancellationTokenSource cts = new CancellationTokenSource();
            private readonly Task deviceLoop;

            public Harness(SspFirmwareFile file, int blockSize, bool acceptHeader = true, int syncsToFailAfterReset = 0)
            {
                (hostStream, deviceStream) = SspLoopbackStream.CreatePair();
                Device = new SspDownloadTargetSimulator(deviceStream, file, blockSize, acceptHeader, syncsToFailAfterReset);
                deviceLoop = Device.RunAsync(cts.Token);
            }

            public SspLoopbackStream HostStream => hostStream;

            public SspDownloadTargetSimulator Device { get; }

            public async Task WaitForDevice()
            {
                var finished = await Task.WhenAny(deviceLoop, Task.Delay(TimeSpan.FromSeconds(10)));
                if (finished != deviceLoop) cts.Cancel();
            }

            public void Dispose()
            {
                cts.Cancel();
                try { deviceLoop.Wait(TimeSpan.FromSeconds(5)); } catch (AggregateException) { }
                hostStream.Dispose();
                deviceStream.Dispose();
                cts.Dispose();
            }
        }
    }
}

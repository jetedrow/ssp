using CCS.SspNet;
using CCS.SspNet.Hosting;
using CCS.SspNet.Protocol;
using CCS.SspNet.Tests.Fakes;
using FluentAssertions;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace CCS.SspNet.Tests.Hosting
{
    /// <summary>
    /// Drives the poll loop against a simulated device.
    /// </summary>
    [Trait(TestCategories.Name, TestCategories.Fast)]
    public class SspDeviceHostTests
    {
        private static readonly byte[] Ok = { (byte)SspResponse.OK };

        /// <summary>A poll reply saying a note of channel 3 is validated and held in escrow.</summary>
        private static readonly byte[] NoteInEscrow =
            { (byte)SspResponse.OK, (byte)SspEvent.Read, 0x03 };

        /// <summary>A poll reply saying a note is still being scanned.</summary>
        private static readonly byte[] NoteBeingScanned =
            { (byte)SspResponse.OK, (byte)SspEvent.Read, 0x00 };

        [Fact]
        public async Task EventsFromTheDeviceReachAHandler()
        {
            using var harness = new Harness();
            harness.Device.Respond((byte)SspCommand.Poll,
                (byte)SspResponse.OK, (byte)SspEvent.NoteCredit, 0x04, (byte)SspEvent.Stacked);

            var seen = new ConcurrentQueue<SspEvent>();
            harness.Host.EventReceived += (_, e) => seen.Enqueue(e.Event.Event);

            harness.Host.Start();
            await harness.WaitUntil(() => seen.Count >= 2);
            await harness.Host.StopAsync();

            seen.Should().Contain(SspEvent.NoteCredit).And.Contain(SspEvent.Stacked);
        }

        /// <summary>
        /// The behaviour that makes a poll loop dangerous if you do not know about it: the next
        /// poll takes the note, so a handler that does nothing has accepted it.
        /// </summary>
        [Fact]
        public async Task AHandlerThatDoesNothingLetsTheNextPollTakeTheNote()
        {
            using var harness = new Harness();
            harness.Device.Respond((byte)SspCommand.Poll, NoteInEscrow);

            var events = 0;
            harness.Host.EventReceived += (_, _) => Interlocked.Increment(ref events);

            harness.Host.Start();
            await harness.WaitUntil(() => Volatile.Read(ref events) >= 2);
            await harness.Host.StopAsync();

            harness.CommandsSent.Should().OnlyContain(c => c == (byte)SspCommand.Poll,
                "nothing was sent to stop the next poll taking the note");
        }

        /// <summary>
        /// A rejection is only a rejection if it beats the next poll to the device, so the order
        /// on the wire is the thing worth asserting — not merely that a reject was sent.
        /// </summary>
        [Fact]
        public async Task ARejectingHandlerGetsItsRejectOutBeforeTheNextPoll()
        {
            using var harness = new Harness();
            harness.Device.Respond((byte)SspCommand.Poll, NoteInEscrow);
            harness.Device.Respond((byte)SspCommand.RejectBanknote, Ok);

            harness.Host.EventReceived += (_, e) =>
            {
                if (e.IsNoteInEscrow) e.Escrow = SspEscrowAction.Reject;
            };

            harness.Host.Start();
            await harness.WaitUntil(() => harness.CommandsSent.Contains((byte)SspCommand.RejectBanknote));
            await harness.Host.StopAsync();

            var sent = harness.CommandsSent;
            var reject = sent.IndexOf((byte)SspCommand.RejectBanknote);

            reject.Should().Be(1, "the reject should be the very next thing after the poll that found the note");
            sent[0].Should().Be((byte)SspCommand.Poll);
        }

        [Fact]
        public async Task AHoldingHandlerGetsItsHoldOutBeforeTheNextPoll()
        {
            using var harness = new Harness();
            harness.Device.Respond((byte)SspCommand.Poll, NoteInEscrow);
            harness.Device.Respond((byte)SspCommand.Hold, Ok);

            harness.Host.EventReceived += (_, e) =>
            {
                if (e.IsNoteInEscrow) e.Escrow = SspEscrowAction.Hold;
            };

            harness.Host.Start();
            await harness.WaitUntil(() => harness.CommandsSent.Contains((byte)SspCommand.Hold));
            await harness.Host.StopAsync();

            harness.CommandsSent.IndexOf((byte)SspCommand.Hold).Should().Be(1);
        }

        /// <summary>
        /// A read event with an all-zero payload is the device saying it is still looking at the
        /// note, not that it wants a decision.  Treating it as an escrow would reject notes
        /// mid-scan.
        /// </summary>
        [Fact]
        public async Task ANoteStillBeingScannedIsNotAnEscrowDecision()
        {
            using var harness = new Harness();
            harness.Device.Respond((byte)SspCommand.Poll, NoteBeingScanned);

            var escrows = 0;
            var events = 0;
            harness.Host.EventReceived += (_, e) =>
            {
                Interlocked.Increment(ref events);
                if (e.IsNoteInEscrow) Interlocked.Increment(ref escrows);
            };

            harness.Host.Start();
            await harness.WaitUntil(() => Volatile.Read(ref events) >= 2);
            await harness.Host.StopAsync();

            Volatile.Read(ref escrows).Should().Be(0);
        }

        /// <summary>
        /// A loop that died on the first bad handler would lose every event after it, so the
        /// exception is reported and polling carries on.
        /// </summary>
        [Fact]
        public async Task AHandlerThatThrowsIsReportedAndTheLoopKeepsPolling()
        {
            using var harness = new Harness();
            harness.Device.Respond((byte)SspCommand.Poll, (byte)SspResponse.OK, (byte)SspEvent.Stacked);

            var faults = new ConcurrentQueue<string>();
            harness.Host.Fault += (_, e) => faults.Enqueue(e.ToString());
            harness.Host.EventReceived += (_, _) => throw new InvalidOperationException("handler blew up");

            harness.Host.Start();
            await harness.WaitUntil(() => faults.Count >= 2);
            await harness.Host.StopAsync();

            faults.Should().OnlyContain(f => f.Contains("handler blew up"));
            harness.CommandsSent.Count.Should().BeGreaterThan(1, "it kept polling");
        }

        /// <summary>
        /// A cable coming loose should not end the host; it should be visible and survivable.
        /// </summary>
        [Fact]
        public async Task APollThatFailsIsReportedAndTheLoopRecovers()
        {
            using var harness = new Harness(new SspDeviceOptions
            {
                ResponseTimeout = TimeSpan.FromMilliseconds(80),
                MaxRetries = 0,
            });
            harness.Device.Respond((byte)SspCommand.Poll, (byte)SspResponse.OK, (byte)SspEvent.Stacked);
            harness.Device.DropNextReplies = 1;

            var faults = 0;
            var events = 0;
            harness.Host.Fault += (_, _) => Interlocked.Increment(ref faults);
            harness.Host.EventReceived += (_, _) => Interlocked.Increment(ref events);

            harness.Host.Start();
            await harness.WaitUntil(() => Volatile.Read(ref faults) >= 1 && Volatile.Read(ref events) >= 1);
            await harness.Host.StopAsync();

            Volatile.Read(ref faults).Should().BeGreaterThan(0, "the dropped reply was reported");
            Volatile.Read(ref events).Should().BeGreaterThan(0, "and the loop went on to read a real event");
        }

        /// <summary>
        /// An event this library has no payload length for cuts the reply short.  The events read
        /// before it are real and are raised; the gap is reported separately rather than thrown.
        /// </summary>
        [Fact]
        public async Task AReplyThatStopsPartWayRaisesWhatItReadAndReportsTheGap()
        {
            using var harness = new Harness();
            harness.Device.Respond((byte)SspCommand.Poll,
                (byte)SspResponse.OK, (byte)SspEvent.Stacked, 0x7C, 0x11);

            var events = new ConcurrentQueue<SspEvent>();
            var stops = new ConcurrentQueue<byte?>();
            harness.Host.EventReceived += (_, e) => events.Enqueue(e.Event.Event);
            harness.Host.Fault += (_, e) => stops.Enqueue(e.Result?.StoppedAtCode);

            harness.Host.Start();
            await harness.WaitUntil(() => !events.IsEmpty && !stops.IsEmpty);
            await harness.Host.StopAsync();

            events.Should().Contain(SspEvent.Stacked);
            stops.Should().Contain((byte?)0x7C);
        }

        /// <summary>
        /// With acknowledgement on, the host polls with ack and acknowledges once its handlers
        /// have run — so a host that dies in between sees the credit again rather than losing it.
        /// </summary>
        [Fact]
        public async Task AcknowledgingEventsPollsWithAckAndAcknowledgesAfterTheHandlers()
        {
            using var harness = new Harness(hostOptions: new SspDeviceHostOptions { AcknowledgeEvents = true });
            harness.Device.Respond((byte)SspCommand.PollWithAck,
                (byte)SspResponse.OK, (byte)SspEvent.NoteCredit, 0x01);
            harness.Device.Respond((byte)SspCommand.EventAck, Ok);

            var handled = 0;
            harness.Host.EventReceived += (_, _) => Interlocked.Increment(ref handled);

            harness.Host.Start();
            await harness.WaitUntil(() => harness.CommandsSent.Contains((byte)SspCommand.EventAck));
            await harness.Host.StopAsync();

            var sent = harness.CommandsSent;
            sent[0].Should().Be((byte)SspCommand.PollWithAck);
            sent[1].Should().Be((byte)SspCommand.EventAck);
            Volatile.Read(ref handled).Should().BeGreaterThan(0, "the ack came after a handler had seen it");
        }

        /// <summary>
        /// An `await foreach` body runs between polls, so it can await a decision the synchronous
        /// handler could not — a database lookup, say — and still beat the poll that would have
        /// taken the note.
        /// </summary>
        [Fact]
        public async Task AnAwaitForeachBodyCanAwaitItsEscrowDecisionBeforeTheNextPoll()
        {
            using var harness = new Harness();
            harness.Device.Respond((byte)SspCommand.Poll, NoteInEscrow);
            harness.Device.Respond((byte)SspCommand.RejectBanknote, Ok);

            using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(10));

            await foreach (var e in harness.Host.ReadEventsAsync(stop.Token))
            {
                if (!e.IsNoteInEscrow) continue;

                // Something a synchronous handler could not do.
                await Task.Delay(20, stop.Token);
                await e.Device.RejectBanknoteAsync(stop.Token);
                break;
            }

            var sent = harness.CommandsSent;
            sent.IndexOf((byte)SspCommand.RejectBanknote).Should().Be(1,
                "the awaited reject still landed before a second poll went out");
        }

        [Fact]
        public async Task StoppingEndsThePolling()
        {
            using var harness = new Harness();
            harness.Device.Respond((byte)SspCommand.Poll, Ok);

            harness.Host.Start();
            await harness.WaitUntil(() => harness.CommandsSent.Count >= 2);
            await harness.Host.StopAsync();

            harness.Host.IsRunning.Should().BeFalse();

            var afterStop = harness.CommandsSent.Count;
            await Task.Delay(150);
            harness.CommandsSent.Count.Should().Be(afterStop);
        }

        [Fact]
        public void StartingTwiceIsRejected()
        {
            using var harness = new Harness();
            harness.Device.Respond((byte)SspCommand.Poll, Ok);

            harness.Host.Start();

            harness.Host.Invoking(h => h.Start()).Should().Throw<InvalidOperationException>();
        }

        /// <summary>
        /// A poll interval at or beyond the device's own escrow timeout would guarantee that every
        /// note held gets rejected, so it is refused rather than accepted and regretted.
        /// </summary>
        [Theory]
        [InlineData(10)]
        [InlineData(30)]
        public void APollIntervalAtOrBeyondTheEscrowTimeoutIsRejected(int seconds)
        {
            var options = new SspDeviceHostOptions();

            options.Invoking(o => o.PollInterval = TimeSpan.FromSeconds(seconds))
                   .Should().Throw<ArgumentOutOfRangeException>()
                   .WithMessage("*10 seconds*");
        }

        [Fact]
        public void ANegativePollIntervalIsRejected()
        {
            var options = new SspDeviceHostOptions();

            options.Invoking(o => o.PollInterval = TimeSpan.FromMilliseconds(-1))
                   .Should().Throw<ArgumentOutOfRangeException>();
        }

        private sealed class Harness : IDisposable
        {
            private readonly SspLoopbackStream hostStream;
            private readonly SspLoopbackStream deviceStream;
            private readonly CancellationTokenSource cts = new CancellationTokenSource();
            private readonly Task deviceLoop;

            public Harness(SspDeviceOptions? options = null, SspDeviceHostOptions? hostOptions = null)
            {
                (hostStream, deviceStream) = SspLoopbackStream.CreatePair();
                Device = new SspDeviceSimulator(deviceStream);
                deviceLoop = Device.RunAsync(cts.Token);

                Ssp = SspDevice.Attach(hostStream, 0x00, options);
                Host = new SspDeviceHost(Ssp, hostOptions ?? new SspDeviceHostOptions
                {
                    PollInterval = TimeSpan.FromMilliseconds(10),
                });
            }

            public SspDeviceSimulator Device { get; }

            public SspDevice Ssp { get; }

            public SspDeviceHost Host { get; }

            /// <summary>Gets the command code of everything sent so far, in order.</summary>
            public List<byte> CommandsSent =>
                Device.ReceivedCommands.Where(c => c.Length > 0).Select(c => c[0]).ToList();

            public async Task WaitUntil(Func<bool> condition, int timeoutMs = 10_000)
            {
                var deadline = Environment.TickCount + timeoutMs;

                while (!condition())
                {
                    if (Environment.TickCount > deadline)
                    {
                        throw new TimeoutException(
                            $"The condition was still false after {timeoutMs} ms. " +
                            $"Commands sent: {string.Join(", ", CommandsSent.Select(c => $"0x{c:X2}"))}");
                    }

                    await Task.Delay(5).ConfigureAwait(false);
                }
            }

            public void Dispose()
            {
                Host.Dispose();
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

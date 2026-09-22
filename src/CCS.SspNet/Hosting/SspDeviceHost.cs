using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using CCS.SspNet.Protocol;

namespace CCS.SspNet.Hosting
{
    /// <summary>
    /// Polls a device in the background and reports what it says.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the event-driven half of the library.  It is a loop over
    /// <see cref="SspDevice.PollAsync(CancellationToken)"/>, which is why the same commands stay available while it
    /// runs: the bus serialises exchanges, so a command issued from a handler queues between polls
    /// rather than racing the loop.
    /// </para>
    /// <para>
    /// There are two ways to consume it, and they are not interchangeable.
    /// <see cref="EventReceived"/> is a plain .NET event, raised on the loop's own thread, for
    /// handlers that decide without waiting for anything.
    /// <see cref="ReadEventsAsync"/> is an <c>await foreach</c> that polls inline, so the loop body
    /// runs between polls and can await whatever it likes before the next one goes out.
    /// </para>
    /// <para>
    /// That distinction matters because of escrow.  A note the validator is holding is taken by the
    /// <em>next poll</em>, so a handler has one interval to say otherwise and doing nothing
    /// accepts.  A synchronous handler sets <see cref="SspDeviceEventArgs.Escrow"/>; an
    /// <c>await foreach</c> body can go and ask a database first.
    /// </para>
    /// </remarks>
    public sealed class SspDeviceHost : IDisposable
    {
        private readonly SspDevice device;
        private readonly SspDeviceHostOptions options;
        private readonly object gate = new object();
        private CancellationTokenSource? running;
        private Task? loop;
        private bool disposed;

        /// <summary>Initializes a new host for a device.</summary>
        /// <param name="device">The device to poll.</param>
        /// <param name="options">How to run the loop, or <see langword="null"/> for the defaults.</param>
        public SspDeviceHost(SspDevice device, SspDeviceHostOptions? options = null)
        {
            this.device = device ?? throw new ArgumentNullException(nameof(device));
            this.options = options ?? new SspDeviceHostOptions();
        }

        /// <summary>
        /// Raised for each event the device reports, on the poll loop's own thread.
        /// </summary>
        /// <remarks>
        /// Handlers run one after another, and the next poll waits for them all.  A handler that
        /// blocks for longer than
        /// <see cref="SspDeviceHostOptions.EscrowTimeout"/> will cost the device any note it was
        /// holding, so do the slow part elsewhere — or use
        /// <see cref="ReadEventsAsync"/>, where waiting is the normal thing to do.
        /// </remarks>
        public event EventHandler<SspDeviceEventArgs>? EventReceived;

        /// <summary>
        /// Raised when a poll fails, or when its reply could not be read to the end.
        /// </summary>
        /// <remarks>
        /// The loop keeps going either way.  A cable that comes loose should not end the host, and
        /// an event this library has no payload length for should not either — the events read
        /// before it have already been raised.
        /// </remarks>
        public event EventHandler<SspPollFaultEventArgs>? Fault;

        /// <summary>Gets the device being polled.</summary>
        public SspDevice Device => device;

        /// <summary>Gets a value indicating whether the poll loop is running.</summary>
        public bool IsRunning
        {
            get
            {
                lock (gate) return loop != null && !loop.IsCompleted;
            }
        }

        /// <summary>
        /// Starts polling in the background.
        /// </summary>
        /// <param name="cancellationToken">Stops the loop when cancelled.</param>
        /// <exception cref="InvalidOperationException">The host is already running.</exception>
        public void Start(CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            lock (gate)
            {
                if (loop != null && !loop.IsCompleted)
                {
                    throw new InvalidOperationException("This host is already polling.");
                }

                running = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                loop = Task.Run(() => RunAsync(running.Token));
            }
        }

        /// <summary>
        /// Stops polling and waits for the loop to finish, including any handler still running.
        /// </summary>
        public async Task StopAsync()
        {
            Task? pending;
            CancellationTokenSource? cts;

            lock (gate)
            {
                pending = loop;
                cts = running;
                loop = null;
                running = null;
            }

            if (cts == null) return;

            cts.Cancel();

            if (pending != null)
            {
                try
                {
                    await pending.ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    // Stopping is not a failure.
                }
            }

            cts.Dispose();
        }

        /// <summary>
        /// Polls the device and yields each event, letting the loop body act before the next poll.
        /// </summary>
        /// <param name="cancellationToken">Stops the enumeration.</param>
        /// <remarks>
        /// <para>
        /// The poll happens inside the enumeration rather than on a background thread, so the body
        /// of an <c>await foreach</c> runs in the gap between one poll and the next.  That is what
        /// makes an awaited escrow decision possible: call
        /// <see cref="SspDevice.HoldAsync"/> or <see cref="SspDevice.RejectBanknoteAsync"/> from
        /// the body and it reaches the device before the poll that would have taken the note.
        /// </para>
        /// <para>
        /// A body that takes longer than <see cref="SspDeviceHostOptions.EscrowTimeout"/> costs the
        /// device any note it was holding, exactly as a slow event handler would.
        /// </para>
        /// <para>
        /// A poll that fails raises <see cref="Fault"/> and the enumeration continues, so the
        /// sequence ends only on cancellation.
        /// </para>
        /// </remarks>
        public async IAsyncEnumerable<SspDeviceEventArgs> ReadEventsAsync(
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            while (!cancellationToken.IsCancellationRequested)
            {
                var poll = await PollOnceAsync(cancellationToken).ConfigureAwait(false);

                if (poll != null)
                {
                    foreach (var pollEvent in poll.Events)
                    {
                        yield return new SspDeviceEventArgs(device, pollEvent);
                    }

                    if (options.AcknowledgeEvents && poll.Events.Count > 0)
                    {
                        await AcknowledgeAsync(cancellationToken).ConfigureAwait(false);
                    }
                }

                await DelayAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        private async Task RunAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var poll = await PollOnceAsync(cancellationToken).ConfigureAwait(false);

                if (poll != null)
                {
                    await RaiseAsync(poll, cancellationToken).ConfigureAwait(false);

                    if (options.AcknowledgeEvents && poll.Events.Count > 0)
                    {
                        await AcknowledgeAsync(cancellationToken).ConfigureAwait(false);
                    }
                }

                await DelayAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Polls once, turning anything that goes wrong into a fault rather than an end to the loop.
        /// </summary>
        /// <returns>The result, or <see langword="null"/> if the poll failed.</returns>
        private async Task<SspPollResult?> PollOnceAsync(CancellationToken cancellationToken)
        {
            SspPollResult result;

            try
            {
                result = options.AcknowledgeEvents
                    ? await device.PollWithAckAsync(cancellationToken).ConfigureAwait(false)
                    : await device.PollAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                OnFault(new SspPollFaultEventArgs(device, ex, null));
                return null;
            }

            // A reply that stopped part-way still carries the events read before it, so those are
            // reported as usual and the gap is reported separately.
            if (!result.IsComplete)
            {
                OnFault(new SspPollFaultEventArgs(device, null, result));
            }

            return result;
        }

        private async Task RaiseAsync(SspPollResult poll, CancellationToken cancellationToken)
        {
            foreach (var pollEvent in poll.Events)
            {
                var args = new SspDeviceEventArgs(device, pollEvent);

                try
                {
                    EventReceived?.Invoke(this, args);
                }
                catch (Exception ex)
                {
                    // A handler that throws must not take the loop down with it, and must not
                    // silently decide the fate of a note either -- so the event still gets its
                    // default treatment and the exception is reported.
                    OnFault(new SspPollFaultEventArgs(device, ex, null));
                }

                await ApplyEscrowAsync(args, cancellationToken).ConfigureAwait(false);
            }
        }

        private async Task ApplyEscrowAsync(SspDeviceEventArgs args, CancellationToken cancellationToken)
        {
            if (!args.IsNoteInEscrow || args.Escrow == SspEscrowAction.Accept) return;

            try
            {
                if (args.Escrow == SspEscrowAction.Hold)
                {
                    await device.HoldAsync(cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    await device.RejectBanknoteAsync(cancellationToken).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                OnFault(new SspPollFaultEventArgs(device, ex, null));
            }
        }

        private async Task AcknowledgeAsync(CancellationToken cancellationToken)
        {
            try
            {
                await device.EventAcknowledgeAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                OnFault(new SspPollFaultEventArgs(device, ex, null));
            }
        }

        private async Task DelayAsync(CancellationToken cancellationToken)
        {
            if (options.PollInterval <= TimeSpan.Zero) return;

            try
            {
                await Task.Delay(options.PollInterval, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Stopping mid-wait is the ordinary way out.
            }
        }

        private void OnFault(SspPollFaultEventArgs args)
        {
            try
            {
                Fault?.Invoke(this, args);
            }
            catch
            {
                // A fault handler that throws has nowhere left to report to.
            }
        }

        private void ThrowIfDisposed()
        {
            if (disposed) throw new ObjectDisposedException(nameof(SspDeviceHost));
        }

        /// <summary>
        /// Stops polling.  The device and its bus are not owned by the host and are left open.
        /// </summary>
        /// <remarks>
        /// This does not wait for a handler that is still running; <see cref="StopAsync"/> does.
        /// </remarks>
        public void Dispose()
        {
            if (disposed) return;
            disposed = true;

            CancellationTokenSource? cts;

            lock (gate)
            {
                cts = running;
                running = null;
                loop = null;
            }

            if (cts == null) return;

            cts.Cancel();
            cts.Dispose();
        }
    }
}

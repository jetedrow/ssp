using System;
using CCS.SspNet.Protocol;

namespace CCS.SspNet.Hosting
{
    /// <summary>
    /// Something went wrong during a poll, without the loop stopping.
    /// </summary>
    /// <remarks>
    /// A poll loop that died on the first cable glitch would be worse than useless, so the host
    /// keeps polling and reports faults here instead.  A host that wants to give up can call
    /// <see cref="SspDeviceHost.StopAsync"/> from the handler.
    /// </remarks>
    public sealed class SspPollFaultEventArgs : EventArgs
    {
        internal SspPollFaultEventArgs(SspDevice device, Exception? exception, SspPollResult? result)
        {
            Device = device;
            Exception = exception;
            Result = result;
        }

        /// <summary>Gets the device being polled.</summary>
        public SspDevice Device { get; }

        /// <summary>
        /// Gets what went wrong, or <see langword="null"/> when the poll itself succeeded but its
        /// reply could not be read to the end — see <see cref="Result"/>.
        /// </summary>
        public Exception? Exception { get; }

        /// <summary>
        /// Gets the partial result, when the poll succeeded but stopped at an event the library
        /// has no payload length for.  <see langword="null"/> when the poll itself failed.
        /// </summary>
        /// <remarks>
        /// The events read before that point are in here and have already been raised, so this is
        /// for reporting the gap rather than recovering the events.
        /// </remarks>
        public SspPollResult? Result { get; }

        /// <inheritdoc />
        public override string ToString() =>
            Exception?.Message ?? Result?.StopReason ?? "unknown poll fault";
    }
}

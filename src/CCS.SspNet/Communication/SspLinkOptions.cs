using System;

namespace CCS.SspNet.Communication
{
    /// <summary>
    /// Timing and retry behaviour for an <see cref="SspLink"/>.
    /// </summary>
    internal sealed class SspLinkOptions
    {
        /// <summary>
        /// Gets or sets how long to wait for a device's reply before treating the exchange as lost
        /// and retrying.
        /// </summary>
        public TimeSpan ResponseTimeout { get; set; } = TimeSpan.FromSeconds(1);

        /// <summary>
        /// Gets or sets how many times to resend a command that drew no usable reply.  Zero means
        /// send once and give up.
        /// </summary>
        /// <remarks>
        /// A retry repeats the original sequence flag rather than advancing it, which is what lets
        /// the device tell a retransmission from a new command and reply from its cache instead of
        /// acting twice.
        /// </remarks>
        public int MaxRetries { get; set; } = 2;
    }
}

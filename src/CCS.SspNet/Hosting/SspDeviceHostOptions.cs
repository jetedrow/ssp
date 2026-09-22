using System;

namespace CCS.SspNet.Hosting
{
    /// <summary>
    /// How an <see cref="SspDeviceHost"/> runs its poll loop.
    /// </summary>
    public sealed class SspDeviceHostOptions
    {
        /// <summary>
        /// The longest a validator will wait between polls before rejecting a note it is holding
        /// in escrow.
        /// </summary>
        public static readonly TimeSpan EscrowTimeout = TimeSpan.FromSeconds(10);

        private TimeSpan pollInterval = TimeSpan.FromMilliseconds(200);

        /// <summary>
        /// Gets or sets how long to wait between polls.  Defaults to 200 milliseconds.
        /// </summary>
        /// <remarks>
        /// This is the delay after one poll's handlers have run, not a fixed cadence, so a slow
        /// handler lengthens the gap rather than queueing polls behind it.  That matters because
        /// the gap is what the device measures: leave more than
        /// <see cref="EscrowTimeout"/> between polls and it rejects any note it was holding.
        /// </remarks>
        /// <exception cref="ArgumentOutOfRangeException">
        /// The interval is negative, or at or beyond <see cref="EscrowTimeout"/>.
        /// </exception>
        public TimeSpan PollInterval
        {
            get => pollInterval;
            set
            {
                if (value < TimeSpan.Zero)
                {
                    throw new ArgumentOutOfRangeException(nameof(value), value, "A poll interval cannot be negative.");
                }

                if (value >= EscrowTimeout)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(value), value,
                        $"A validator rejects an escrowed note after {EscrowTimeout.TotalSeconds:0} seconds " +
                        "without a poll, so the interval has to be shorter than that.");
                }

                pollInterval = value;
            }
        }

        /// <summary>
        /// Gets or sets whether to poll in a way that makes the device repeat each event until it
        /// is acknowledged.  Off by default.
        /// </summary>
        /// <remarks>
        /// With this on the host polls with acknowledgement and acknowledges only once every
        /// handler for that poll has returned without throwing.  A host that dies between reading
        /// a credit and recording it therefore sees the credit again on restart instead of losing
        /// it.  The cost is a second command per poll, and it only covers the events the protocol
        /// supports it for.
        /// </remarks>
        public bool AcknowledgeEvents { get; set; }
    }
}

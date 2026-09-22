using System;
using CCS.SspNet.Protocol;
using CCS.SspNet.Security;

namespace CCS.SspNet
{
    /// <summary>
    /// Timing, retry and decoding settings for a bus and the devices on it.
    /// </summary>
    public sealed class SspDeviceOptions
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
        /// A retry repeats the original sequence flag rather than advancing it, so a device can
        /// tell a retransmission from a new command and reply from its cache instead of acting
        /// twice.  That matters most on a payout: a retried command that the device treats as new
        /// pays out again.
        /// </remarks>
        public int MaxRetries { get; set; } = 2;

        /// <summary>
        /// Gets or sets the event payload lengths to decode poll replies with.  Defaults to
        /// <see cref="SspEventTable.Default"/>.
        /// </summary>
        /// <remarks>
        /// Replace this to support a device newer than this library — see
        /// <see cref="SspEventTable.WithEvent"/>.
        /// </remarks>
        public SspEventTable EventTable { get; set; } = SspEventTable.Default;

        /// <summary>
        /// Gets or sets the highest protocol version this host is prepared to decode.  Defaults to
        /// the highest the event table describes.
        /// </summary>
        /// <remarks>
        /// <see cref="SspDevice.NegotiateProtocolVersionAsync"/> never sets a device above this.
        /// Lower it if your own code only handles the events of an earlier version; raising it
        /// above what <see cref="EventTable"/> covers means poll replies may stop part-way.
        /// </remarks>
        public SspProtocolVersion HighestProtocolVersion { get; set; } = SspProtocolVersion.Highest;

        /// <summary>
        /// Gets or sets what <see cref="SspDevice.NegotiateKeysAsync"/> uses when it is not given
        /// settings of its own.
        /// </summary>
        /// <remarks>
        /// Encryption is not switched on by having these: nothing is encrypted until a key has
        /// been negotiated.
        /// </remarks>
        public SspEncryptionOptions Encryption { get; set; } = new SspEncryptionOptions();
    }
}

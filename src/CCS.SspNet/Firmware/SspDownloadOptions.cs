using System;
using CCS.SspNet.Interfaces;

namespace CCS.SspNet.Firmware
{
    /// <summary>
    /// Settings for a firmware or dataset download.
    /// </summary>
    /// <remarks>
    /// The defaults follow the speeds and waits in ITL's implementation guide.  The one thing worth
    /// setting is <see cref="BaudRateControl"/>, on a real serial device: without it the transfer
    /// runs at the connection's existing speed rather than the faster download speed the device
    /// expects, which a real device may not follow.
    /// </remarks>
    public sealed class SspDownloadOptions
    {
        /// <summary>
        /// Gets or sets the transport's speed control, so the download can raise the line speed for
        /// the transfer and set it back afterwards.  <see langword="null"/> leaves the speed alone.
        /// </summary>
        public ISspBaudRateControl? BaudRateControl { get; set; }

        /// <summary>
        /// Gets or sets the speed to run the transfer at.  Defaults to 38400, the speed the guide
        /// uses.  Ignored when there is no <see cref="BaudRateControl"/>.
        /// </summary>
        public int TransferBaudRate { get; set; } = 38400;

        /// <summary>
        /// Gets or sets the speed to return to once the device has restarted.  Defaults to 9600,
        /// the speed every SSP device comes back at.  Ignored when there is no
        /// <see cref="BaudRateControl"/>.
        /// </summary>
        public int NormalBaudRate { get; set; } = 9600;

        /// <summary>
        /// Gets or sets how long to wait for the device to run the RAM program before sending the
        /// payload.  Defaults to 2.5 seconds, the guide's figure.
        /// </summary>
        public TimeSpan RamExecutionDelay { get; set; } = TimeSpan.FromSeconds(2.5);

        /// <summary>
        /// Gets or sets how long to wait for a single raw acknowledgement or checksum byte during
        /// the transfer.  Defaults to 5 seconds.
        /// </summary>
        public TimeSpan RawResponseTimeout { get; set; } = TimeSpan.FromSeconds(5);

        /// <summary>
        /// Gets or sets how long to keep trying to reach the device after it restarts before giving
        /// up.  Defaults to 30 seconds.
        /// </summary>
        public TimeSpan RestartTimeout { get; set; } = TimeSpan.FromSeconds(30);
    }
}

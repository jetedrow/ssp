using System;
using System.IO.Ports;
using System.Threading;

namespace CCS.SspNet.Serial
{
    /// <summary>
    /// Wire settings for an SSP serial link.  The defaults are the settings SSP devices ship with,
    /// so most callers never need to change anything but <see cref="BaudRate"/>.
    /// </summary>
    public sealed class SspSerialPortOptions
    {
        /// <summary>
        /// Gets or sets the baud rate.  Defaults to 9600, the rate every SSP device supports.
        /// Some devices negotiate higher rates; on a multi-drop bus use the lowest rate common to
        /// every device on the port.
        /// </summary>
        public int BaudRate { get; set; } = 9600;

        /// <summary>Gets or sets the data bits per byte.  SSP uses 8.</summary>
        public int DataBits { get; set; } = 8;

        /// <summary>Gets or sets the parity.  SSP uses none.</summary>
        public Parity Parity { get; set; } = Parity.None;

        /// <summary>Gets or sets the stop bits.  SSP uses two.</summary>
        public StopBits StopBits { get; set; } = StopBits.Two;

        /// <summary>Gets or sets the handshake.  SSP uses none.</summary>
        public Handshake Handshake { get; set; } = Handshake.None;

        /// <summary>
        /// Gets or sets how long a read blocks before timing out.  <see cref="Timeout.InfiniteTimeSpan"/>
        /// blocks forever.
        /// </summary>
        public TimeSpan ReadTimeout { get; set; } = TimeSpan.FromMilliseconds(500);

        /// <summary>Gets or sets how long a write blocks before timing out.</summary>
        public TimeSpan WriteTimeout { get; set; } = TimeSpan.FromMilliseconds(500);

        internal void ApplyTo(SerialPort port)
        {
            port.BaudRate = BaudRate;
            port.DataBits = DataBits;
            port.Parity = Parity;
            port.StopBits = StopBits;
            port.Handshake = Handshake;
            port.ReadTimeout = ToMilliseconds(ReadTimeout);
            port.WriteTimeout = ToMilliseconds(WriteTimeout);
        }

        private static int ToMilliseconds(TimeSpan value)
        {
            if (value == Timeout.InfiniteTimeSpan) return SerialPort.InfiniteTimeout;

            var milliseconds = value.TotalMilliseconds;
            if (milliseconds <= 0 || milliseconds > int.MaxValue)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(value),
                    value,
                    "Timeout must be positive, or Timeout.InfiniteTimeSpan to wait indefinitely.");
            }

            return (int)milliseconds;
        }
    }
}

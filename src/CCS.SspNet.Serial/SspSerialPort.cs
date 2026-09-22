using System;
using System.IO;
using System.IO.Ports;
using CCS.SspNet.Interfaces;

namespace CCS.SspNet.Serial
{
    /// <summary>
    /// An SSP link over a serial port.
    /// </summary>
    /// <remarks>
    /// The library itself only ever talks to a <see cref="System.IO.Stream"/>; this type exists to
    /// open a serial port with SSP's wire settings and hand back that stream.  Any other transport
    /// — network, I2C, SPI, an in-memory stream in a test — needs no equivalent, because a
    /// <see cref="System.IO.Stream"/> is all that is required.
    /// </remarks>
    public sealed class SspSerialPort : IDisposable, ISspBaudRateControl
    {
        private readonly SerialPort port;
        private bool disposed;

        private SspSerialPort(SerialPort port)
        {
            this.port = port;
        }

        /// <summary>Gets the names of the serial ports present on this machine.</summary>
        public static string[] GetPortNames() => SerialPort.GetPortNames();

        /// <summary>
        /// Opens <paramref name="portName"/> with SSP's wire settings.
        /// </summary>
        /// <param name="portName">The port to open, for example <c>COM3</c> or <c>/dev/ttyUSB0</c>.</param>
        /// <param name="options">Wire settings, or <see langword="null"/> for the SSP defaults.</param>
        /// <returns>An open link.  Dispose it to close the port.</returns>
        public static SspSerialPort Open(string portName, SspSerialPortOptions? options = null)
        {
            if (string.IsNullOrWhiteSpace(portName)) throw new ArgumentException("Port name is required.", nameof(portName));

            var port = new SerialPort(portName);
            try
            {
                (options ?? new SspSerialPortOptions()).ApplyTo(port);
                port.Open();
            }
            catch
            {
                port.Dispose();
                throw;
            }

            return new SspSerialPort(port);
        }

        /// <summary>Gets the name of the open port.</summary>
        public string PortName => port.PortName;

        /// <summary>Gets a value indicating whether the port is open.</summary>
        public bool IsOpen => !disposed && port.IsOpen;

        /// <summary>
        /// Gets the stream to hand to the library.  The stream is owned by this instance; disposing
        /// the stream directly does not close the port, and disposing this instance does.
        /// </summary>
        public Stream Stream
        {
            get
            {
                if (disposed) throw new ObjectDisposedException(nameof(SspSerialPort));
                return port.BaseStream;
            }
        }

        /// <summary>Discards anything sitting in the driver's receive and transmit buffers.</summary>
        public void DiscardBuffers()
        {
            if (disposed) throw new ObjectDisposedException(nameof(SspSerialPort));
            port.DiscardInBuffer();
            port.DiscardOutBuffer();
        }

        /// <summary>
        /// Gets or sets the port's line speed.  A firmware download raises this for the transfer
        /// and puts it back afterwards; ordinary use leaves it at what the port was opened with.
        /// </summary>
        public int BaudRate
        {
            get
            {
                if (disposed) throw new ObjectDisposedException(nameof(SspSerialPort));
                return port.BaudRate;
            }
            set
            {
                if (disposed) throw new ObjectDisposedException(nameof(SspSerialPort));
                port.BaudRate = value;
            }
        }

        /// <summary>Closes the port.</summary>
        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            port.Dispose();
        }
    }
}

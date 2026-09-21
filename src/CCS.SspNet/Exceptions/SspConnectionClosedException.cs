using System;

namespace CCS.SspNet.Exceptions
{
    /// <summary>
    /// Thrown when the stream carrying SSP traffic ended part-way through a packet — a closed port,
    /// an unplugged cable, or a socket dropped by the other end.
    /// </summary>
    public class SspConnectionClosedException : Exception
    {
        /// <summary>Initializes a new instance.</summary>
        public SspConnectionClosedException() { }

        /// <summary>Initializes a new instance with a message.</summary>
        public SspConnectionClosedException(string message) : base(message) { }

        /// <summary>Initializes a new instance with a message and an inner exception.</summary>
        public SspConnectionClosedException(string message, Exception innerException) : base(message, innerException) { }
    }
}

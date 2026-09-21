using System;

namespace CCS.SspNet.Exceptions
{
    /// <summary>
    /// Thrown when an exchange with a device could not be completed, after every retry was used.
    /// The <see cref="Exception.InnerException"/> carries the last failure.
    /// </summary>
    public class SspCommunicationException : Exception
    {
        /// <summary>Initializes a new instance.</summary>
        public SspCommunicationException() { }

        /// <summary>Initializes a new instance with a message.</summary>
        public SspCommunicationException(string message) : base(message) { }

        /// <summary>Initializes a new instance with a message and an inner exception.</summary>
        public SspCommunicationException(string message, Exception innerException) : base(message, innerException) { }
    }
}

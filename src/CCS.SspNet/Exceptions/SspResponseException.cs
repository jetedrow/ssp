using System;

namespace CCS.SspNet.Exceptions
{
    /// <summary>
    /// Thrown when a device understood a command and refused it, or failed carrying it out.
    /// </summary>
    /// <remarks>
    /// This is a working device saying no, not a communication fault — the packet arrived intact
    /// and the device answered.  <see cref="SspCommunicationException"/> covers the other case.
    /// </remarks>
    public class SspResponseException : Exception
    {
        /// <summary>Initializes a new instance.</summary>
        public SspResponseException() { }

        /// <summary>Initializes a new instance with a message.</summary>
        public SspResponseException(string message) : base(message) { }

        /// <summary>Initializes a new instance with a message and an inner exception.</summary>
        public SspResponseException(string message, Exception innerException) : base(message, innerException) { }

        /// <summary>Initializes a new instance for a response code.</summary>
        /// <param name="response">The code the device answered with.</param>
        public SspResponseException(SspResponse response)
            : base(Describe(response)) => Response = response;

        /// <summary>Gets the code the device answered with.</summary>
        public SspResponse Response { get; }

        private static string Describe(SspResponse response) =>
            Enum.IsDefined(typeof(SspResponse), response)
                ? $"The device answered {response} (0x{(byte)response:X2})."
                : $"The device answered with an unrecognised response code, 0x{(byte)response:X2}.";
    }
}

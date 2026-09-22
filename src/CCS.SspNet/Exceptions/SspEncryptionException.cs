using System;

namespace CCS.SspNet.Exceptions
{
    /// <summary>
    /// The encrypted layer could not make sense of a packet.
    /// </summary>
    /// <remarks>
    /// This is not a transmission error — the transport's own CRC has already passed by the time
    /// anything here runs — so it is not worth retrying.  It means the two ends disagree about the
    /// key, the packet counter, or the shape of the block.
    /// </remarks>
    public class SspEncryptionException : SspCommunicationException
    {
        /// <summary>Creates the exception.</summary>
        public SspEncryptionException(string message) : base(message) { }

        /// <summary>Creates the exception.</summary>
        public SspEncryptionException(string message, Exception innerException) : base(message, innerException) { }
    }
}

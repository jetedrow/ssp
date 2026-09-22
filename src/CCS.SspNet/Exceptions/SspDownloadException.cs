using System;

namespace CCS.SspNet.Exceptions
{
    /// <summary>
    /// A firmware or dataset download could not be completed.
    /// </summary>
    /// <remarks>
    /// A download is not a routine exchange that can be retried in place: it leaves the device part
    /// way through replacing its own program, and ITL's own guidance is blunt that getting it wrong
    /// can damage a unit.  So this is thrown rather than swallowed, and the message says which stage
    /// failed so a caller can decide whether the device is safe to leave as it is or must be
    /// re-flashed.
    /// </remarks>
    public class SspDownloadException : SspCommunicationException
    {
        /// <summary>Creates the exception.</summary>
        public SspDownloadException(string message) : base(message) { }

        /// <summary>Creates the exception.</summary>
        public SspDownloadException(string message, Exception innerException) : base(message, innerException) { }
    }
}

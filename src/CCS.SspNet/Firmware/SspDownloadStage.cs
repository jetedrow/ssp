namespace CCS.SspNet.Firmware
{
    /// <summary>
    /// Where a download has reached, as reported to an <see cref="System.IProgress{T}"/>.
    /// </summary>
    public enum SspDownloadStage
    {
        /// <summary>Checking the device is there and answering.</summary>
        Synchronising,

        /// <summary>Telling the device a download is coming and learning its block size.</summary>
        Preparing,

        /// <summary>Sending the header, which the device accepts or rejects as meant for it.</summary>
        SendingHeader,

        /// <summary>Sending the small program the device runs to update itself.</summary>
        SendingRamBlock,

        /// <summary>Sending the firmware or dataset payload.</summary>
        SendingPayload,

        /// <summary>Waiting for the device to come back after it resets.</summary>
        Restarting,

        /// <summary>The device is back and answering; the download is done.</summary>
        Complete,
    }
}

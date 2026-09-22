namespace CCS.SspNet.Firmware
{
    /// <summary>
    /// A snapshot of a running download.
    /// </summary>
    /// <remarks>
    /// A download moves several hundred kilobytes and can take minutes, so it reports as it goes.
    /// <see cref="BytesSent"/> and <see cref="BytesTotal"/> count the RAM block and payload
    /// together — the bulk that takes the time — so a single bar over the pair tracks the whole
    /// transfer.
    /// </remarks>
    public readonly struct SspDownloadProgress
    {
        /// <summary>Creates a snapshot.</summary>
        public SspDownloadProgress(SspDownloadStage stage, long bytesSent, long bytesTotal)
        {
            Stage = stage;
            BytesSent = bytesSent;
            BytesTotal = bytesTotal;
        }

        /// <summary>Gets the stage the download is in.</summary>
        public SspDownloadStage Stage { get; }

        /// <summary>Gets how many payload and RAM bytes have been sent so far.</summary>
        public long BytesSent { get; }

        /// <summary>Gets how many payload and RAM bytes there are in total.</summary>
        public long BytesTotal { get; }

        /// <summary>Gets the fraction transferred, from 0 to 1, or 0 when nothing is known yet.</summary>
        public double Fraction => BytesTotal <= 0 ? 0 : (double)BytesSent / BytesTotal;
    }
}

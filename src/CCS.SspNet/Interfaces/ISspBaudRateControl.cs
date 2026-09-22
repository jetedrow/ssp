namespace CCS.SspNet.Interfaces
{
    /// <summary>
    /// A transport that can change the line speed under a running connection.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The library talks to a plain <see cref="System.IO.Stream"/> and asks nothing of it beyond
    /// reading and writing bytes, because for ordinary commands the line speed is set once and
    /// never touched.  A firmware download is the one exception: partway through, the device
    /// switches to a faster speed to take the payload, and a host that cannot follow it there is
    /// left talking into a line the device is no longer listening to at.
    /// </para>
    /// <para>
    /// A serial transport implements this; an in-memory or network stream has no line speed and
    /// does not.  A download over a transport that cannot change speed simply runs at whatever the
    /// stream is already set to — which is correct for a test, and something to know for a real
    /// device.
    /// </para>
    /// </remarks>
    public interface ISspBaudRateControl
    {
        /// <summary>Gets or sets the line speed in bits per second.</summary>
        int BaudRate { get; set; }

        /// <summary>Throws away anything sitting unread in the transport's buffers.</summary>
        /// <remarks>
        /// A download changes what the bytes on the line mean partway through, so anything buffered
        /// from before the change has to be discarded rather than read as if it belonged after it.
        /// </remarks>
        void DiscardBuffers();
    }
}

namespace CCS.SspNet.Hosting
{
    /// <summary>
    /// What to do about a note the validator is holding in escrow.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A validator that has read and accepted a note holds it where the user can still get it back,
    /// and waits.  The <em>next poll</em> is what takes the note — polling is not a passive read.
    /// A host therefore has exactly one poll interval in which to decide, and doing nothing is a
    /// decision: it accepts.
    /// </para>
    /// <para>
    /// The validator also has a ten second timeout of its own.  If no poll arrives within it, the
    /// note is rejected and the device reports an escrow timeout, so a host cannot hold a note by
    /// simply stopping.
    /// </para>
    /// </remarks>
    public enum SspEscrowAction
    {
        /// <summary>
        /// Let the next poll take the note.  This is what happens if a handler does nothing.
        /// </summary>
        Accept = 0,

        /// <summary>
        /// Keep the note in escrow for another interval, and decide later.
        /// </summary>
        /// <remarks>
        /// This sends a hold command, which resets the device's own escrow timeout.  A host can
        /// hold a note indefinitely by holding it on every poll.
        /// </remarks>
        Hold,

        /// <summary>Give the note back to the user.</summary>
        Reject,
    }
}

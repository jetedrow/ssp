using System;
using CCS.SspNet.Protocol;

namespace CCS.SspNet.Hosting
{
    /// <summary>
    /// One event from a device, and the chance to act on it before the next poll.
    /// </summary>
    public sealed class SspDeviceEventArgs : EventArgs
    {
        internal SspDeviceEventArgs(SspDevice device, SspPollEvent pollEvent)
        {
            Device = device;
            Event = pollEvent;
        }

        /// <summary>Gets the device that reported this.</summary>
        public SspDevice Device { get; }

        /// <summary>Gets the event and its data.</summary>
        public SspPollEvent Event { get; }

        /// <summary>
        /// Gets a value indicating whether this event means a note is sitting in escrow, waiting
        /// for the host to decide.
        /// </summary>
        /// <remarks>
        /// A read event with an all-zero payload means the note is still being scanned.  Once any
        /// byte of it is non-zero the note has been validated and is held, and the next poll takes
        /// it unless <see cref="Escrow"/> says otherwise.
        /// </remarks>
        public bool IsNoteInEscrow
        {
            get
            {
                if (Event.Event != SspEvent.Read) return false;

                var data = Event.Data.Span;
                for (var i = 0; i < data.Length; i++)
                {
                    if (data[i] != 0) return true;
                }

                return false;
            }
        }

        /// <summary>
        /// Gets or sets what to do about a note in escrow.  Ignored unless
        /// <see cref="IsNoteInEscrow"/> is <see langword="true"/>.
        /// </summary>
        /// <remarks>
        /// Setting this is the only way to stop the next poll accepting the note, and the host
        /// applies it as soon as every handler has run — so a handler must decide while it is on
        /// the stack, not afterwards.  A handler that needs to await something before deciding
        /// should use <see cref="SspDeviceHost.ReadEventsAsync"/> instead, where the decision can
        /// be awaited.
        /// </remarks>
        public SspEscrowAction Escrow { get; set; } = SspEscrowAction.Accept;
    }
}

using System;

namespace CCS.SspNet.Protocol
{
    /// <summary>
    /// One note channel from a banknote validator's setup reply.
    /// </summary>
    /// <remarks>
    /// A channel is a denomination the loaded dataset recognises.  Poll events identify a note by
    /// its channel number, so this is how a credit turns into an amount of money.
    /// </remarks>
    public readonly struct SspChannel
    {
        /// <summary>Initializes a new instance.</summary>
        /// <param name="number">The channel number, counting from one.</param>
        /// <param name="value">The note's value in the smallest unit of its currency.</param>
        /// <param name="countryCode">The three-letter code of the note's currency.</param>
        public SspChannel(int number, uint value, string countryCode)
        {
            Number = number;
            Value = value;
            CountryCode = countryCode;
        }

        /// <summary>
        /// Gets the channel number, counting from one.  This is the number a
        /// <see cref="SspEvent.NoteCredit"/> payload carries.
        /// </summary>
        public int Number { get; }

        /// <summary>
        /// Gets the note's value in the smallest unit of its currency — cents, not euros.
        /// </summary>
        public uint Value { get; }

        /// <summary>Gets the three-letter code of the note's currency, such as <c>EUR</c>.</summary>
        public string CountryCode { get; }

        /// <inheritdoc />
        public override string ToString() => $"Channel {Number}: {Value} {CountryCode}";
    }
}

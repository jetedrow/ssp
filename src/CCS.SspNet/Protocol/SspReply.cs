using System;
using CCS.SspNet.Exceptions;

namespace CCS.SspNet.Protocol
{
    /// <summary>
    /// A device's reply to a command, split into its response code and the data behind it.
    /// </summary>
    /// <remarks>
    /// Every SSP reply starts with a response code.  For most commands an <see cref="SspResponse.OK"/>
    /// is followed by whatever that command returns; for a poll it is followed by the event list.
    /// </remarks>
    public readonly struct SspReply
    {
        /// <summary>Initializes a new instance.</summary>
        /// <param name="code">The response code.</param>
        /// <param name="data">Everything after it.</param>
        public SspReply(SspResponse code, ReadOnlyMemory<byte> data)
        {
            Code = code;
            Data = data;
        }

        /// <summary>Gets the response code the reply opened with.</summary>
        public SspResponse Code { get; }

        /// <summary>Gets the reply's data, which may be empty.</summary>
        public ReadOnlyMemory<byte> Data { get; }

        /// <summary>Gets a value indicating whether the device accepted the command.</summary>
        public bool IsOk => Code == SspResponse.OK;

        /// <summary>
        /// Splits a reply's raw data into a response code and the rest.
        /// </summary>
        /// <param name="payload">A reply packet's data, as <see cref="Communication.SspRawPacket.Data"/> gives it.</param>
        /// <exception cref="PacketFormatException">The reply carried no response code at all.</exception>
        public static SspReply Parse(ReadOnlySpan<byte> payload)
        {
            if (payload.Length == 0)
            {
                throw new PacketFormatException("A reply must carry at least a response code.");
            }

            return new SspReply((SspResponse)payload[0], payload.Slice(1).ToArray());
        }

        /// <summary>
        /// Throws unless the device accepted the command.
        /// </summary>
        /// <exception cref="SspResponseException">The device refused or failed the command.</exception>
        public void EnsureOk()
        {
            if (!IsOk) throw new SspResponseException(Code);
        }

        /// <inheritdoc />
        public override string ToString() =>
            Data.Length == 0 ? Code.ToString() : $"{Code}, {Data.Length} data byte(s)";
    }
}

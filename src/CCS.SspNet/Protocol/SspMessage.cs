using System;

namespace CCS.SspNet.Protocol
{
    /// <summary>
    /// Builds the data half of a command packet: the command code, then its parameters.
    /// </summary>
    /// <remarks>
    /// Framing, addressing, the sequence flag and the CRC all belong to the layer below, so what
    /// is built here is only what goes in a packet's data field.
    /// </remarks>
    public static class SspMessage
    {
        /// <summary>
        /// Builds a command with no parameters.
        /// </summary>
        /// <param name="command">The command code.</param>
        public static byte[] Create(SspCommand command) => new[] { (byte)command };

        /// <summary>
        /// Builds a command and its parameters.
        /// </summary>
        /// <param name="command">The command code.</param>
        /// <param name="parameters">The bytes that follow it.</param>
        public static byte[] Create(SspCommand command, params byte[] parameters) =>
            Create((byte)command, parameters);

        /// <summary>
        /// Builds a command this library has no name for, so that a device newer than the library
        /// can still be driven.
        /// </summary>
        /// <param name="command">The command code.</param>
        /// <param name="parameters">The bytes that follow it.</param>
        /// <exception cref="ArgumentException">
        /// The command and its parameters would not fit in one packet.
        /// </exception>
        public static byte[] Create(byte command, params byte[] parameters)
        {
            parameters ??= Array.Empty<byte>();

            if (parameters.Length + 1 > Constants.MaxDataLength)
            {
                throw new ArgumentException(
                    $"A command and its parameters must fit in {Constants.MaxDataLength} bytes; " +
                    $"this one needs {parameters.Length + 1}.",
                    nameof(parameters));
            }

            var message = new byte[parameters.Length + 1];
            message[0] = command;
            parameters.CopyTo(message, 1);
            return message;
        }
    }
}

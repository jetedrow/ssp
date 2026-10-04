using System;
using System.Collections.Generic;
using System.Text;

namespace SmileySecure.Net.Exceptions
{
    public class PacketFormatException : Exception
    {
        public PacketFormatException()
        {
        }

        public PacketFormatException(string message) : base(message)
        {
        }

        public PacketFormatException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}

using System;
using System.Collections.Generic;
using System.Text;

namespace SmileySecure.Net.Exceptions
{
    public class PacketLengthException : Exception
    {
        public PacketLengthException()
        {
        }

        public PacketLengthException(string message) : base(message)
        {
        }

        public PacketLengthException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}

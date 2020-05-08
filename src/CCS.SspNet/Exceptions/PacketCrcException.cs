using System;
using System.Collections.Generic;
using System.Text;

namespace CCS.SspNet.Exceptions
{
    public class PacketCrcException : Exception
    {
        public PacketCrcException()
        {
        }
        public PacketCrcException(string message) : base(message)
        {
        }

        public PacketCrcException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}

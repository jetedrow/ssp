using System;
using System.Collections.Generic;
using System.Text;

namespace CCS.SspNet.Interfaces
{
    public interface ISspRawPacket
    {
        byte Address { get; }
#pragma warning disable CA1819 // Properties should not return arrays
        byte[] Data { get; }
#pragma warning restore CA1819 // Properties should not return arrays

        IEnumerable<byte> GetPacketBytes(bool sequenceFlag);
    }
}

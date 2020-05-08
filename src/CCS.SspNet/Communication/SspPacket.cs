using CCS.SspNet.Exceptions;
using System;
using System.Collections.Generic;
using System.Text;

namespace CCS.SspNet.Communication
{
    //internal struct SspPacket
    //{
    //    public SspPacket(int stuff)
    //    {
    //        data = new byte[255];
    //        dataMem = data.AsMemory();
    //        length = 0;
    //        address = 0;
    //        SequenceFlag = false;
    //    }

    //    private Memory<byte> dataMem;
    //    private readonly byte[] data;
    //    private byte length;
    //    private byte address;

    //    public byte Length => length;

    //    public byte[] Data
    //    {
    //        get
    //        {
    //            return dataMem.Slice(0, length).ToArray();
    //        }
    //        set
    //        {
    //            if (value.Length > 255) throw new PacketLengthException("Data may only be up to 255 bytes per packet.");
    //            length = (byte)value.Length;
    //            value.CopyTo(dataMem);

    //        }
    //    }
    //    public bool SequenceFlag { get; set; }

    //}
}

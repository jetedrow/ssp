using CCS.SspNet.Exceptions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace CCS.SspNet.Communication
{
    class SspStreamReader : IDisposable
    {
        private readonly byte[] packetBuffer;
        private short currentLength = 0;

        public SspStreamReader(Stream stream) 
        {
            if (!stream.CanRead) throw new ArgumentException("Stream does not support reading.", nameof(stream));
            packetBuffer = new byte[260];
            this.BaseStream = stream ?? throw new ArgumentNullException(nameof(stream));
        }

        public Stream BaseStream { get; }
        public bool EndOfStream => throw new NotImplementedException();

        public void Close()
        {
            BaseStream.Close();
        }

        public void DiscardBufferedData()
        {
            packetBuffer.AsSpan().Fill(0);
            currentLength = 0;
        }

        /// <summary>
        /// Reads the next SSP packet fromt he input stream.
        /// </summary>
        /// <returns>The raw bytes of the SSP packet.</returns>
        public async Task<byte[]> ReadRawPacketAsync()
        {
            var packetComplete = false;
            var byteStuffRequired = true;
            var bytesRemaining = 3; // Set so STX, SEQ/ADDR, AND LEN can be read before setting actual bytes remaining in packet.
            while (!packetComplete)
            {
                var curByte = BaseStream.ReadByte();
                if (curByte == -1)
                {
                    await Task.Delay(50); // There is no data yet, so we delay.
                    continue;
                }
                            
                byte b = (byte)curByte;

                if (currentLength == 0 && b != Constants.STX)
                {
                    // We are waiting for an STX to appear on the stream as the start of a packet.
                    continue;
                }
                else if (byteStuffRequired)
                {
                    // If we are waiting for 2nd byte stuffed STX, and the next character is not an STX, the packet was not propperly byte stuffed.
                    if (b != Constants.STX) throw new PacketFormatException($"Non-byte-stuffed STX byte encountered within packet.");
                    // We can add this character to the data buffer.
                    packetBuffer[currentLength] = b;
                    currentLength++;
                    bytesRemaining--;

                    byteStuffRequired = false;
                }
                else if (b == Constants.STX)
                {
                    // This is the beginning of byte stuffing.  We do not read this character into the buffer, but set the flag for the next character.
                    byteStuffRequired = true;
                }
                else if (currentLength == 2)
                {
                    // This character will be the length byte from the packet.  It should set the remaining number of bytes to be read (length + 2 CRC bytes).
                    packetBuffer[currentLength] = b;
                    currentLength++;
                    bytesRemaining = 2 + b;
                }
                else
                {
                    // Normal character.  Read into buffer.
                    packetBuffer[currentLength] = b;
                    currentLength++;
                    bytesRemaining--;

                    if (bytesRemaining == 0)
                    {
                        packetComplete = true;
                    }
                }

            }

            var ret = new byte[currentLength];
            Array.Copy(packetBuffer, 0, ret, 0, currentLength);
            return ret;
        }

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // Dispose managed state (managed objects).
                }

                // Free unmanaged resources (unmanaged objects) and override a finalizer below.
                // Set large fields to null.

                disposedValue = true;
            }
        }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
        }
        #endregion

    }
}

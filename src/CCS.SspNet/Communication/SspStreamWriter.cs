using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace CCS.SspNet.Communication
{
    class SspStreamWriter : IDisposable
    {

        public SspStreamWriter(Stream stream)
        {
            if (!stream.CanWrite) throw new ArgumentException("Stream does not support writing.", nameof(stream));
            this.BaseStream = stream ?? throw new ArgumentNullException(nameof(stream));
        }

        public Stream BaseStream { get; }
        public bool EndOfStream => throw new NotImplementedException();

        public void Close()
        {
            BaseStream.Close();
        }


        #region IDisposable Support

        private bool disposedValue;

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // TODO: dispose managed state (managed objects)
                }

                // TODO: free unmanaged resources (unmanaged objects) and override finalizer
                // TODO: set large fields to null
                disposedValue = true;
            }
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        #endregion
    }
}

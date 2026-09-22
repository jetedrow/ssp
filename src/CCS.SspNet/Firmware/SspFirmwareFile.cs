using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CCS.SspNet.Exceptions;

namespace CCS.SspNet.Firmware
{
    /// <summary>
    /// An ITL firmware or dataset file, parsed into the three parts a download sends separately.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A file is a 128-byte header, then a RAM block, then the firmware or dataset itself.  The
    /// header is sent first and tells the device whether the file is even meant for it; the RAM
    /// block is a small program the device runs to carry out its own update; the last block is the
    /// payload that program writes.
    /// </para>
    /// <para>
    /// The sizes are not guessed.  The header's own bytes say where the RAM block ends: byte 6 is
    /// the update code the device is told to run, and bytes 7 to 10 are the RAM block's length as a
    /// big-endian number.  Everything after the header and the RAM block is the payload.
    /// </para>
    /// <para>
    /// Parsing here does no I/O and talks to no device.  It exists so that a malformed or
    /// wrong-device file is rejected in memory, before a download has begun to overwrite anything.
    /// </para>
    /// </remarks>
    public sealed class SspFirmwareFile
    {
        /// <summary>The three bytes every ITL file begins with: <c>I</c>, <c>T</c>, <c>L</c>.</summary>
        public static readonly byte[] Magic = { 0x49, 0x54, 0x4C };

        /// <summary>The length of the header block, which is also the block every raw write is sized in.</summary>
        public const int HeaderLength = 128;

        private const int UpdateCodeIndex = 6;
        private const int RamSizeIndex = 7;

        private readonly byte[] contents;

        private SspFirmwareFile(byte[] contents, int ramSize)
        {
            this.contents = contents;
            RamBlockLength = ramSize;
        }

        /// <summary>Gets the update code the device is told to run, taken from header byte 6.</summary>
        public byte UpdateCode => contents[UpdateCodeIndex];

        /// <summary>Gets the length of the RAM block.</summary>
        public int RamBlockLength { get; }

        /// <summary>Gets the length of the firmware or dataset payload that follows the RAM block.</summary>
        public int PayloadLength => contents.Length - HeaderLength - RamBlockLength;

        /// <summary>Gets the whole file's length.</summary>
        public int Length => contents.Length;

        /// <summary>Gets the 128-byte header block.</summary>
        public ReadOnlyMemory<byte> Header => new ReadOnlyMemory<byte>(contents, 0, HeaderLength);

        /// <summary>Gets the RAM block that runs the update.</summary>
        public ReadOnlyMemory<byte> RamBlock => new ReadOnlyMemory<byte>(contents, HeaderLength, RamBlockLength);

        /// <summary>Gets the firmware or dataset payload.</summary>
        public ReadOnlyMemory<byte> Payload =>
            new ReadOnlyMemory<byte>(contents, HeaderLength + RamBlockLength, PayloadLength);

        /// <summary>
        /// Parses a file already in memory.
        /// </summary>
        /// <param name="contents">The whole file.</param>
        /// <exception cref="SspDownloadException">
        /// The file is too short to hold what its header describes, or does not begin with the ITL
        /// marker.
        /// </exception>
        public static SspFirmwareFile Parse(byte[] contents)
        {
            if (contents == null) throw new ArgumentNullException(nameof(contents));

            if (contents.Length < HeaderLength)
            {
                throw new SspDownloadException(
                    $"A firmware file is at least its {HeaderLength}-byte header, but this one is {contents.Length} bytes.");
            }

            for (var i = 0; i < Magic.Length; i++)
            {
                if (contents[i] != Magic[i])
                {
                    throw new SspDownloadException(
                        "The file does not begin with the bytes 'ITL', so it is not an Innovative Technology firmware file. " +
                        "Sending it to a device would be sending it something the device cannot use.");
                }
            }

            var ramSize =
                (contents[RamSizeIndex] << 24) |
                (contents[RamSizeIndex + 1] << 16) |
                (contents[RamSizeIndex + 2] << 8) |
                contents[RamSizeIndex + 3];

            if (ramSize < 0 || HeaderLength + ramSize > contents.Length)
            {
                throw new SspDownloadException(
                    $"The header says the RAM block is {ramSize} bytes, which does not fit in a {contents.Length}-byte file.");
            }

            return new SspFirmwareFile((byte[])contents.Clone(), ramSize);
        }

        /// <summary>
        /// Reads and parses a file from a stream.
        /// </summary>
        public static async Task<SspFirmwareFile> LoadAsync(Stream source, CancellationToken cancellationToken = default)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));

            using var buffer = new MemoryStream();
            await source.CopyToAsync(buffer, 81920, cancellationToken).ConfigureAwait(false);
            return Parse(buffer.ToArray());
        }

        /// <summary>
        /// Reads and parses a file from a path.
        /// </summary>
        public static async Task<SspFirmwareFile> LoadAsync(string path, CancellationToken cancellationToken = default)
        {
            if (path == null) throw new ArgumentNullException(nameof(path));

#if NETSTANDARD2_0
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true);
#else
            await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true);
#endif
            return await LoadAsync(stream, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>Gets a short description of the file, for a log line.</summary>
        public override string ToString() =>
            $"ITL firmware file, {Length} bytes (header {HeaderLength}, RAM {RamBlockLength}, payload {PayloadLength}), update code 0x{UpdateCode:X2}";
    }
}

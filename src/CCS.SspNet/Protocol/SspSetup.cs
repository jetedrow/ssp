using System;
using System.Collections.Generic;
using System.Text;
using CCS.SspNet.Exceptions;

namespace CCS.SspNet.Protocol
{
    /// <summary>
    /// A device's reply to a setup request: what it is, what firmware it runs, what protocol
    /// version it is currently set to, and — for a banknote validator — what its dataset holds.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Every device type lays this reply out differently, so only the first five bytes can be read
    /// without knowing which device answered. <see cref="Channels"/> is populated for a banknote
    /// validator and its payout variants; for any other unit type it is empty and
    /// <see cref="Data"/> carries the bytes for a caller that knows the layout.
    /// </para>
    /// <para>
    /// <see cref="ProtocolVersion"/> is the reason to send this command at connect: it is the only
    /// way to learn what version the device is at, which decides how its poll replies read.
    /// </para>
    /// </remarks>
    public sealed class SspSetup
    {
        private SspSetup(SspUnitType unitType, string firmwareVersion, string countryCode,
                         SspProtocolVersion? protocolVersion, IReadOnlyList<SspChannel> channels,
                         uint valueMultiplier, ReadOnlyMemory<byte> data)
        {
            UnitType = unitType;
            FirmwareVersion = firmwareVersion;
            CountryCode = countryCode;
            ProtocolVersion = protocolVersion;
            Channels = channels;
            ValueMultiplier = valueMultiplier;
            Data = data;
        }

        /// <summary>Gets what kind of device answered.</summary>
        public SspUnitType UnitType { get; }

        /// <summary>
        /// Gets the firmware version as four ASCII digits, so <c>0335</c> means 3.35.
        /// </summary>
        public string FirmwareVersion { get; }

        /// <summary>Gets the three-letter code of the dataset's main currency.</summary>
        public string CountryCode { get; }

        /// <summary>
        /// Gets the protocol version the device is currently set to, or <see langword="null"/> if
        /// the reply was too short or its layout is not known.
        /// </summary>
        public SspProtocolVersion? ProtocolVersion { get; }

        /// <summary>
        /// Gets the dataset's channels, empty for a device whose setup layout this library does
        /// not parse.
        /// </summary>
        public IReadOnlyList<SspChannel> Channels { get; }

        /// <summary>
        /// Gets the legacy value multiplier.  Zero on a protocol version 6 or later dataset, where
        /// the per-channel values in <see cref="Channels"/> are already full values.
        /// </summary>
        public uint ValueMultiplier { get; }

        /// <summary>Gets the whole reply, for a caller reading a layout this library does not.</summary>
        public ReadOnlyMemory<byte> Data { get; }

        /// <summary>
        /// Reads a setup request reply.
        /// </summary>
        /// <param name="data">The reply's data, with the response code already removed.</param>
        /// <exception cref="PacketFormatException">The reply is too short to be a setup reply.</exception>
        public static SspSetup Parse(ReadOnlySpan<byte> data)
        {
            // Unit type, four firmware bytes and a three-byte country code are common to every
            // device type; everything after that depends on which device answered.
            const int HeaderLength = 8;

            if (data.Length < HeaderLength)
            {
                throw new PacketFormatException(
                    $"A setup reply is at least {HeaderLength} bytes; this one is {data.Length}.");
            }

            var unitType = (SspUnitType)data[0];
            var firmware = Ascii(data.Slice(1, 4));
            var country = Ascii(data.Slice(5, 3));

            return IsBanknoteValidator(unitType)
                ? ParseValidator(data, unitType, firmware, country)
                : new SspSetup(unitType, firmware, country, null, Array.Empty<SspChannel>(), 0, data.ToArray());
        }

        private static bool IsBanknoteValidator(SspUnitType unitType) =>
            unitType == SspUnitType.BanknoteValidator
            || unitType == SspUnitType.SmartPayout
            || unitType == SspUnitType.NoteFloat
            || unitType == SspUnitType.Tebs
            || unitType == SspUnitType.TebsWithSmartPayout
            || unitType == SspUnitType.TebsWithSmartTicket;

        private static SspSetup ParseValidator(
            ReadOnlySpan<byte> data, SspUnitType unitType, string firmware, string country)
        {
            // Layout, with n channels:
            //   0  unit type              8   value multiplier (3, big endian)
            //   1  firmware (4 ascii)     11  channel count, n
            //   5  country code (3)       12  channel values, one byte each (legacy)
            //   12 + n       channel security, one byte each (obsolete)
            //   12 + 2n      real value multiplier (3, big endian)
            //   15 + 2n      protocol version
            // and from protocol version 6, the part that actually carries the money:
            //   16 + 2n      country code per channel (3 ascii each)
            //   16 + 5n      value per channel (4, LITTLE endian -- unlike the multipliers)
            //   16 + 9n      end
            const int CountOffset = 11;

            if (data.Length <= CountOffset)
            {
                return new SspSetup(unitType, firmware, country, null, Array.Empty<SspChannel>(), 0, data.ToArray());
            }

            var valueMultiplier = ReadBigEndian24(data.Slice(8, 3));
            var channelCount = data[CountOffset];

            var protocolVersionOffset = 15 + (channelCount * 2);
            SspProtocolVersion? protocolVersion = data.Length > protocolVersionOffset && data[protocolVersionOffset] != 0
                ? new SspProtocolVersion(data[protocolVersionOffset])
                : (SspProtocolVersion?)null;

            var channels = ReadChannels(data, channelCount, valueMultiplier);

            return new SspSetup(unitType, firmware, country, protocolVersion, channels, valueMultiplier, data.ToArray());
        }

        private static IReadOnlyList<SspChannel> ReadChannels(
            ReadOnlySpan<byte> data, byte channelCount, uint valueMultiplier)
        {
            if (channelCount == 0) return Array.Empty<SspChannel>();

            var countryOffset = 16 + (channelCount * 2);
            var valueOffset = 16 + (channelCount * 5);
            var expandedEnd = 16 + (channelCount * 9);

            var channels = new SspChannel[channelCount];

            if (data.Length >= expandedEnd)
            {
                // Protocol version 6 and later. Each channel carries its own currency and a full
                // value, so a multi-currency dataset reads correctly.
                for (var i = 0; i < channelCount; i++)
                {
                    channels[i] = new SspChannel(
                        i + 1,
                        SspValues.ReadAmount(data.Slice(valueOffset + (i * 4), 4)),
                        Ascii(data.Slice(countryOffset + (i * 3), 3)));
                }

                return channels;
            }

            // Before version 6 there is one currency, and a channel's value is a single byte that
            // has to be multiplied up.
            const int LegacyValueOffset = 12;

            for (var i = 0; i < channelCount; i++)
            {
                var raw = LegacyValueOffset + i < data.Length ? data[LegacyValueOffset + i] : (byte)0;
                channels[i] = new SspChannel(i + 1, raw * valueMultiplier, Ascii(data.Slice(5, 3)));
            }

            return channels;
        }

        /// <summary>
        /// Reads a three-byte big-endian number.  The two multipliers in a setup reply are the
        /// only big-endian values in it; the per-channel values beside them are little-endian.
        /// </summary>
        private static uint ReadBigEndian24(ReadOnlySpan<byte> data) =>
            (uint)((data[0] << 16) | (data[1] << 8) | data[2]);

        private static string Ascii(ReadOnlySpan<byte> data)
        {
#if NETSTANDARD2_0
            return Encoding.ASCII.GetString(data.ToArray());
#else
            return Encoding.ASCII.GetString(data);
#endif
        }

        /// <inheritdoc />
        public override string ToString() =>
            $"{UnitType}, firmware {FirmwareVersion}, {CountryCode}, protocol version " +
            $"{(ProtocolVersion is null ? "unknown" : ProtocolVersion.ToString())}, {Channels.Count} channel(s)";
    }
}

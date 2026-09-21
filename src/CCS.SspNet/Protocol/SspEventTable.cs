using System;
using System.Collections.Generic;
using System.Linq;

namespace CCS.SspNet.Protocol
{
    /// <summary>
    /// How long each event's payload is, at each protocol version.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the one piece of knowledge a poll reply cannot be read without, and the one most
    /// likely to go out of date: every protocol revision adds events, and some revisions change the
    /// payload of an event that already exists.  So it is data rather than code, and it is
    /// immutable — <see cref="WithEvent"/> returns a new table rather than altering a shared one.
    /// </para>
    /// <para>
    /// A device newer than this library can therefore be supported without waiting for a release:
    /// </para>
    /// <code>
    /// var table = SspEventTable.Default
    ///     .WithEvent(0x7C, firstVersion: 10, SspEventPayload.Fixed(4));
    /// </code>
    /// <para>
    /// <see cref="Default"/> is built from issue 2.2 of the SSP protocol manual and covers protocol
    /// versions <see cref="SspProtocolVersion.Lowest"/> to <see cref="SspProtocolVersion.Highest"/>.
    /// </para>
    /// </remarks>
    public sealed class SspEventTable
    {
        // Each entry is the variants for one code, ordered by the version they first appear at.
        // Looking up a version walks backwards to the first variant at or below it, so a version
        // above every variant gets the newest — which is what a device reporting an unreleased
        // version does in practice.
        private readonly Dictionary<byte, Variant[]> entries;

        private SspEventTable(Dictionary<byte, Variant[]> entries) => this.entries = entries;

        /// <summary>
        /// Gets the table built from issue 2.2 of the SSP protocol manual.
        /// </summary>
        public static SspEventTable Default { get; } = BuildDefault();

        /// <summary>Gets every event code this table has a rule for.</summary>
        public IReadOnlyCollection<byte> Codes => entries.Keys;

        /// <summary>
        /// Finds the payload shape for an event at a given protocol version.
        /// </summary>
        /// <param name="code">The event code, as it appears in the reply.</param>
        /// <param name="version">The protocol version the device is set to.</param>
        /// <param name="payload">The shape, when this returns <see langword="true"/>.</param>
        /// <returns>
        /// <see langword="false"/> when the table has no rule for this code, or none that applies
        /// at or below <paramref name="version"/>.
        /// </returns>
        public bool TryGetPayload(byte code, SspProtocolVersion version, out SspEventPayload payload)
        {
            payload = SspEventPayload.Unknown;

            if (!entries.TryGetValue(code, out var variants)) return false;

            for (var i = variants.Length - 1; i >= 0; i--)
            {
                if (variants[i].FirstVersion <= version)
                {
                    payload = variants[i].Payload;
                    return payload.Kind != SspEventPayloadKind.Unknown;
                }
            }

            // Every variant was added after this version, so the device should never send it.
            return false;
        }

        /// <summary>
        /// Returns a copy of this table with one event's payload added or replaced.
        /// </summary>
        /// <param name="code">The event code.</param>
        /// <param name="firstVersion">The protocol version the shape takes effect at.</param>
        /// <param name="payload">The payload shape from that version onwards.</param>
        /// <remarks>
        /// Registering a shape at a version that already has one replaces it.  Registering at a
        /// version no other variant covers leaves the others in place, so an event whose payload
        /// grew can be described one revision at a time.
        /// </remarks>
        public SspEventTable WithEvent(byte code, SspProtocolVersion firstVersion, SspEventPayload payload)
        {
            var copy = new Dictionary<byte, Variant[]>(entries);
            var existing = entries.TryGetValue(code, out var variants)
                ? variants.Where(v => v.FirstVersion != firstVersion)
                : Enumerable.Empty<Variant>();

            copy[code] = existing
                .Concat(new[] { new Variant(firstVersion, payload) })
                .OrderBy(v => v.FirstVersion.Value)
                .ToArray();

            return new SspEventTable(copy);
        }

        /// <summary>
        /// Returns a copy of this table with an event removed entirely.
        /// </summary>
        /// <param name="code">The event code to drop.</param>
        public SspEventTable WithoutEvent(byte code)
        {
            if (!entries.ContainsKey(code)) return this;

            var copy = new Dictionary<byte, Variant[]>(entries);
            copy.Remove(code);
            return new SspEventTable(copy);
        }

        private readonly struct Variant
        {
            public Variant(SspProtocolVersion firstVersion, SspEventPayload payload)
            {
                FirstVersion = firstVersion;
                Payload = payload;
            }

            public SspProtocolVersion FirstVersion { get; }

            public SspEventPayload Payload { get; }
        }

        private static SspEventTable BuildDefault()
        {
            var entries = new Dictionary<byte, Variant[]>();

            void Add(SspEvent code, byte firstVersion, SspEventPayload payload) =>
                entries[(byte)code] = new[] { new Variant(new SspProtocolVersion(firstVersion), payload) };

            void AddVersioned(SspEvent code, params (byte Version, SspEventPayload Payload)[] variants) =>
                entries[(byte)code] = variants
                    .Select(v => new Variant(new SspProtocolVersion(v.Version), v.Payload))
                    .OrderBy(v => v.FirstVersion.Value)
                    .ToArray();

            // Blocks repeated once per currency in the device's dataset.  A payout block reports
            // both what moved and what was asked for; a value block reports one amount.
            const int ValueBlock = 4 + 3;               // value, then a three-letter country code
            const int PayoutBlock = 4 + 4 + 3;          // value moved, value requested, country code

            Add(SspEvent.SlaveReset, 4, SspEventPayload.None);
            AddVersioned(SspEvent.Read, (4, SspEventPayload.Fixed(1)), (9, SspEventPayload.Fixed(7)));
            AddVersioned(SspEvent.NoteCredit, (4, SspEventPayload.Fixed(1)), (9, SspEventPayload.Fixed(7)));
            Add(SspEvent.Rejecting, 4, SspEventPayload.None);
            Add(SspEvent.Rejected, 4, SspEventPayload.None);
            Add(SspEvent.Stacked, 4, SspEventPayload.None);
            Add(SspEvent.SafeJam, 4, SspEventPayload.None);
            Add(SspEvent.UnsafeJam, 4, SspEventPayload.None);
            Add(SspEvent.Disabled, 4, SspEventPayload.None);
            Add(SspEvent.StackerFull, 4, SspEventPayload.None);
            Add(SspEvent.FraudAttempt, 4, SspEventPayload.Fixed(1));
            Add(SspEvent.BarcodeTicketValidated, 4, SspEventPayload.None);
            Add(SspEvent.CashboxReplaced, 5, SspEventPayload.None);
            Add(SspEvent.CashboxRemoved, 5, SspEventPayload.None);
            Add(SspEvent.NoteClearedIntoCashbox, 5, SspEventPayload.Fixed(1));
            Add(SspEvent.NoteClearedFromFront, 4, SspEventPayload.Fixed(1));
            Add(SspEvent.NotePathOpen, 6, SspEventPayload.None);
            AddVersioned(SspEvent.CoinCredit, (5, SspEventPayload.Fixed(4)), (6, SspEventPayload.Fixed(7)));
            AddVersioned(SspEvent.CashboxPaid,
                (5, SspEventPayload.Fixed(4)), (6, SspEventPayload.CountPrefixed(ValueBlock)));
            AddVersioned(SspEvent.IncompleteFloat,
                (5, SspEventPayload.Fixed(8)), (6, SspEventPayload.CountPrefixed(PayoutBlock)));
            AddVersioned(SspEvent.IncompletePayout,
                (4, SspEventPayload.Fixed(8)), (6, SspEventPayload.CountPrefixed(PayoutBlock)));
            AddVersioned(SspEvent.NoteStoredInPayout,
                (4, SspEventPayload.None), (6, SspEventPayload.Fixed(8)));
            AddVersioned(SspEvent.Dispensing,
                (4, SspEventPayload.Fixed(4)), (6, SspEventPayload.CountPrefixed(ValueBlock)));
            AddVersioned(SspEvent.Timeout,
                (4, SspEventPayload.Fixed(4)), (6, SspEventPayload.CountPrefixed(ValueBlock)));
            AddVersioned(SspEvent.Floated,
                (4, SspEventPayload.Fixed(4)), (6, SspEventPayload.CountPrefixed(ValueBlock)));
            AddVersioned(SspEvent.Floating,
                (4, SspEventPayload.Fixed(4)), (6, SspEventPayload.CountPrefixed(ValueBlock)));
            AddVersioned(SspEvent.Halted,
                (4, SspEventPayload.Fixed(4)), (6, SspEventPayload.CountPrefixed(ValueBlock)));
            AddVersioned(SspEvent.HopperJammed,
                (5, SspEventPayload.Fixed(4)), (6, SspEventPayload.CountPrefixed(ValueBlock)));
            AddVersioned(SspEvent.Dispensed,
                (4, SspEventPayload.Fixed(4)), (6, SspEventPayload.CountPrefixed(ValueBlock)));
            Add(SspEvent.BarcodeTicketAck, 4, SspEventPayload.None);
            Add(SspEvent.DeviceFull, 5, SspEventPayload.None);
            Add(SspEvent.NoteHeldInBezel, 8, SspEventPayload.Fixed(7));
            Add(SspEvent.NoteDispensedAtReset, 6, SspEventPayload.Fixed(7));
            Add(SspEvent.Stacking, 4, SspEventPayload.None);
            Add(SspEvent.NoteIntoStoreAtReset, 8, SspEventPayload.Fixed(7));
            Add(SspEvent.NoteIntoStackerAtReset, 8, SspEventPayload.Fixed(7));
            Add(SspEvent.NoteTransferedToStacker, 6, SspEventPayload.Fixed(7));
            Add(SspEvent.NoteFloatAttached, 5, SspEventPayload.None);
            Add(SspEvent.NoteFloatRemoved, 5, SspEventPayload.None);
            Add(SspEvent.PayoutOutOfService, 4, SspEventPayload.None);
            Add(SspEvent.CoinMechReturnActive, 5, SspEventPayload.None);
            Add(SspEvent.CoinMechJammed, 5, SspEventPayload.None);
            Add(SspEvent.Emptied, 5, SspEventPayload.None);
            Add(SspEvent.Emptying, 5, SspEventPayload.None);
            Add(SspEvent.ValueAdded, 7, SspEventPayload.CountPrefixed(ValueBlock));
            Add(SspEvent.AttachedCoinMechEnabled, 6, SspEventPayload.None);
            Add(SspEvent.AttachedCoinMechDisabled, 6, SspEventPayload.None);
            Add(SspEvent.CoinMechError, 7, SspEventPayload.Fixed(1));
            Add(SspEvent.Initialising, 7, SspEventPayload.None);
            Add(SspEvent.ChannelDisable, 7, SspEventPayload.None);
            AddVersioned(SspEvent.SmartEmptied,
                (5, SspEventPayload.Fixed(4)), (6, SspEventPayload.CountPrefixed(ValueBlock)));
            AddVersioned(SspEvent.SmartEmptying,
                (5, SspEventPayload.Fixed(4)), (6, SspEventPayload.CountPrefixed(ValueBlock)));

            // Alone among the count-prefixed events, this one ends with a byte saying what failed.
            Add(SspEvent.ErrorDuringPayout, 7, SspEventPayload.CountPrefixed(ValueBlock, trailerSize: 1));

            Add(SspEvent.JamRecovery, 7, SspEventPayload.None);
            Add(SspEvent.PrintedToCashbox, 6, SspEventPayload.None);
            Add(SspEvent.PrintHalted, 6, SspEventPayload.None);
            Add(SspEvent.TicketInBezel, 6, SspEventPayload.None);
            Add(SspEvent.PaperReplaced, 6, SspEventPayload.None);
            Add(SspEvent.NoPaper, 6, SspEventPayload.None);
            Add(SspEvent.TicketPathClosed, 6, SspEventPayload.None);
            Add(SspEvent.PrinterHeadReplaced, 6, SspEventPayload.None);
            Add(SspEvent.TicketPrintingError, 6, SspEventPayload.Fixed(1));
            Add(SspEvent.TicketPrinted, 6, SspEventPayload.None);
            Add(SspEvent.TicketPrinting, 6, SspEventPayload.None);
            Add(SspEvent.TicketJam, 6, SspEventPayload.None);
            Add(SspEvent.TicketPathOpen, 6, SspEventPayload.None);
            Add(SspEvent.PrinterHeadRemoved, 6, SspEventPayload.None);
            Add(SspEvent.TicketsReplaced, 6, SspEventPayload.None);
            Add(SspEvent.TicketsLow, 6, SspEventPayload.None);
            Add(SspEvent.CashboxUnlockEnabled, 6, SspEventPayload.None);
            Add(SspEvent.CashboxBackInService, 6, SspEventPayload.None);
            Add(SspEvent.CashboxTamper, 4, SspEventPayload.None);
            Add(SspEvent.CashboxOutOfService, 6, SspEventPayload.Fixed(1));
            Add(SspEvent.CalibrationFailed, 7, SspEventPayload.Fixed(1));

            // The protocol manual names these events but gives no payload size for them, and
            // prints no worked packet to read one off.  Guessing zero would be a coin flip that
            // desynchronises the rest of the reply when it lost, so they are declared unknown:
            // a decode stops at one and says so.  A host that knows better can supply the size
            // with WithEvent.
            Add(SspEvent.CoinsLow, 4, SspEventPayload.Unknown);
            Add(SspEvent.MaintenanceRequired, 4, SspEventPayload.Unknown);
            Add(SspEvent.CoinRejected, 4, SspEventPayload.Unknown);
            Add(SspEvent.TicketInBezelAtStartup, 4, SspEventPayload.Unknown);
            Add(SspEvent.EscrowActive, 4, SspEventPayload.Unknown);

            return new SspEventTable(entries);
        }
    }
}

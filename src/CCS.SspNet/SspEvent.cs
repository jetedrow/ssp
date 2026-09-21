namespace CCS.SspNet
{
    /// <summary>
    /// The events a device can report in a poll reply.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A poll reply carries a run of these codes, each followed by however many data bytes that
    /// event is defined to carry.  The count varies with the device's protocol version, so the
    /// codes alone are not enough to read a reply —
    /// <see cref="Protocol.SspEventTable"/> holds the lengths and
    /// <see cref="Protocol.SspPollDecoder"/> walks the reply.
    /// </para>
    /// <para>
    /// No device sends all of these.  Which subset a device can send is a property of the device
    /// and its dataset, not of the protocol version.
    /// </para>
    /// <para>
    /// A code absent from this enum is not an error.  Devices newer than this library will send
    /// codes it has never heard of, which is why the decoder works off a table that can be extended
    /// rather than off this enum.
    /// </para>
    /// </remarks>
    public enum SspEvent : byte
    {
        /// <summary>The device has finished its power-up or reset sequence.</summary>
        SlaveReset = 0xF1,

        /// <summary>
        /// A note is being scanned, or has been validated and is held in escrow.  A zero payload
        /// means scanning is still in progress; anything else identifies the note.
        /// </summary>
        Read = 0xEF,

        /// <summary>
        /// A note has moved somewhere the user can no longer reach it.  This is the point at which
        /// a host should credit it.
        /// </summary>
        NoteCredit = 0xEE,

        /// <summary>A note is on its way back out of the bezel.</summary>
        Rejecting = 0xED,

        /// <summary>A note has been returned to the user.</summary>
        Rejected = 0xEC,

        /// <summary>A note has reached the stacker.</summary>
        Stacked = 0xEB,

        /// <summary>
        /// A note has jammed somewhere the user may still be able to reach it.
        /// </summary>
        UnsafeJam = 0xE9,

        /// <summary>The device is disabled and will not accept anything.</summary>
        Disabled = 0xE8,

        /// <summary>The stacker has taken as many notes as it holds.</summary>
        StackerFull = 0xE7,

        /// <summary>The device believes it is being manipulated to register credit for nothing.</summary>
        FraudAttempt = 0xE6,

        /// <summary>A barcode ticket has been read and is held in escrow.</summary>
        BarcodeTicketValidated = 0xE5,

        /// <summary>The cashbox has been put back in place.</summary>
        CashboxReplaced = 0xE4,

        /// <summary>The cashbox has been taken out.</summary>
        CashboxRemoved = 0xE3,

        /// <summary>A note found in the stack path at power-up has been moved into the cashbox.</summary>
        NoteClearedIntoCashbox = 0xE2,

        /// <summary>A note found in the note path at power-up has been pushed back out of the bezel.</summary>
        NoteClearedFromFront = 0xE1,

        /// <summary>The note path has been opened.</summary>
        NotePathOpen = 0xE0,

        /// <summary>A coin has been accepted and its value is in the payload.</summary>
        CoinCredit = 0xDF,

        /// <summary>Notes have been moved from the payout store into the cashbox.</summary>
        CashboxPaid = 0xDE,

        /// <summary>A float was interrupted, and the payload gives what was moved against what was asked for.</summary>
        IncompleteFloat = 0xDD,

        /// <summary>A payout was interrupted, and the payload gives what was paid against what was asked for.</summary>
        IncompletePayout = 0xDC,

        /// <summary>A note has been routed into the payout store rather than the cashbox.</summary>
        NoteStoredInPayout = 0xDB,

        /// <summary>A payout is under way; the payload gives the value paid so far.</summary>
        Dispensing = 0xDA,

        /// <summary>The device gave up on a request; the payload gives what it managed first.</summary>
        Timeout = 0xD9,

        /// <summary>A float has finished; the payload gives the value moved.</summary>
        Floated = 0xD8,

        /// <summary>A float is under way; the payload gives the value moved so far.</summary>
        Floating = 0xD7,

        /// <summary>A payout stopped part-way; the payload gives the value paid first.</summary>
        Halted = 0xD6,

        /// <summary>The hopper has jammed; the payload gives the value moved before it did.</summary>
        HopperJammed = 0xD5,

        /// <summary>The hopper is running out of coins.</summary>
        CoinsLow = 0xD3,

        /// <summary>A payout has finished; the payload gives the value paid.</summary>
        Dispensed = 0xD2,

        /// <summary>A barcode ticket has been moved somewhere safe.</summary>
        BarcodeTicketAck = 0xD1,

        /// <summary>The device has no room for anything more.</summary>
        DeviceFull = 0xCF,

        /// <summary>A note has been paid out and is waiting in the bezel to be taken.</summary>
        NoteHeldInBezel = 0xCE,

        /// <summary>A note left in the payout at power-up was paid out during the reset.</summary>
        NoteDispensedAtReset = 0xCD,

        /// <summary>The stacker path was cleared at power-up.</summary>
        Stacking = 0xCC,

        /// <summary>A note found at power-up was moved into the payout store.</summary>
        NoteIntoStoreAtReset = 0xCB,

        /// <summary>A note found at power-up was moved into the stacker.</summary>
        NoteIntoStackerAtReset = 0xCA,

        /// <summary>A note has been moved out of the payout store and into the stacker.</summary>
        NoteTransferedToStacker = 0xC9,

        /// <summary>A note float unit has been fitted.</summary>
        NoteFloatAttached = 0xC8,

        /// <summary>A note float unit has been taken off.</summary>
        NoteFloatRemoved = 0xC7,

        /// <summary>The payout unit cannot be used.</summary>
        PayoutOutOfService = 0xC6,

        /// <summary>The attached coin mechanism's return lever has been pressed.</summary>
        CoinMechReturnActive = 0xC5,

        /// <summary>The attached coin mechanism has jammed.</summary>
        CoinMechJammed = 0xC4,

        /// <summary>Emptying the payout store into the cashbox has finished.</summary>
        Emptied = 0xC3,

        /// <summary>The payout store is being emptied into the cashbox.</summary>
        Emptying = 0xC2,

        /// <summary>The device is asking to be serviced.</summary>
        MaintenanceRequired = 0xC0,

        /// <summary>Money has been added to the payout store; the payload gives what and how much.</summary>
        ValueAdded = 0xBF,

        /// <summary>The attached coin mechanism has been enabled.</summary>
        AttachedCoinMechEnabled = 0xBE,

        /// <summary>The attached coin mechanism has been disabled.</summary>
        AttachedCoinMechDisabled = 0xBD,

        /// <summary>A coin was not recognised and has been returned.</summary>
        CoinRejected = 0xBA,

        /// <summary>The attached coin mechanism has reported a fault.</summary>
        CoinMechError = 0xB7,

        /// <summary>The device is powering up and is not yet ready.</summary>
        Initialising = 0xB6,

        /// <summary>Every channel is inhibited, so nothing can be accepted.</summary>
        ChannelDisable = 0xB5,

        /// <summary>A smart empty has finished; the payload gives the value moved.</summary>
        SmartEmptied = 0xB4,

        /// <summary>A smart empty is under way; the payload gives the value moved so far.</summary>
        SmartEmptying = 0xB3,

        /// <summary>
        /// A payout went wrong.  The payload gives what was paid and ends with a code saying why.
        /// </summary>
        ErrorDuringPayout = 0xB1,

        /// <summary>The device is clearing a jam by itself.</summary>
        JamRecovery = 0xB0,

        /// <summary>A printed ticket was moved into the cashbox.</summary>
        PrintedToCashbox = 0xAF,

        /// <summary>Printing has stopped part-way.</summary>
        PrintHalted = 0xAE,

        /// <summary>A ticket is waiting in the bezel to be taken.</summary>
        TicketInBezel = 0xAD,

        /// <summary>Paper has been reloaded.</summary>
        PaperReplaced = 0xAC,

        /// <summary>The printer has run out of paper.</summary>
        NoPaper = 0xAB,

        /// <summary>The ticket path has been closed.</summary>
        TicketPathClosed = 0xAA,

        /// <summary>The print head has been refitted.</summary>
        PrinterHeadReplaced = 0xA9,

        /// <summary>Printing failed; the payload says why.</summary>
        TicketPrintingError = 0xA8,

        /// <summary>A ticket was already in the bezel when the device powered up.</summary>
        TicketInBezelAtStartup = 0xA7,

        /// <summary>A ticket has finished printing.</summary>
        TicketPrinted = 0xA6,

        /// <summary>A ticket is being printed.</summary>
        TicketPrinting = 0xA5,

        /// <summary>A ticket has jammed.</summary>
        TicketJam = 0xA4,

        /// <summary>The ticket path has been opened.</summary>
        TicketPathOpen = 0xA3,

        /// <summary>The print head has been taken out.</summary>
        PrinterHeadRemoved = 0xA2,

        /// <summary>Tickets have been reloaded.</summary>
        TicketsReplaced = 0xA1,

        /// <summary>The ticket supply is running out.</summary>
        TicketsLow = 0xA0,

        /// <summary>The cashbox may now be unlocked.</summary>
        CashboxUnlockEnabled = 0x93,

        /// <summary>The cashbox is usable again.</summary>
        CashboxBackInService = 0x92,

        /// <summary>The cashbox cannot be used; the payload says why.</summary>
        CashboxOutOfService = 0x90,

        /// <summary>The coin escrow is open.</summary>
        EscrowActive = 0x8B,

        /// <summary>The device could not calibrate itself; the payload says why.</summary>
        CalibrationFailed = 0x83,

        /// <summary>
        /// A note has jammed somewhere the user cannot reach it.
        /// </summary>
        /// <remarks>
        /// Issue 2.2 of the protocol manual does not list this event, but devices in the field do
        /// send it and earlier issues define it.  It is kept here, and in
        /// <see cref="Protocol.SspEventTable.Default"/>, so that a reply carrying it still decodes.
        /// </remarks>
        SafeJam = 0xEA,

        /// <summary>
        /// The cashbox has been interfered with.
        /// </summary>
        /// <remarks>
        /// As with <see cref="SafeJam"/>, this is not in issue 2.2 of the protocol manual but is
        /// defined by earlier issues and kept for compatibility.
        /// </remarks>
        CashboxTamper = 0x91,
    }
}

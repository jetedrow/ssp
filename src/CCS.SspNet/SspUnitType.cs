namespace CCS.SspNet
{
    /// <summary>
    /// The kind of device, as the first byte of a setup request reply reports it.
    /// </summary>
    /// <remarks>
    /// The rest of a setup reply is laid out differently for each of these, which is why
    /// <see cref="Protocol.SspSetup"/> keeps the raw bytes alongside anything it parses.
    /// </remarks>
    public enum SspUnitType : byte
    {
        /// <summary>A banknote validator, with or without a payout unit fitted.</summary>
        BanknoteValidator = 0x00,

        /// <summary>A coin hopper.</summary>
        SmartHopper = 0x03,

        /// <summary>A banknote validator with a payout unit.</summary>
        SmartPayout = 0x06,

        /// <summary>The note float fitted to an NV11.</summary>
        NoteFloat = 0x07,

        /// <summary>A printer fitted to a validator.</summary>
        AddonPrinter = 0x08,

        /// <summary>A coin system.</summary>
        SmartSystem = 0x09,

        /// <summary>A printer that stands on its own rather than being fitted to a validator.</summary>
        StandAlonePrinter = 0x0B,

        /// <summary>A tamper evident banknote system.</summary>
        Tebs = 0x0D,

        /// <summary>A tamper evident banknote system with a payout unit.</summary>
        TebsWithSmartPayout = 0x0E,

        /// <summary>A tamper evident banknote system with a printer.</summary>
        TebsWithSmartTicket = 0x0F,
    }
}

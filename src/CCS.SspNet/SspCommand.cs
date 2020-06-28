using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace CCS.SspNet
{
#pragma warning disable CA1027 // Mark enums with FlagsAttribute
    public enum SspCommand : byte
#pragma warning restore CA1027 // Mark enums with FlagsAttribute
    {
        [Display(Name = "Reset", Description = "Reset the device.")]
        Reset = 0x01,

        [Display(Name = "Set Channel Inhibit", Description = "Set the bit low to inhibit all note acceptance on that channel, high to allow note acceptance.")]
        SetChannelInhibits = 0x02,

        [Display(Name = "Display On", Description = "Allows the host to control the illumination of the bezel.")]
        DisplayOn = 0x03,

        [Display(Name = "Display Off", Description = "Allows the host to control the illumination of the bezel.")]
        DisplayOff = 0x04,

        [Display(Name = "Setup Request", Description = "Request the set­up configuration of the device. (Each device type has a different return data format. )")]
        SetupRequest = 0x05,

        [Display(Name = "Host Protocol Version", Description = "Sets the device's SSP protocol version.")]
        HostProtocolVersion = 0x06,

        [Display(Name = "Poll", Description = "Polls the device for events that have happened since the last poll request.")]
        Poll = 0x07,

        [Display(Name = "Reject Banknote", Description = "After a banknote validator device reports a valid note is held in escrow, this command may be sent to cause the banknote to be rejected back to the user.")]
        RejectBanknote = 0x08,

        [Display(Name = "Disable", Description = "Changes the device to its disabled state.")]
        Disable = 0x09,

        [Display(Name = "Enable", Description = "Changes the device to its enabled state.")]
        Enable = 0x0A,

        [Display(Name = "Program Firmware/Dataset", Description = "Allows the firmware or note data to be updated by the host machine.")]
        ProgramFirmware = 0x0B,

        [Display(Name = "Get Serial Number", Description = "Gets the device's serial number.")]
        GetSerialNumber = 0x0C,

        [Display(Name = "Unit Data", Description = "A command to return version information about the connected device.")]
        UnitData = 0x0D,

        [Display(Name = "Channel Value Data", Description = "Returns channel value data for a banknote validator. Note that this will differ depeneind on the protocl version used/supported.")]
        ChannelValueData = 0x0E,

        [Display(Name = "Channel Security Data", Description = "Command which returns a number of channels and then 1 to n bytes which give the security of each channel.")]
        ChannelSecurityData = 0x0F,

        [Display(Name = "Channel Reteach Data", Description = "Provides ability to re-teach bill data on a channel.")]
        ChannelReteachData = 0x10,

        [Display(Name = "Synchronization", Description = "Resets the sync bit that makes up the synchronization to 0, commonly used to detect if a device is present.")]
        Sync = 0x11,

        [Display(Name = "Last Reject Code", Description = "Returns a one byte code representing the reason the BNV rejected the last note.")]
        LastRejectCode = 0x17,

        [Display(Name = "Hold", Description = "Hold the current note in escrow, by resetting escrow timer (10 seconds).")]
        Hold = 0x18,

        [Display(Name = "Get Firmware Version", Description = "Returns the detailed information about the current firmware installed in the device.")]
        GetFirmwareVersion = 0x20,

        [Display(Name = "Get Dataset Version", Description = "Returns the detailed information about the current dataset (note/bill data) installed in the device.")]
        GetDatasetVersion = 0x21,

        [Display(Name = "Get All Levels", Description = "Use this command to return all the stored levels of denominations in the device (including those at zero level).")]
        GetAllLevels = 0x22,

        [Display(Name = "Get Barcode Reader Configuration", Description = "Returns the set­up data for the device bar code readers.")]
        GetBarcodeReaderConfiguration = 0x23,

        [Display(Name = "Set Barcode Reader Configuration", Description = "This command allows the host to set­up the bar code reader(s) configuration on the device.")]
        SetBarcodeReaderConfiguration = 0x24,

        [Display(Name = "Get Barcode Inhibit Status", Description = "Command to return the current bar code/currency inhibit status.")]
        GetBarcodeReaderInhibitStatus = 0x25,

        [Display(Name = "Set Barcode Inhibit Status", Description = "Sets up the bar code inhibit status register.")]
        SetBarcodeReaderInhibitStatus = 0x26,

        [Display(Name = "Get Barcode Reader Data", Description = "Command to obtain last valid bar code ticket data, send in response to a bar code ticket validated event.")]
        GetBarcodeReaderData = 0x27,

        [Display(Name = "Set Refill Mode", Description = "A command sequence to set or reset the facility for the payout to reject notes that are routed to the payout store but the firmware determines that they are un­suitable for storage.")]
        SetRefillMode = 0x30,

        [Display(Name = "Payout Amount", Description = "A command to set the monetary value to be paid by the payout unit.")]
        PayoutAmount = 0x33,

        [Display(Name = "Set Denomination Level", Description = "A command to increment the level of coins of a denomination stored in the hopper.")]
        SetDenominationLevel = 0x34,

        [Display(Name = "Get Denomination Level", Description = "This command returns the level of a denomination stored in a payout device as a 2 byte value.")]
        GetDenominationLevel = 0x35,

        [Display(Name = "Communication Pass-through", Description = "The SMART Hopper includes two serial connections and this command enables the user to convert either of these into a USB to serial convertor so that the host can communicate directly with periferla connected to these ports.")]
        CommunicationPassThrough = 0x37,

        [Display(Name = "Halt Payout", Description = "A command to stop the execution of an existing payout. ")]
        HaltPayout = 0x38,

        [Display(Name = "Payout Amount By Denomination", Description = "This command is similar to 'Payout Amount' but has two values in the payout which you can select the denominations for each.")]
        PayoutAmountByDenomination = 0x39,

        [Display(Name = "Coin Escrow", Description = "Command to hold coins in the feeder without accepting into hopper.")]
        CoinEscrow = 0x3A,

        [Display(Name = "Set Denomination Route", Description = "This command will configure the denomination to be either routed to the cashbox on detection or stored to be made available for later possible payout.")]
        SetDenominationRoute = 0x3B,

        [Display(Name = "Get Denomination Route", Description = "This command allows the host to determine the route of a denomination.")]
        GetDenominationRoute = 0x3C,

        [Display(Name = "Float Amount", Description = "A command to float the payout unit to leave a requested value of money, with a requested minimum possible payout level.")]
        FloatAmount = 0x3D,

        [Display(Name = "Get Minimum Payout", Description = "A command to request the minimum possible payout amount that this device can provide.")]
        GetMinimumPayout = 0x3E,

        [Display(Name = "Empty All", Description = "This command will direct all stored monies to the cash box without reporting any value and reset all the stored counters to zero.")]
        EmptyAll = 0x3F,

        [Display(Name = "Set Coin Mech Inhibits", Description = "This command is used to enable or disable acceptance of individual coin values from a coin acceptor connected to the hopper.")]
        SetCoinMechInhibits = 0x40,

        [Display(Name = "Get Note Positions", Description = "This command will return the number of notes in the Note Float and the value in each position.")]
        GetNotePositions = 0x41,

        [Display(Name = "Payout Note", Description = "The Note Float will payout the last note that was stored.")]
        PayoutNote = 0x42,

        [Display(Name = "Stack Note", Description = "The Note Float will stack the last note that was stored.")]
        StackNote = 0x43,

        [Display(Name = "Float By Denomination", Description = "A command to float (leave in device) the requested quantity of individual denominations.")]
        FloatByDenomination = 0x44,

        [Display(Name = "Set Value Reporting Type", Description = "This will set the method of reporting values of notes.")]
        SetValueReportingType = 0x45,

        [Display(Name = "Payout By Denomination", Description = "A command to payout the requested quantity of individual denominations.")]
        PayoutByDenomination = 0x46,

        [Display(Name = "Coin Mech Global Inhibit", Description = "This command allows the host to enable/disable the attached coin mech in one command.")]
        SetCoinMechGlobalInhibit = 0x49,

        [Display(Name = "Set Generator", Description = "Part of the eSSP encryption negotiation sequence.")]
        SetGenerator = 0x4A,

        [Display(Name = "Set Modulus", Description = "Part of the eSSP encryption negotiation sequence.")]
        SetModulus = 0x4B,

        [Display(Name = "Request Key Exchange", Description = "Part of the eSSP encryption negotiation sequence.")]
        RequestKeyExchange = 0x4C,

        [Display(Name = "Set Baud Rate", Description = "This command has two data bytes to allow communication speed to be set on a device.")]
        SetBaudRate = 0x4D,

        [Display(Name = "Set Cashbox Payout Limit", Description = "Allow the host to specify a maximum level of coins, by denomination, to be left in the hopper.")]
        SetCashboxPayoutLimit = 0x4E,

        [Display(Name = "Get Build Revision", Description = "A command to return the build revision information of a device. ")]
        GetBuildRevision = 0x4F,

        [Display(Name = "Set Hopper Options", Description = "Set options for the Smart Hopper.  These options do not persist in memory.")]
        SetHopperOptions = 0x50,

        [Display(Name = "Get Hopper Options", Description = "Get options currently configured for the Smart Hopper.")]
        GetHopperOptions = 0x51,

        [Display(Name = "Smart Empty", Description = "Empties payout device of contents, maintaining a count of value emptied. ")]
        SmartEmpty = 0x52,

        [Display(Name = "Cashbox Payout Operation Data", Description = "Returns the amount emptied to cashbox from the payout in the last dispense, float or empty command.")]
        CashboxPayoutOperationData = 0x53,

        [Display(Name = "Configure Bezel", Description = "This command allows the host to configure a supported BNV bezel.")]
        ConfigureBezel = 0x54,

        [Display(Name = "Poll With Ack", Description = "A command that behaves in the same way as the Poll command but with this command, the specified events will need to be acknowledged by the host using the EVENT ACK command.")]
        PollWithAck = 0x56,

        [Display(Name = "Event Ack", Description = "This command will clear a repeating Poll ACK response and allow further note operations.")]
        EventAck = 0x57,

        [Display(Name = "Get Counters", Description = "A command to return a global note activity counter set for the slave device.")]
        GetCounters = 0x58,

        [Display(Name = "Reset Counters", Description = "Resets the note activity counters described in Get Counters command to all zero values.")]
        ResetCounters = 0x59,

        [Display(Name = "Coin Mech Options", Description = "Set options for the coin mech.  These options do not persist in memory.")]
        CoinMechOptions = 0x5A,

        [Display(Name = "Disable Payout Device", Description = "All accepted notes will be routed to the stacker and payout commands will not be accepted.")]
        DisablePayoutDevice = 0x5B,

        [Display(Name = "Enable Payout Device", Description = "A command to enable the attached payout device for storing/paying out notes.")]
        EnablePayoutDevice = 0x5C,

        [Display(Name = "Coin Stir", Description = "Mixes the coins by performs a rotation of the Coin Hopper Motor for a specifed time.")]
        CoinStir = 0x5D,

        [Display(Name = "Set Fixed Encryption Key", Description = "A command to allow the host to change the fixed part of the eSSP key.")]
        SetFixedEncryptionKey = 0x60,

        [Display(Name = "Reset Fixed Encryption Key", Description = "Resets the fixed encryption key to the device default.")]
        ResetFixedEncryptionKey = 0x61,

        [Display(Name = "Get Real Time Clock Configuration", Description = "Returns the configuration of the device RTC.")]
        GetRealTimeClockConfig = 0x62,

        [Display(Name = "Get Real Time Clock", Description = "Returns the device RTC date and time.")]
        GetRealTimeClock = 0x62,

        [Display(Name = "Set Real Time Clock Configuration", Description = "Sets the configuration of the device RTC.")]
        SetRealTimeClockConfig = 0x64,

        [Display(Name = "Get TEBS Barcode", Description = "This command is sent to the device to retrieve the barcode of the tamper evident cash bag.")]
        RequestTebsBarcode = 0x65,

        [Display(Name = "", Description = "")]
        RequestTebsLog = 0x66,

        [Display(Name = "Cashbox Unlock Enable", Description = "This command allows the TEBS device to be unlocked using the physical key.")]
        TebsUnlockEnable = 0x67,

        [Display(Name = "Cashbox Unlock Disable", Description = "This command stops the TEBS device from being unlocked using the physical key.")]
        TebsUnlockDisable = 0x68,

        [Display(Name = "Reset TEBS Logs", Description = "Reset the TEBS logs. All fields are reset to zero values.")]
        ResetTebsLogs = 0x69,

        [Display(Name = "Ticket Print", Description = "The Ticket Print command uses a system of sub commands to allow the host to send printer commands to the device.")]
        TicketPrint = 0x70,

        [Display(Name = "Printer Configuration", Description = "The Printer Configuration command uses a system of sub commands to allow the host to send printer configuration commands to the device.")]
        PrinterConfiguration = 0x71,

        [Display(Name = "Enable Tito Events", Description = "When communicating with the NV200 attached to the printer, optional additional poll events may be enabled.")]
        EnableTitoEvents = 0x72,

        [Display(Name = "Cancel Escrow Transaction", Description = "All notes In Escrow are Returned. **NOTE: A special version of firmware is required. **")]
        CancelEscrowTransaction = 0x76,

        [Display(Name = "Commit Escrow Transaction", Description = "Notes in Escrow are Committed. **NOTE: A special version of firmware is required. **")]
        CommitEscrowTransaction = 0x77,

        [Display(Name = "Read Escrow Value", Description = "Returns current Escrow Value. **NOTE: A special version of firmware is required. **")]
        ReadEscrowValue = 0x78,

        [Display(Name = "Get Escrow Size", Description = "Returns Escrow Spaces. **NOTE: A special version of firmware is required. **")]
        GetEscrowSize = 0x79,

        [Display(Name = "Set Escrow Size", Description = "Sets Escrow Spaces. **NOTE: A special version of firmware is required. **")]
        SetEscrowSize = 0x7A,

        //     [Display(Name = "", Description = "")]
    }
}

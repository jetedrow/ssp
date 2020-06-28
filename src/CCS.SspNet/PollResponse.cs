using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace CCS.SspNet
{
    public enum PollResponse : byte
    {
        [Display(Name = "", Description = "")]
        TebsCashboxOutOfService = 0x90,

        [Display(Name = "", Description = "")]
        TebsCashboxTamper = 0x91,

        [Display(Name = "", Description = "")]
        TebsCashboxInService = 0x92,

        [Display(Name = "", Description = "")]
        TebsCashboxUnlockEnabled = 0x93,

        [Display(Name = "", Description = "")]
        JamRecovery = 0xB0,

        [Display(Name = "", Description = "")]
        ErrorDuringPayout = 0xB1,

        [Display(Name = "", Description = "")]
        SmartEmptying = 0xB3,

        [Display(Name = "", Description = "")]
        SmartEmptied = 0xB4,

        [Display(Name = "Channel Disable", Description = "The device has had all its note channels inhibited and has become disabled for note insertion.")]
        ChannelDisable = 0xB5,

        [Display(Name = "Initalizing", Description = "This event is given only when using the Poll with ACK command. It is given when the BNV is powered up and setting its sensors and mechanisms to be ready for Note acceptance.")]
        Initalizing = 0xB6,

        [Display(Name = "", Description = "")]
        CoinMechError = 0xB7,

        [Display(Name = "", Description = "")]
        Emptying = 0xC2,

        [Display(Name = "", Description = "")]
        Emptied = 0xC3,

        [Display(Name = "", Description = "")]
        CoinMechJammed = 0xC4,

        [Display(Name = "", Description = "")]
        CoinMechReturnPressed = 0xC5,

        [Display(Name = "", Description = "")]
        PayoutOutOfService = 0xC6,

        [Display(Name = "", Description = "")]
        NoteFloatRemoved = 0xC7,

        [Display(Name = "", Description = "")]
        NoteFloatAttached = 0xC8,

        [Display(Name = "", Description = "")]
        NoteTransferredToStacker = 0xC9,

        [Display(Name = "", Description = "")]
        NotePaidIntoStackerAtPowerup = 0xCA,

        [Display(Name = "", Description = "")]
        NotePaidIntoStoreAtPoweup = 0xCB,

        [Display(Name = "Note Stacking", Description = "The bill is currently being transported to and through the device stacker.")]
        NoteStacking = 0xCC,

        [Display(Name = "", Description = "")]
        NoteDispensedAtPowerup = 0xCD,

        [Display(Name = "", Description = "")]
        NoteHeldInBezel = 0xCE,

        [Display(Name = "Barcode Ticket Ack", Description = "The device has moved the barcode ticket to a safe stack position.")]
        BarcodeTicketAcknowledge = 0xD1,

        [Display(Name = "Dispensed", Description = "Show the total value the device has dispensed in repsonse to a Dispense command.")]
        Dispensed = 0xD2,

        [Display(Name = "Coins Low", Description = "")]
        CoinsLow = 0xD3,

        [Display(Name = "Hopper Jammed", Description = "An event showing the hopper unit has jammed and giving the value paid/floated upto that jam.")]
        Jammed = 0xD5,

        [Display(Name = "Halted", Description = "Triggered when payout is interrupted for some reason.")]
        Halted = 0xD6,

        [Display(Name = "Floating", Description = "Event showing the amount of cash floated up to the poll point.")]
        Floating = 0xD7,

        [Display(Name = "Floated", Description = "Event given at the end of the floating process which will display the amount actually floated.")]
        Floated = 0xD8,

        [Display(Name = "Timeout", Description = "The device has been unable to complete a request. The value paid up until the time­out point is given in the event data.")]
        Timeout = 0xD9,

        [Display(Name = "Dispensing", Description = "The device is in the process of paying out a requested value. The value paid at the poll is given in the event data.")]
        Dispensing = 0xDA,

        [Display(Name = "", Description = "")]
        NoteStoredInPayout = 0xDB,

        [Display(Name = "", Description = "")]
        IncompletePayout = 0xDC,

        [Display(Name = "", Description = "")]
        IncompleteFloat = 0xDD,

        [Display(Name = "", Description = "")]
        CashboxPaid = 0xDE,

        [Display(Name = "", Description = "")]
        CoinCredit = 0xDF,

        [Display(Name = "Note Path Open", Description = "The device has detected that it's note path has been opened.")]
        NotePathOpen = 0xE0,

        [Display(Name = "Note Cleared From Front", Description = "During the device power­up sequence a bill was detected as being in the note path. This bill is then rejected from the device via the bezel and this event is issued.")]
        NoteClearedFromFront = 0xE1,

        [Display(Name = "Note Cleared Into Cashbox", Description = "During the device power­up sequence a bill was detected as being in the stack path. This bill is then moved into the device cashbox and this event is issued.")]
        NoteClearedToCashbox = 0xE2,

        [Display(Name = "Cashbox Removed", Description = "The system has detected that the cashbox unit has been removed from it's working position.")]
        CashboxRemoved = 0xE3,

        [Display(Name = "Cashbox Replaced", Description = "The device cashbox box unit has been detected as replaced into it's working position.")]
        CashboxReplaced = 0xE4,

        [Display(Name = "Barcode Ticket Validated", Description = "A barcode ticket has been scanned and identified by the system and is currently held in the escrow position.")]
        BarcodeTicketValidated = 0xE5,

        [Display(Name = "Fraud Attempt", Description = "The validator system has detected an attempt to mauipulate the coin/banknote in order to fool the system to register credits with no monies added.")]
        FraudAttempt = 0xE6,

        [Display(Name = "Stacker Full", Description = "Event in response to poll given when the device has detected that the stacker unit has stacked its full limit of banknotes.")]
        StackerFull = 0xE7,

        [Display(Name = "Disabled", Description = "A disabled event is given in response to a poll command when a device has been disabled by the host or by some other internal function of the device.")]
        Disabled = 0xE8,

        [Display(Name = "Unsafe Jam", Description = "A bill has been detected as jammed during it's transport through the validator. An unsafe jam indicates that this bill may be in a position when the user could retrieve it from the validator bezel.")]
        UnsafeNoteJam = 0xE9,

        [Display(Name = "", Description = "")]
        SafeNoteJam = 0xEA,

        [Display(Name = "Note Stacked", Description = "A bill has been transported trough the banknote validator and is in it's stacked position.")]
        NoteStacked = 0xEB,

        [Display(Name = "Note Rejected", Description = "A bill has been rejected back to the user by the Banknote Validator.")]
        NoteRejected = 0xEC,

        [Display(Name = "Note Rejecting", Description = "A bill is in the process of being rejected back to the user by the Banknte Validator.")]
        NoteRejecting = 0xED,

        [Display(Name = "Note Credit", Description = "This event is generated when the banknote has been moved from the escrow position to a safe postion within the validator system where the baknote cannot be retreived by the user.")]
        CreditNote = 0xEE,

        [Display(Name = "Read Note", Description = "An event given when the BNV is reading a banknote.")]
        ReadNote = 0xEF,

        [Display(Name = "Slave Reset", Description = "An event gven when the device has been powered up or power cycled and has run through its reset process.")]
        SlaveReset = 0xF1,
    }
}

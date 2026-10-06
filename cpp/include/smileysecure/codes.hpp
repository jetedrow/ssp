// SmileySecure — a portable C++ library for the Smiley Secure Protocol (SSP/eSSP).
// SPDX-License-Identifier: LGPL-3.0-or-later
#pragma once

#include <cstdint>

namespace smileysecure {

/// The commands a host can send.
///
/// A code missing from here is still sendable: every command call takes a raw byte as well, so
/// a device capability this library has not named stays reachable.
enum class Command : uint8_t {
    Reset = 0x01,
    SetChannelInhibits = 0x02,
    DisplayOn = 0x03,
    DisplayOff = 0x04,
    SetupRequest = 0x05,
    HostProtocolVersion = 0x06,
    Poll = 0x07,
    RejectBanknote = 0x08,
    Disable = 0x09,
    Enable = 0x0A,
    ProgramFirmware = 0x0B,
    GetSerialNumber = 0x0C,
    UnitData = 0x0D,
    ChannelValueData = 0x0E,
    ChannelSecurityData = 0x0F,
    ChannelReteachData = 0x10,
    Sync = 0x11,
    LastRejectCode = 0x17,
    Hold = 0x18,
    GetFirmwareVersion = 0x20,
    GetDatasetVersion = 0x21,
    GetAllLevels = 0x22,
    GetBarcodeReaderConfiguration = 0x23,
    SetBarcodeReaderConfiguration = 0x24,
    GetBarcodeReaderInhibitStatus = 0x25,
    SetBarcodeReaderInhibitStatus = 0x26,
    GetBarcodeReaderData = 0x27,
    SetRefillMode = 0x30,
    PayoutAmount = 0x33,
    SetDenominationLevel = 0x34,
    GetDenominationLevel = 0x35,
    CommunicationPassThrough = 0x37,
    HaltPayout = 0x38,
    PayoutAmountByDenomination = 0x39,
    CoinEscrow = 0x3A,
    SetDenominationRoute = 0x3B,
    GetDenominationRoute = 0x3C,
    FloatAmount = 0x3D,
    GetMinimumPayout = 0x3E,
    EmptyAll = 0x3F,
    SetCoinMechInhibits = 0x40,
    GetNotePositions = 0x41,
    PayoutNote = 0x42,
    StackNote = 0x43,
    FloatByDenomination = 0x44,
    SetValueReportingType = 0x45,
    PayoutByDenomination = 0x46,
    SetCoinMechGlobalInhibit = 0x49,
    SetGenerator = 0x4A,
    SetModulus = 0x4B,
    RequestKeyExchange = 0x4C,
    SetBaudRate = 0x4D,
    SetCashboxPayoutLimit = 0x4E,
    GetBuildRevision = 0x4F,
    SetHopperOptions = 0x50,
    GetHopperOptions = 0x51,
    SmartEmpty = 0x52,
    CashboxPayoutOperationData = 0x53,
    ConfigureBezel = 0x54,
    PollWithAck = 0x56,
    EventAck = 0x57,
    GetCounters = 0x58,
    ResetCounters = 0x59,
    CoinMechOptions = 0x5A,
    DisablePayoutDevice = 0x5B,
    EnablePayoutDevice = 0x5C,
    CoinStir = 0x5D,
    SetFixedEncryptionKey = 0x60,
    ResetFixedEncryptionKey = 0x61,
    GetRealTimeClockConfig = 0x62,
    GetRealTimeClock = 0x63,
    SetRealTimeClock = 0x64,
    RequestTebsBarcode = 0x65,
    RequestTebsLog = 0x66,
    TebsUnlockEnable = 0x67,
    TebsUnlockDisable = 0x68,
    ResetTebsLogs = 0x69,
    TicketPrint = 0x70,
    PrinterConfiguration = 0x71,
    EnableTitoEvents = 0x72,
    CancelEscrowTransaction = 0x76,
    CommitEscrowTransaction = 0x77,
    ReadEscrowValue = 0x78,
    GetEscrowSize = 0x79,
    SetEscrowSize = 0x7A,
};

/// The response code that opens every reply.
enum class Response : uint8_t {
    Ok = 0xF0,
    CommandNotKnown = 0xF2,
    WrongParameterCount = 0xF3,
    ParameterOutOfRange = 0xF4,
    UnprocessibleCommand = 0xF5,
    SoftwareError = 0xF6,
    Failure = 0xF8,
    /// Returned during a download when the 128-byte ITL header is not valid for this device.
    HeaderFailure = 0xF9,
    /// The device is in encrypted mode but no key has been negotiated.
    KeyNotSet = 0xFA,
};

/// The kind of device, as the first byte of a setup reply reports it.
enum class UnitType : uint8_t {
    BanknoteValidator = 0x00,
    SmartHopper = 0x03,
    SmartPayout = 0x06,
    NoteFloat = 0x07,
    AddonPrinter = 0x08,
    SmartSystem = 0x09,
    StandAlonePrinter = 0x0B,
    Tebs = 0x0D,
    TebsWithSmartPayout = 0x0E,
    TebsWithSmartTicket = 0x0F,
};

/// The events a device can report in a poll reply.
///
/// A code absent from here is not an error.  Devices newer than this library will send codes it
/// has never heard of, which is why the decoder works off an extendable `EventTable` rather
/// than off this enum.
enum class Event : uint8_t {
    SlaveReset = 0xF1,
    Read = 0xEF,
    NoteCredit = 0xEE,
    Rejecting = 0xED,
    Rejected = 0xEC,
    Stacked = 0xEB,
    UnsafeJam = 0xE9,
    Disabled = 0xE8,
    StackerFull = 0xE7,
    FraudAttempt = 0xE6,
    BarcodeTicketValidated = 0xE5,
    CashboxReplaced = 0xE4,
    CashboxRemoved = 0xE3,
    NoteClearedIntoCashbox = 0xE2,
    NoteClearedFromFront = 0xE1,
    NotePathOpen = 0xE0,
    CoinCredit = 0xDF,
    CashboxPaid = 0xDE,
    IncompleteFloat = 0xDD,
    IncompletePayout = 0xDC,
    NoteStoredInPayout = 0xDB,
    Dispensing = 0xDA,
    Timeout = 0xD9,
    Floated = 0xD8,
    Floating = 0xD7,
    Halted = 0xD6,
    HopperJammed = 0xD5,
    CoinsLow = 0xD3,
    Dispensed = 0xD2,
    BarcodeTicketAck = 0xD1,
    DeviceFull = 0xCF,
    NoteHeldInBezel = 0xCE,
    NoteDispensedAtReset = 0xCD,
    Stacking = 0xCC,
    NoteIntoStoreAtReset = 0xCB,
    NoteIntoStackerAtReset = 0xCA,
    NoteTransferedToStacker = 0xC9,
    NoteFloatAttached = 0xC8,
    NoteFloatRemoved = 0xC7,
    PayoutOutOfService = 0xC6,
    CoinMechReturnActive = 0xC5,
    CoinMechJammed = 0xC4,
    Emptied = 0xC3,
    Emptying = 0xC2,
    MaintenanceRequired = 0xC0,
    ValueAdded = 0xBF,
    AttachedCoinMechEnabled = 0xBE,
    AttachedCoinMechDisabled = 0xBD,
    CoinRejected = 0xBA,
    CoinMechError = 0xB7,
    Initialising = 0xB6,
    ChannelDisable = 0xB5,
    SmartEmptied = 0xB4,
    SmartEmptying = 0xB3,
    ErrorDuringPayout = 0xB1,
    JamRecovery = 0xB0,
    PrintedToCashbox = 0xAF,
    PrintHalted = 0xAE,
    TicketInBezel = 0xAD,
    PaperReplaced = 0xAC,
    NoPaper = 0xAB,
    TicketPathClosed = 0xAA,
    PrinterHeadReplaced = 0xA9,
    TicketPrintingError = 0xA8,
    TicketInBezelAtStartup = 0xA7,
    TicketPrinted = 0xA6,
    TicketPrinting = 0xA5,
    TicketJam = 0xA4,
    TicketPathOpen = 0xA3,
    PrinterHeadRemoved = 0xA2,
    TicketsReplaced = 0xA1,
    TicketsLow = 0xA0,
    CashboxUnlockEnabled = 0x93,
    CashboxBackInService = 0x92,
    CashboxOutOfService = 0x90,
    EscrowActive = 0x8B,
    CalibrationFailed = 0x83,
    SafeJam = 0xEA,
    CashboxTamper = 0x91,
};

/// The command's name, such as "Host Protocol Version", or nullptr for a code this library
/// does not name.
const char* command_name(uint8_t code) noexcept;

/// The response code's name, such as "Key Not Set", or nullptr for one this library does not
/// name.
const char* response_name(uint8_t code) noexcept;

/// The event's name, such as "Note Credit", or nullptr for a code this library does not name.
const char* event_name(uint8_t code) noexcept;

}  // namespace smileysecure

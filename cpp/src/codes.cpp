// SmileySecure — a portable C++ library for the Smiley Secure Protocol (SSP/eSSP).
// SPDX-License-Identifier: LGPL-3.0-or-later
#include "smileysecure/codes.hpp"

namespace smileysecure {

const char* command_name(uint8_t code) noexcept {
    switch (code) {
        case 0x01: return "Reset";
        case 0x02: return "Set Channel Inhibit";
        case 0x03: return "Display On";
        case 0x04: return "Display Off";
        case 0x05: return "Setup Request";
        case 0x06: return "Host Protocol Version";
        case 0x07: return "Poll";
        case 0x08: return "Reject Banknote";
        case 0x09: return "Disable";
        case 0x0A: return "Enable";
        case 0x0B: return "Program Firmware";
        case 0x0C: return "Get Serial Number";
        case 0x0D: return "Unit Data";
        case 0x0E: return "Channel Value Data";
        case 0x0F: return "Channel Security Data";
        case 0x10: return "Channel Reteach Data";
        case 0x11: return "Synchronization";
        case 0x17: return "Last Reject Code";
        case 0x18: return "Hold";
        case 0x20: return "Get Firmware Version";
        case 0x21: return "Get Dataset Version";
        case 0x22: return "Get All Levels";
        case 0x23: return "Get Barcode Reader Configuration";
        case 0x24: return "Set Barcode Reader Configuration";
        case 0x25: return "Get Barcode Inhibit Status";
        case 0x26: return "Set Barcode Inhibit Status";
        case 0x27: return "Get Barcode Reader Data";
        case 0x30: return "Set Refill Mode";
        case 0x33: return "Payout Amount";
        case 0x34: return "Set Denomination Level";
        case 0x35: return "Get Denomination Level";
        case 0x37: return "Communication Pass-through";
        case 0x38: return "Halt Payout";
        case 0x39: return "Payout Amount By Denomination";
        case 0x3A: return "Coin Escrow";
        case 0x3B: return "Set Denomination Route";
        case 0x3C: return "Get Denomination Route";
        case 0x3D: return "Float Amount";
        case 0x3E: return "Get Minimum Payout";
        case 0x3F: return "Empty All";
        case 0x40: return "Set Coin Mech Inhibits";
        case 0x41: return "Get Note Positions";
        case 0x42: return "Payout Note";
        case 0x43: return "Stack Note";
        case 0x44: return "Float By Denomination";
        case 0x45: return "Set Value Reporting Type";
        case 0x46: return "Payout By Denomination";
        case 0x49: return "Coin Mech Global Inhibit";
        case 0x4A: return "Set Generator";
        case 0x4B: return "Set Modulus";
        case 0x4C: return "Request Key Exchange";
        case 0x4D: return "Set Baud Rate";
        case 0x4E: return "Set Cashbox Payout Limit";
        case 0x4F: return "Get Build Revision";
        case 0x50: return "Set Hopper Options";
        case 0x51: return "Get Hopper Options";
        case 0x52: return "Smart Empty";
        case 0x53: return "Cashbox Payout Operation Data";
        case 0x54: return "Configure Bezel";
        case 0x56: return "Poll With Ack";
        case 0x57: return "Event Ack";
        case 0x58: return "Get Counters";
        case 0x59: return "Reset Counters";
        case 0x5A: return "Coin Mech Options";
        case 0x5B: return "Disable Payout Device";
        case 0x5C: return "Enable Payout Device";
        case 0x5D: return "Coin Stir";
        case 0x60: return "Set Fixed Encryption Key";
        case 0x61: return "Reset Fixed Encryption Key";
        case 0x62: return "Get Real Time Clock Configuration";
        case 0x63: return "Get Real Time Clock";
        case 0x64: return "Set Real Time Clock";
        case 0x65: return "Get TEBS Barcode";
        case 0x66: return "Request TEBS Log";
        case 0x67: return "Cashbox Unlock Enable";
        case 0x68: return "Cashbox Unlock Disable";
        case 0x69: return "Reset TEBS Logs";
        case 0x70: return "Ticket Print";
        case 0x71: return "Printer Configuration";
        case 0x72: return "Enable Tito Events";
        case 0x76: return "Cancel Escrow Transaction";
        case 0x77: return "Commit Escrow Transaction";
        case 0x78: return "Read Escrow Value";
        case 0x79: return "Get Escrow Size";
        case 0x7A: return "Set Escrow Size";
        default: return nullptr;
    }
}

const char* response_name(uint8_t code) noexcept {
    switch (code) {
        case 0xF0: return "OK";
        case 0xF2: return "Command Not Known";
        case 0xF3: return "Wrong Parameter Count";
        case 0xF4: return "Parameter Out Of Range";
        case 0xF5: return "Unprocessible Command";
        case 0xF6: return "Software Error";
        case 0xF8: return "Fail";
        case 0xF9: return "Header Failure";
        case 0xFA: return "Key Not Set";
        default: return nullptr;
    }
}

const char* event_name(uint8_t code) noexcept {
    switch (code) {
        case 0xF1: return "Slave Reset";
        case 0xEF: return "Read";
        case 0xEE: return "Note Credit";
        case 0xED: return "Rejecting";
        case 0xEC: return "Rejected";
        case 0xEB: return "Stacked";
        case 0xE9: return "Unsafe Jam";
        case 0xE8: return "Disabled";
        case 0xE7: return "Stacker Full";
        case 0xE6: return "Fraud Attempt";
        case 0xE5: return "Barcode Ticket Validated";
        case 0xE4: return "Cashbox Replaced";
        case 0xE3: return "Cashbox Removed";
        case 0xE2: return "Note Cleared Into Cashbox";
        case 0xE1: return "Note Cleared From Front";
        case 0xE0: return "Note Path Open";
        case 0xDF: return "Coin Credit";
        case 0xDE: return "Cashbox Paid";
        case 0xDD: return "Incomplete Float";
        case 0xDC: return "Incomplete Payout";
        case 0xDB: return "Note Stored In Payout";
        case 0xDA: return "Dispensing";
        case 0xD9: return "Timeout";
        case 0xD8: return "Floated";
        case 0xD7: return "Floating";
        case 0xD6: return "Halted";
        case 0xD5: return "Hopper Jammed";
        case 0xD3: return "Coins Low";
        case 0xD2: return "Dispensed";
        case 0xD1: return "Barcode Ticket Ack";
        case 0xCF: return "Device Full";
        case 0xCE: return "Note Held In Bezel";
        case 0xCD: return "Note Dispensed At Reset";
        case 0xCC: return "Stacking";
        case 0xCB: return "Note Into Store At Reset";
        case 0xCA: return "Note Into Stacker At Reset";
        case 0xC9: return "Note Transfered To Stacker";
        case 0xC8: return "Note Float Attached";
        case 0xC7: return "Note Float Removed";
        case 0xC6: return "Payout Out Of Service";
        case 0xC5: return "Coin Mech Return Active";
        case 0xC4: return "Coin Mech Jammed";
        case 0xC3: return "Emptied";
        case 0xC2: return "Emptying";
        case 0xC0: return "Maintenance Required";
        case 0xBF: return "Value Added";
        case 0xBE: return "Attached Coin Mech Enabled";
        case 0xBD: return "Attached Coin Mech Disabled";
        case 0xBA: return "Coin Rejected";
        case 0xB7: return "Coin Mech Error";
        case 0xB6: return "Initialising";
        case 0xB5: return "Channel Disable";
        case 0xB4: return "Smart Emptied";
        case 0xB3: return "Smart Emptying";
        case 0xB1: return "Error During Payout";
        case 0xB0: return "Jam Recovery";
        case 0xAF: return "Printed To Cashbox";
        case 0xAE: return "Print Halted";
        case 0xAD: return "Ticket In Bezel";
        case 0xAC: return "Paper Replaced";
        case 0xAB: return "No Paper";
        case 0xAA: return "Ticket Path Closed";
        case 0xA9: return "Printer Head Replaced";
        case 0xA8: return "Ticket Printing Error";
        case 0xA7: return "Ticket In Bezel At Startup";
        case 0xA6: return "Ticket Printed";
        case 0xA5: return "Ticket Printing";
        case 0xA4: return "Ticket Jam";
        case 0xA3: return "Ticket Path Open";
        case 0xA2: return "Printer Head Removed";
        case 0xA1: return "Tickets Replaced";
        case 0xA0: return "Tickets Low";
        case 0x93: return "Cashbox Unlock Enabled";
        case 0x92: return "Cashbox Back In Service";
        case 0x90: return "Cashbox Out Of Service";
        case 0x8B: return "Escrow Active";
        case 0x83: return "Calibration Failed";
        case 0xEA: return "Safe Jam";
        case 0x91: return "Cashbox Tamper";
        default: return nullptr;
    }
}

}  // namespace smileysecure

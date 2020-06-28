using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace CCS.SspNet
{
    public enum SspResponse : byte
    {
        [Display(Name = "OK", Description = "Returned when a command from the host is understood and has been, or is in the process of, being executed.")]
        OK = 0xF0,

        [Display(Name = "Command Not Known", Description = "Returned when an invalid command is received by a peripheral.")]
        CommandNotKnown = 0xF2,

        [Display(Name = "Wrong Parameter Count", Description = "A command was received by a peripheral, but an incorrect number of parameters were received.")]
        WrongParameterCount = 0xF3,

        [Display(Name = "Parameter Out of Range", Description = "One of the parameters sent with a command is out of range.")]
        ParameterOutOfRange = 0xF4,

        [Display(Name = "Unprocessible Command", Description = "The command sent could not be processed at this time (i.e. sending a dispense command before the last dispense operation has completed).")]
        UnprocessibleCommand = 0xF5,

        [Display(Name = "Software Error", Description = "Reported for errors in the execution of software (i.e. divide by zero). This may also be reported if there is a problem resulting from a failed firmware upgrade. If so, the firmware upgrade should be re-done.")]
        SoftwareError = 0xF6,

        [Display(Name = "Failure", Description = "Command failure.")]
        Failure = 0xF8,

        [Display(Name = "Key Not Set", Description = "The device is in encrypted communication mode, but the encryption keys have not been negotiated.")]
        KeyNotSet = 0xFA

    }
}

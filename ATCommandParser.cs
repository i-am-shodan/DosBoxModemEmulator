using Microsoft.Extensions.Logging;

namespace DosBoxModemEmulator;

public enum ModemState
{
    Command,
    DialTone,
    Dialing,
    Ringing,
    Connected,
    Busy
}

public class ATCommandParser
{
    private readonly ModemConfig _config;
    private readonly ILogger<ATCommandParser> _logger;

    public ATCommandParser(ModemConfig config, ILogger<ATCommandParser> logger)
    {
        _config = config;
        _logger = logger;
    }

    public (string Response, ModemCommand? Command) ParseCommand(string input)
    {
        input = input.Trim().ToUpperInvariant();

        // Echo back the command
        if (string.IsNullOrWhiteSpace(input))
        {
            return (string.Empty, null);
        }

        // Log the incoming AT command
        _logger.LogInformation("AT Command received: {Command}", input);

        // Handle +++ escape sequence
        if (input == "+++" || input.StartsWith("+++AT"))
        {
            // If there's a command after +++, process it
            if (input.StartsWith("+++AT"))
            {
                // Extract the command after +++
                input = input.Substring(3);
            }
            else
            {
                return ("OK\r\n", new ModemCommand { Type = CommandType.Escape });
            }
        }

        // Handle AT commands
        if (!input.StartsWith("AT"))
        {
            return ("ERROR\r\n", null);
        }

        // Remove AT prefix
        var command = input.Substring(2);

        // Parse compound commands (e.g., AT S7=90 S0=0 V1 X4)
        var modemCommand = ParseCompoundCommand(command);

        // ATZ - Reset modem
        if (command == "Z" || command == "Z0")
        {
            return ("OK\r\n", new ModemCommand { Type = CommandType.Reset });
        }

        // ATE0/ATE1 - Echo off/on
        if (command == "E0" || command == "E1")
        {
            return ("OK\r\n", new ModemCommand { Type = CommandType.Echo, Parameter = command });
        }

        // ATV0/ATV1 - Verbose mode off/on
        if (command == "V0" || command == "V1")
        {
            return ("OK\r\n", new ModemCommand { Type = CommandType.Verbose, Parameter = command });
        }

        // ATQ0/ATQ1 - Quiet mode off/on
        if (command == "Q0" || command == "Q1")
        {
            return ("OK\r\n", new ModemCommand { Type = CommandType.Quiet, Parameter = command });
        }

        // ATH - Hang up
        if (command == "H" || command == "H0")
        {
            return ("OK\r\n", new ModemCommand { Type = CommandType.Hangup });
        }

        // ATD - Dial
        if (command.StartsWith("D") || command.StartsWith("DT") || command.StartsWith("DP"))
        {
            var number = ExtractPhoneNumber(command);
            return ("OK\r\n", new ModemCommand { Type = CommandType.Dial, Parameter = number });
        }

        // ATA - Answer
        if (command == "A")
        {
            return ("OK\r\n", new ModemCommand { Type = CommandType.Answer });
        }

        // ATI - Information
        if (command.StartsWith("I"))
        {
            return ("DosBox Modem Emulator v1.0\r\nOK\r\n", null);
        }

        // ATX - Extended result codes
        if (command.StartsWith("X"))
        {
            return ("OK\r\n", null);
        }

        // ATS - S-registers (just return OK for common ones)
        if (command.StartsWith("S"))
        {
            return ("OK\r\n", modemCommand);
        }

        // AT&F - Factory defaults
        if (command == "&F" || command == "&F0")
        {
            return ("OK\r\n", new ModemCommand { Type = CommandType.Reset });
        }

        // AT&D - DTR handling
        if (command.StartsWith("&D"))
        {
            return ("OK\r\n", null);
        }

        // AT&C - DCD handling  
        if (command.StartsWith("&C"))
        {
            return ("OK\r\n", null);
        }

        // AT+++ - Escape to command mode
        if (input == "+++")
        {
            return ("OK\r\n", new ModemCommand { Type = CommandType.Escape });
        }

        // ATO - Return to online mode
        if (command == "O" || command == "O0")
        {
            return ("CONNECT 57600\r\n", new ModemCommand { Type = CommandType.Online });
        }

        // Unknown command
        return ("OK\r\n", null);
    }

    private ModemCommand? ParseCompoundCommand(string command)
    {
        // Parse commands like "S7=90 S0=0 V1 X4" that contain multiple parts
        // Look for specific command types that may be in a compound command
        
        // Check for hangup in compound commands
        if (command.Contains("H0") || command.EndsWith("H"))
        {
            return new ModemCommand { Type = CommandType.Hangup };
        }

        // For other compound commands, just acknowledge with OK
        return null;
    }

    private string ExtractPhoneNumber(string dialCommand)
    {
        // Remove D, DT, or DP prefix
        var number = dialCommand;
        if (number.StartsWith("DT"))
        {
            number = number.Substring(2);
        }
        else if (number.StartsWith("DP"))
        {
            number = number.Substring(2);
        }
        else if (number.StartsWith("D"))
        {
            number = number.Substring(1);
        }

        // Clean up common dial modifiers
        number = number.Replace("W", string.Empty); // Wait for dial tone
        number = number.Replace("P", string.Empty); // Pulse dial
        number = number.Replace("T", string.Empty); // Tone dial
        number = number.Replace(",", string.Empty); // Pause
        number = number.Replace(";", string.Empty); // Return to command mode

        return number.Trim();
    }
}

public class ModemCommand
{
    public CommandType Type { get; set; }
    public string Parameter { get; set; } = string.Empty;
}

public enum CommandType
{
    Reset,
    Dial,
    Answer,
    Hangup,
    Echo,
    Verbose,
    Quiet,
    Escape,
    Online
}

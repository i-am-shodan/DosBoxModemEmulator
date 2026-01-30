using Microsoft.Extensions.Logging;

namespace DosBoxModemEmulator;

public static class ConfigLoader
{
    public static ModemConfig LoadConfig(string configPath, ILogger logger)
    {
        try
        {
            var yaml = File.ReadAllText(configPath);
            return ParseYaml(yaml);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error loading config from {ConfigPath}", configPath);
            throw;
        }
    }

    private static ModemConfig ParseYaml(string yaml)
    {
        var config = new ModemConfig();
        var lines = yaml.Split('\n').Select(l => l.TrimEnd('\r')).ToArray();
        string? currentSection = null;
        PhonebookEntry? currentPhonebookEntry = null;

        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            
            // Skip empty lines and comments
            if (string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith('#'))
                continue;

            var trimmedLine = line.TrimStart();
            var indent = line.Length - trimmedLine.Length;

            // Root level sections
            if (indent == 0 && trimmedLine.EndsWith(':'))
            {
                currentSection = trimmedLine.TrimEnd(':');
                currentPhonebookEntry = null;
                continue;
            }

            // Parse based on current section
            if (currentSection == "config")
            {
                ParseConfigSection(trimmedLine, config.Config);
            }
            else if (currentSection == "phonebook")
            {
                if (trimmedLine.StartsWith("- "))
                {
                    // New phonebook entry
                    currentPhonebookEntry = new PhonebookEntry();
                    config.Phonebook.Add(currentPhonebookEntry);
                    
                    var keyValue = trimmedLine.Substring(2);
                    ParsePhonebookEntry(keyValue, currentPhonebookEntry);
                }
                else if (currentPhonebookEntry != null && indent >= 2)
                {
                    // Continuation of phonebook entry
                    ParsePhonebookEntry(trimmedLine, currentPhonebookEntry);
                }
            }
            else if (currentSection == "sounds")
            {
                ParseSoundsSection(trimmedLine, config.Sounds);
            }
        }

        return config;
    }

    private static void ParseConfigSection(string line, ConfigSection config)
    {
        var parts = line.Split(':', 2);
        if (parts.Length != 2) return;

        var key = parts[0].Trim();
        var value = parts[1].Trim().Trim('"', '\'');

        if (key == "port" && int.TryParse(value, out var port))
        {
            config.Port = port;
        }
    }

    private static void ParsePhonebookEntry(string line, PhonebookEntry entry)
    {
        var parts = line.Split(':', 2);
        if (parts.Length != 2) return;

        var key = parts[0].Trim();
        var value = parts[1].Trim().Trim('"', '\'');

        switch (key)
        {
            case "number":
                entry.Number = value;
                break;
            case "route_to":
                entry.Route_To = value;
                break;
            case "play":
                entry.Play = value;
                break;
        }
    }

    private static void ParseSoundsSection(string line, SoundsSection sounds)
    {
        var parts = line.Split(':', 2);
        if (parts.Length != 2) return;

        var key = parts[0].Trim();
        var value = parts[1].Trim().Trim('"', '\'');

        switch (key)
        {
            case "dialtone":
                sounds.Dialtone = value;
                break;
            case "busy":
                sounds.Busy = value;
                break;
            case "modem_noise":
                sounds.Modem_Noise = value;
                break;
            case "connect_success":
                sounds.Connect_Success = value;
                break;
            case "connect_failed":
                sounds.Connect_Failed = value;
                break;
            case "tone_0":
                sounds.Tone_0 = value;
                break;
            case "tone_1":
                sounds.Tone_1 = value;
                break;
            case "tone_2":
                sounds.Tone_2 = value;
                break;
            case "tone_3":
                sounds.Tone_3 = value;
                break;
            case "tone_4":
                sounds.Tone_4 = value;
                break;
            case "tone_5":
                sounds.Tone_5 = value;
                break;
            case "tone_6":
                sounds.Tone_6 = value;
                break;
            case "tone_7":
                sounds.Tone_7 = value;
                break;
            case "tone_8":
                sounds.Tone_8 = value;
                break;
            case "tone_9":
                sounds.Tone_9 = value;
                break;
        }
    }
}

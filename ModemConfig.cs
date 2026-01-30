namespace DosBoxModemEmulator;

public class ModemConfig
{
    public ConfigSection Config { get; set; } = new();
    public List<PhonebookEntry> Phonebook { get; set; } = new();
    public SoundsSection Sounds { get; set; } = new();
}

public class ConfigSection
{
    public int Port { get; set; } = 5000;
}

public class PhonebookEntry
{
    public string Number { get; set; } = string.Empty;
    public string? Route_To { get; set; }
    public string? Play { get; set; }
}

public class SoundsSection
{
    public string Dialtone { get; set; } = string.Empty;
    public string Busy { get; set; } = string.Empty;
    public string Modem_Noise { get; set; } = string.Empty;
    public string Connect_Success { get; set; } = string.Empty;
    public string Connect_Failed { get; set; } = string.Empty;
    public string Tone_0 { get; set; } = string.Empty;
    public string Tone_1 { get; set; } = string.Empty;
    public string Tone_2 { get; set; } = string.Empty;
    public string Tone_3 { get; set; } = string.Empty;
    public string Tone_4 { get; set; } = string.Empty;
    public string Tone_5 { get; set; } = string.Empty;
    public string Tone_6 { get; set; } = string.Empty;
    public string Tone_7 { get; set; } = string.Empty;
    public string Tone_8 { get; set; } = string.Empty;
    public string Tone_9 { get; set; } = string.Empty;
}

# DosBox Modem Emulator

A Hayes-compatible modem emulator designed for DOSBox-X that allows you to connect to remote services with your favourite terminal program. What makes this special over programs like tcpser is that before connecting you get a dial tone, DTMF and modem noises.

## Features

- **Hayes-compatible AT command set** (ATD, ATH, ATE, ATV, etc.)
- **Phone book routing** - Map phone numbers to TCP endpoints
- **Audio feedback** - Dial tones, busy signals, modem handshake sounds

## Quick Start

### Running the Release Version

1. **Download the latest release** from the releases page
2. **Extract** the files to a directory of your choice
3. **Edit config.yaml** to configure your phone book entries and settings
4. **Ensure sound files** are present in the `sounds/` directory
5. **Run** `DosBoxModemEmulator.exe`
6. **Configure DOSBox-X** to connect to the modem:
   ```
   serial1=nullmodem server:127.0.0.1 port:5000
   ```
7. **In DOS**, use communication software like Telix, Telemate, or Qmodem to dial

### Example Usage

In your DOS terminal program:
```
ATD123         # Dial phone number 123
ATH            # Hang up
ATE1           # Enable echo
ATV1           # Verbose mode
```

## Configuration

### config.yaml Format

The `config.yaml` file controls all aspects of the modem emulator:

```yaml
config:
  port: 5000                          # TCP port to listen on (default: 5000)

phonebook:
  - number: "123"                     # Phone number to dial
    route_to: "example.com:23"        # Route to this TCP endpoint (host:port)
  - number: "456"
    route_to: "bbs.example.org:23"
  - number: "999"                     # Optional: play audio instead of connecting
    play: "sounds/custom.wav"

sounds:
  dialtone: "sounds/dialtone.wav"           # Played when going off-hook
  busy: "sounds/busy.wav"                   # Played when number is busy/not found
  modem_noise: "sounds/modem_noise.wav"     # Handshake noise during connection
  connect_success: "sounds/connect.wav"     # Played on successful connection
  connect_failed: "sounds/connect_failed.wav" # Played on connection failure
  tone_0: "sounds/0.wav"                    # DTMF tones for each digit
  tone_1: "sounds/1.wav"
  tone_2: "sounds/2.wav"
  tone_3: "sounds/3.wav"
  tone_4: "sounds/4.wav"
  tone_5: "sounds/5.wav"
  tone_6: "sounds/6.wav"
  tone_7: "sounds/7.wav"
  tone_8: "sounds/8.wav"
  tone_9: "sounds/9.wav"
```

### Configuration Options

#### Config Section
- **port**: The TCP port the emulator listens on for DOSBox-X connections (default: 5000)

#### Phonebook Section
Each phonebook entry can have:
- **number**: The phone number to match when dialing (string)
- **route_to**: The TCP endpoint to connect to in the format `hostname:port`
- **play**: (Optional) Instead of connecting, play a WAV file

#### Sounds Section
All sound effects are optional but enhance the experience. Each sound should be a valid WAV file. The DTMF tones (0-9) are played when dialing numbers.

## Building from Source

### Prerequisites

- **.NET 10.0 SDK** (or compatible version)
- **Windows, Linux, or macOS**

### Build Steps

1. **Clone the repository**:
   ```bash
   git clone https://github.com/yourusername/DosBoxModemEmulator.git
   cd DosBoxModemEmulator
   ```

2. **Restore dependencies**:
   ```bash
   dotnet restore
   ```

3. **Build the project**:
   ```bash
   dotnet build -c Release
   ```

4. **Run the application**:
   ```bash
   dotnet run
   ```

### Publishing a Release

To create a self-contained executable with Native AOT compilation:

```bash
dotnet publish -c Release -r win-x64 --self-contained
```

The compiled executable will be in `bin/Release/net10.0/win-x64/publish/`

**Tested on Windows / Raspberry PI only**

For other platforms, replace `win-x64` with:
- `linux-x64` for Linux
- `osx-x64` for macOS (Intel)
- `osx-arm64` for macOS (Apple Silicon)

## Supported AT Commands

- **ATD[number]** - Dial a phone number from the phonebook
- **ATH** - Hang up the current connection
- **ATE0/ATE1** - Disable/Enable command echo
- **ATV0/ATV1** - Numeric/Verbose response mode
- **ATZ** - Reset modem to default settings
- **AT** - Test command (returns OK)

## How It Works

1. The emulator listens on a local TCP port (default: 5000)
2. DOSBox-X connects to this port using null-modem serial emulation
3. DOS communication software sends AT commands
4. When you dial a number (ATD), the emulator:
   - Plays DTMF tones for each digit
   - Looks up the number in the phonebook
   - Connects to the specified TCP endpoint
   - Plays modem handshake sounds
   - Proxies data between DOSBox-X and the remote server
5. You can hang up with ATH or close the connection from either side

## License

See [LICENSE](LICENSE) file for details.

## Contributing

Contributions are welcome! Please feel free to submit issues or pull requests.

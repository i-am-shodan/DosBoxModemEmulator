using System.Net.Sockets;
using System.Text;
using Microsoft.Extensions.Logging;

namespace DosBoxModemEmulator;

public class ModemSession : IDisposable
{
    private readonly TcpClient _client;
    private readonly NetworkStream _stream;
    private readonly ModemConfig _config;
    private readonly ILogger<ModemSession> _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ATCommandParser _atParser;
    private readonly AudioPlayer _audioPlayer;
    private readonly StringBuilder _commandBuffer = new();
    private bool _echoEnabled = true;
    private bool _verboseMode = true;
    private bool _isConnected = false;
    private TcpProxy? _tcpProxy;
    private CancellationTokenSource? _cts;
#pragma warning disable CS0414 // Field is assigned but never used - state tracking for future use
    private ModemState _state = ModemState.Command;
#pragma warning restore CS0414

    public ModemSession(TcpClient client, ModemConfig config, ILogger<ModemSession> logger, ILoggerFactory loggerFactory)
    {
        _client = client;
        _stream = _client.GetStream();
        _config = config;
        _logger = logger;
        _loggerFactory = loggerFactory;
        _atParser = new ATCommandParser(config, loggerFactory.CreateLogger<ATCommandParser>());
        _audioPlayer = new AudioPlayer(config, loggerFactory.CreateLogger<AudioPlayer>());
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        
        try
        {
            _logger.LogInformation("New modem session from {RemoteEndPoint}", _client.Client.RemoteEndPoint);
            
            // Send welcome message
            await WriteLineAsync("DosBox Modem Emulator v1.0");
            await WriteLineAsync("Ready");

            var buffer = new byte[4096];

            while (!_cts.Token.IsCancellationRequested && _client.Connected)
            {
                var bytesRead = await _stream.ReadAsync(buffer, 0, buffer.Length, _cts.Token);
                
                if (bytesRead == 0)
                {
                    // Connection closed
                    break;
                }

                if (_isConnected && _tcpProxy != null)
                {
                    // In connected mode, check for escape sequence
                    if (bytesRead == 3 && buffer[0] == '+' && buffer[1] == '+' && buffer[2] == '+')
                    {
                        _state = ModemState.Command;
                        _isConnected = false;
                        await WriteAsync("OK\r\n");
                        _logger.LogDebug("Returned to command mode");
                    }
                    else
                    {
                        // Forward data to remote server
                        await _tcpProxy.SendAsync(buffer.Take(bytesRead).ToArray(), _cts.Token);
                    }
                }
                else
                {
                    // In command mode, process AT commands
                    await ProcessCommandDataAsync(buffer, bytesRead);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in modem session");
        }
        finally
        {
            await CleanupAsync();
            _logger.LogInformation("Modem session ended for {RemoteEndPoint}", _client.Client.RemoteEndPoint);
        }
    }

    private async Task ProcessCommandDataAsync(byte[] buffer, int length)
    {
        for (int i = 0; i < length; i++)
        {
            char c = (char)buffer[i];

            if (c == '\r' || c == '\n')
            {
                if (_commandBuffer.Length > 0)
                {
                    var command = _commandBuffer.ToString();
                    _commandBuffer.Clear();
                    
                    if (_echoEnabled)
                    {
                        await WriteAsync("\r\n");
                    }

                    await HandleCommandAsync(command);
                }
            }
            else if (c == '\b' || c == 127) // Backspace or DEL
            {
                if (_commandBuffer.Length > 0)
                {
                    _commandBuffer.Length--;
                    if (_echoEnabled)
                    {
                        await WriteAsync("\b \b");
                    }
                }
            }
            else if (c >= 32 && c < 127) // Printable ASCII
            {
                _commandBuffer.Append(c);
                // Echo character if enabled
                if (_echoEnabled)
                {
                    await _stream.WriteAsync(new[] { buffer[i] }, 0, 1);
                }
            }
        }
    }

    private async Task HandleCommandAsync(string command)
    {
        _logger.LogDebug("Command: {Command}", command);
        
        var (response, modemCommand) = _atParser.ParseCommand(command);
        
        // Send response
        if (!string.IsNullOrEmpty(response))
        {
            await WriteAsync(response);
        }

        // Handle command
        if (modemCommand != null)
        {
            await ExecuteModemCommandAsync(modemCommand);
        }
    }

    private async Task ExecuteModemCommandAsync(ModemCommand command)
    {
        switch (command.Type)
        {
            case CommandType.Reset:
                await HandleResetAsync();
                break;

            case CommandType.Dial:
                await HandleDialAsync(command.Parameter);
                break;

            case CommandType.Hangup:
                await HandleHangupAsync();
                break;

            case CommandType.Echo:
                _echoEnabled = command.Parameter == "E1";
                break;

            case CommandType.Verbose:
                _verboseMode = command.Parameter == "V1";
                break;

            case CommandType.Escape:
                if (_isConnected)
                {
                    _state = ModemState.Command;
                    _isConnected = false;
                }
                break;

            case CommandType.Online:
                if (_tcpProxy?.IsConnected == true)
                {
                    _state = ModemState.Connected;
                    _isConnected = true;
                }
                break;
        }
    }

    private async Task HandleResetAsync()
    {
        _logger.LogInformation("Modem reset");
        if (_tcpProxy != null)
        {
            await _tcpProxy.DisconnectAsync();
            _tcpProxy = null;
        }
        _state = ModemState.Command;
        _isConnected = false;
    }

    private async Task HandleDialAsync(string phoneNumber)
    {
        _logger.LogInformation("Dialing: {PhoneNumber}", phoneNumber);
        _state = ModemState.Dialing;

        // Always play dialtone first
        await _audioPlayer.PlayDialtoneAsync(_cts!.Token);

        // Always play dial tones
        await _audioPlayer.PlayDialTonesAsync(phoneNumber, _cts!.Token);

        // Find phonebook entry
        var entry = _config.Phonebook.FirstOrDefault(e => 
            e.Number.Replace(" ", string.Empty).Replace("-", string.Empty) == phoneNumber.Replace(" ", string.Empty).Replace("-", string.Empty));

        if (entry == null)
        {
            _logger.LogWarning("Number not found in phonebook: {PhoneNumber}", phoneNumber);
            await _audioPlayer.PlayConnectFailedAsync(_cts!.Token);
            await WriteAsync("NO CARRIER\r\n");
            _state = ModemState.Command;
            return;
        }

        // Play custom sound if specified
        if (!string.IsNullOrEmpty(entry.Play))
        {
            _logger.LogDebug("Playing custom sound: {Sound}", entry.Play);
            await _audioPlayer.PlayCustomSoundAsync(entry.Play, _cts!.Token);
        }

        // Connect to remote server if specified
        if (!string.IsNullOrEmpty(entry.Route_To))
        {
            var parts = entry.Route_To.Split(':');
            if (parts.Length == 2 && int.TryParse(parts[1], out var port))
            {
                var host = parts[0];

                // Attempt connection
                _tcpProxy = new TcpProxy(_loggerFactory.CreateLogger<TcpProxy>());
                
                // Subscribe to events
                _tcpProxy.DataReceived += async (sender, data) =>
                {
                    // Need to wait until isConnected == true before sending any data
                    while (_isConnected == false && _cts!.IsCancellationRequested == false)
                    {
                        await Task.Delay(100, _cts!.Token);
                    }

                    await _stream.WriteAsync(data, 0, data.Length, _cts!.Token);
                };

                _tcpProxy.Disconnected += async (sender, e) =>
                {
                    if (_isConnected)
                    {
                        _state = ModemState.Command;
                        _isConnected = false;
                        await WriteAsync("NO CARRIER\r\n");
                        _logger.LogInformation("Remote connection closed");
                    }
                };

                var connected = await _tcpProxy.ConnectAsync(host, port, _cts!.Token);

                if (connected)
                {
                    // Play modem connection noise after successful connection
                    await _audioPlayer.PlayModemNoiseAsync(_cts!.Token);

                    // Play connect success sound
                    await _audioPlayer.PlayConnectSuccessAsync(_cts!.Token);

                    _state = ModemState.Connected;
                    _isConnected = true;
                    await WriteAsync("CONNECT 57600\r\n");
                    _logger.LogInformation("Connected to {Host}:{Port}", host, port);
                }
                else
                {
                    // Play connect failed sound (busy tone)
                    await _audioPlayer.PlayConnectFailedAsync(_cts!.Token);

                    _state = ModemState.Command;
                    await WriteAsync("NO CARRIER\r\n");
                    _logger.LogWarning("Connection failed");
                }
            }
            else
            {
                _logger.LogWarning("Invalid route_to format: {RouteTo}", entry.Route_To);
                await WriteAsync("NO CARRIER\r\n");
                _state = ModemState.Command;
            }
        }
        else
        {
            // No route specified, just play sound and return busy
            await _audioPlayer.PlayBusySignalAsync(_cts!.Token);

            await WriteAsync("BUSY\r\n");
            _state = ModemState.Command;
        }
    }

    private async Task HandleHangupAsync()
    {
        _logger.LogInformation("Hanging up");
        
        if (_tcpProxy != null)
        {
            await _tcpProxy.DisconnectAsync();
            _tcpProxy = null;
        }

        _state = ModemState.Command;
        _isConnected = false;
    }

    private async Task WriteAsync(string text)
    {
        var bytes = Encoding.ASCII.GetBytes(text);
        await _stream.WriteAsync(bytes, 0, bytes.Length);
    }

    private async Task WriteLineAsync(string text)
    {
        await WriteAsync(text + "\r\n");
    }

    private async Task CleanupAsync()
    {
        _cts?.Cancel();
        
        if (_tcpProxy != null)
        {
            await _tcpProxy.DisconnectAsync();
            _tcpProxy.Dispose();
        }
        
        _audioPlayer.StopCurrentAudio();
        
        _stream.Close();
        _client.Close();
    }

    public void Dispose()
    {
        CleanupAsync().Wait();
        _cts?.Dispose();
        _stream.Dispose();
        _client.Dispose();
    }
}

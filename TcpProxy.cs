using System.Net.Sockets;
using Microsoft.Extensions.Logging;

namespace DosBoxModemEmulator;

public class TcpProxy : IDisposable
{
    private TcpClient? _client;
    private NetworkStream? _stream;
    private CancellationTokenSource? _cts;
    private bool _isConnected;
    private readonly ILogger<TcpProxy> _logger;

    public event EventHandler<byte[]>? DataReceived;
    public event EventHandler? Disconnected;

    public bool IsConnected => _isConnected;

    public TcpProxy(ILogger<TcpProxy> logger)
    {
        _logger = logger;
    }

    public async Task<bool> ConnectAsync(string host, int port, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Connecting to {Host}:{Port}...", host, port);
            
            _client = new TcpClient();
            await _client.ConnectAsync(host, port, cancellationToken);
            _stream = _client.GetStream();
            _isConnected = true;

            _logger.LogInformation("Connected to {Host}:{Port}", host, port);

            // Start reading from the TCP connection
            _cts = new CancellationTokenSource();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error connecting to {Host}:{Port}", host, port);
            _isConnected = false;
        }

        return _isConnected;
    }

    public void StartTransfer()
    {
        if (_isConnected && _cts != null)
        {
            _ = Task.Run(() => ReadLoopAsync(_cts.Token));
        }
    }

    public async Task SendAsync(byte[] data, CancellationToken cancellationToken = default)
    {
        if (_stream != null && _isConnected)
        {
            try
            {
                await _stream.WriteAsync(data, 0, data.Length, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending data to TCP connection");
                await DisconnectAsync();
            }
        }
    }

    private async Task ReadLoopAsync(CancellationToken cancellationToken)
    {
        var buffer = new byte[4096];

        try
        {
            while (!cancellationToken.IsCancellationRequested && _stream != null && _isConnected)
            {
                var bytesRead = await _stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken);
                
                if (bytesRead == 0)
                {
                    // Connection closed
                    _logger.LogInformation("TCP connection closed by remote host");
                    await DisconnectAsync();
                    break;
                }

                if (bytesRead > 0)
                {
                    var data = new byte[bytesRead];
                    Array.Copy(buffer, data, bytesRead);
                    DataReceived?.Invoke(this, data);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Normal cancellation
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading from TCP connection");
            await DisconnectAsync();
        }
    }

    public async Task DisconnectAsync()
    {
        if (!_isConnected)
        {
            return;
        }

        _logger.LogInformation("Disconnecting TCP connection...");
        _isConnected = false;

        _cts?.Cancel();

        if (_stream != null)
        {
            await _stream.DisposeAsync();
            _stream = null;
        }

        if (_client != null)
        {
            _client.Close();
            _client.Dispose();
            _client = null;
        }

        Disconnected?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose()
    {
        DisconnectAsync().Wait();
        _cts?.Dispose();
    }
}

using System.Net;
using System.Net.Sockets;
using System.Text;
using Microsoft.Extensions.Logging;
using DosBoxModemEmulator;

// Setup logging
using var loggerFactory = LoggerFactory.Create(builder =>
{
    builder
        .AddConsole()
        .SetMinimumLevel(LogLevel.Information);
});

var logger = loggerFactory.CreateLogger<Program>();

logger.LogInformation("DosBox Modem Emulator v1.0");
logger.LogInformation("===========================");

// Load configuration
var config = ConfigLoader.LoadConfig("config.yaml", logger);
logger.LogInformation("Configuration loaded. Port: {Port}", config.Config.Port);
logger.LogInformation("Phonebook entries: {Count}", config.Phonebook.Count);

// Create TCP listener (localhost only)
var listener = new TcpListener(IPAddress.Loopback, config.Config.Port);
var cts = new CancellationTokenSource();
Task? activeSession = null;

// Handle Ctrl+C
Console.CancelKeyPress += (sender, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};

try
{
    listener.Start();
    logger.LogInformation("Listening on TCP port {Port} (localhost only)", config.Config.Port);
    logger.LogInformation("Waiting for DOSBox-X connection...");
    logger.LogInformation("Note: Only one connection at a time (exclusive audio use)");
    logger.LogInformation("Press Ctrl+C to exit.");

    while (!cts.Token.IsCancellationRequested)
    {
        // Accept connections with cancellation support
        var acceptTask = listener.AcceptTcpClientAsync();
        var cancelTask = Task.Delay(Timeout.Infinite, cts.Token);
        
        var completedTask = await Task.WhenAny(acceptTask, cancelTask);
        
        if (completedTask == cancelTask)
        {
            // Cancellation requested
            break;
        }

        var client = await acceptTask;
        
        // Check if there's already an active session
        if (activeSession != null && !activeSession.IsCompleted)
        {
            // Reject the connection - already have an active session
            logger.LogWarning("Rejected connection from {RemoteEndPoint} - session already active", client.Client.RemoteEndPoint);
            
            try
            {
                var rejectMessage = Encoding.ASCII.GetBytes("BUSY - Another session is active\r\n");
                await client.GetStream().WriteAsync(rejectMessage, 0, rejectMessage.Length);
                await Task.Delay(100); // Give time for message to send
            }
            catch
            {
            }
            
            client.Close();
            continue;
        }
        
        // Handle the connection in a new session
        var sessionLogger = loggerFactory.CreateLogger<ModemSession>();
        var session = new ModemSession(client, config, sessionLogger, loggerFactory);
        activeSession = session.RunAsync(cts.Token);
    }
}
catch (OperationCanceledException)
{
    logger.LogInformation("Shutting down...");
}
catch (Exception ex)
{
    logger.LogError(ex, "Error in main loop");
}
finally
{
    listener.Stop();
    
    // Wait for active session to complete
    if (activeSession != null && !activeSession.IsCompleted)
    {
        logger.LogInformation("Waiting for active session to close...");
        await activeSession;
    }
}

logger.LogInformation("Modem emulator stopped.");

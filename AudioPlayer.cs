using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

namespace DosBoxModemEmulator;

public class AudioPlayer
{
    private readonly ModemConfig _config;
    private readonly string _basePath;
    private readonly ILogger<AudioPlayer> _logger;
    private Process? _currentProcess;
    private static readonly bool IsWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
    private static readonly bool IsLinux = RuntimeInformation.IsOSPlatform(OSPlatform.Linux);

    public AudioPlayer(ModemConfig config, ILogger<AudioPlayer> logger, string basePath = ".")
    {
        _config = config;
        _logger = logger;
        _basePath = basePath;
    }

    private async Task<bool> PlayAudioFileAsync(string filename, CancellationToken cancellationToken = default)
    {
        try
        {
            var fullPath = Path.Combine(_basePath, filename);
            if (!File.Exists(fullPath))
            {
                _logger.LogWarning("Audio file not found: {FullPath}", fullPath);
                return false;
            }

            // Stop any currently playing audio
            StopCurrentAudio();

            ProcessStartInfo processStartInfo;

            if (IsWindows)
            {
                // Use PowerShell with System.Media.SoundPlayer on Windows
                processStartInfo = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-NoProfile -NonInteractive -Command \"(New-Object System.Media.SoundPlayer '{fullPath}').PlaySync()\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                };
            }
            else if (IsLinux)
            {
                // Use aplay (ALSA) on Linux
                processStartInfo = new ProcessStartInfo
                {
                    FileName = "aplay",
                    Arguments = $"-q \"{fullPath}\"", // -q for quiet mode
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };
            }
            else
            {
                _logger.LogWarning("Unsupported platform for audio playback");
                return false;
            }

            _currentProcess = Process.Start(processStartInfo);
            
            if (_currentProcess == null)
            {
                _logger.LogWarning("Failed to start audio playback for {Filename}", filename);
                return false;
            }

            _logger.LogDebug("Playing audio: {Filename}", filename);
            await _currentProcess.WaitForExitAsync(cancellationToken);
            
            var exitCode = _currentProcess.ExitCode;
            _currentProcess = null;
            
            return exitCode == 0;
        }
        catch (OperationCanceledException)
        {
            StopCurrentAudio();
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error playing audio file {Filename}", filename);
            return false;
        }
    }

    public void StopCurrentAudio()
    {
        if (_currentProcess != null && !_currentProcess.HasExited)
        {
            try
            {
                _currentProcess.Kill();
                _currentProcess.WaitForExit(1000);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error stopping audio");
            }
            finally
            {
                _currentProcess?.Dispose();
                _currentProcess = null;
            }
        }
    }

    public async Task PlayDialtoneAsync(CancellationToken cancellationToken = default)
    {
        await PlayAudioFileAsync(_config.Sounds.Dialtone, cancellationToken);
    }

    public async Task PlayBusySignalAsync(CancellationToken cancellationToken = default)
    {
        await PlayAudioFileAsync(_config.Sounds.Busy, cancellationToken);
    }

    public async Task PlayModemNoiseAsync(CancellationToken cancellationToken = default)
    {
        await PlayAudioFileAsync(_config.Sounds.Modem_Noise, cancellationToken);
    }

    public async Task PlayConnectSuccessAsync(CancellationToken cancellationToken = default)
    {
        await PlayAudioFileAsync(_config.Sounds.Connect_Success, cancellationToken);
    }

    public async Task PlayConnectFailedAsync(CancellationToken cancellationToken = default)
    {
        await PlayAudioFileAsync(_config.Sounds.Connect_Failed, cancellationToken);
    }

    public async Task PlayToneForDigitAsync(char digit, CancellationToken cancellationToken = default)
    {
        var filename = digit switch
        {
            '0' => _config.Sounds.Tone_0,
            '1' => _config.Sounds.Tone_1,
            '2' => _config.Sounds.Tone_2,
            '3' => _config.Sounds.Tone_3,
            '4' => _config.Sounds.Tone_4,
            '5' => _config.Sounds.Tone_5,
            '6' => _config.Sounds.Tone_6,
            '7' => _config.Sounds.Tone_7,
            '8' => _config.Sounds.Tone_8,
            '9' => _config.Sounds.Tone_9,
            _ => null
        };

        if (filename != null)
        {
            await PlayAudioFileAsync(filename, cancellationToken);
        }
    }

    public async Task PlayDialTonesAsync(string phoneNumber, CancellationToken cancellationToken = default)
    {
        foreach (char digit in phoneNumber)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            if (char.IsDigit(digit))
            {
                await PlayToneForDigitAsync(digit, cancellationToken);
                await Task.Delay(100, cancellationToken); // Brief pause between tones
            }
            else if (digit == ' ' || digit == '-')
            {
                await Task.Delay(200, cancellationToken); // Longer pause for spaces/dashes
            }
        }

        // Add 1s second gap after all DTMF tones
        await Task.Delay(1000, cancellationToken);
    }

    public async Task PlayCustomSoundAsync(string filename, CancellationToken cancellationToken = default)
    {
        await PlayAudioFileAsync(filename, cancellationToken);
    }
}

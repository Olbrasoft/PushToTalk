using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Olbrasoft.PushToTalk.Clipboard;

namespace Olbrasoft.PushToTalk.TextInput;

/// <summary>
/// Text typer implementation using clipboard + dotool for Linux Wayland.
/// Saves clipboard content, pastes text, then restores original clipboard.
/// This approach supports full Unicode including Czech diacritics (háčky, čárky).
/// Automatically detects terminal windows and uses appropriate paste shortcut.
/// </summary>
public class DotoolTextTyper : ITextTyper
{
    private readonly IClipboardManager _clipboardManager;
    private readonly ILogger<DotoolTextTyper> _logger;
    
    /// <summary>
    /// Terminal window class names that require Ctrl+Shift+V for pasting.
    /// </summary>
    private static readonly HashSet<string> TerminalClasses = new(StringComparer.OrdinalIgnoreCase)
    {
        "kitty",
        "gnome-terminal",
        "gnome-terminal-server",
        "org.gnome.Terminal",
        "konsole",
        "xfce4-terminal",
        "mate-terminal",
        "tilix",
        "terminator",
        "alacritty",
        "wezterm",
        "foot",
        "xterm",
        "urxvt",
        "st",
        "terminology"
    };

    /// <summary>
    /// Initializes a new instance of the <see cref="DotoolTextTyper"/> class.
    /// </summary>
    /// <param name="clipboardManager">Clipboard manager for save/restore operations.</param>
    /// <param name="logger">Logger instance.</param>
    public DotoolTextTyper(
        IClipboardManager clipboardManager,
        ILogger<DotoolTextTyper> logger)
    {
        _clipboardManager = clipboardManager ?? throw new ArgumentNullException(nameof(clipboardManager));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public bool IsAvailable
    {
        get
        {
            try
            {
                // Check both dotool and wl-copy are available
                var dotoolCheck = Process.Start(new ProcessStartInfo
                {
                    FileName = "which",
                    Arguments = "dotool",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                });
                dotoolCheck?.WaitForExit(1000);
                
                var wlCopyCheck = Process.Start(new ProcessStartInfo
                {
                    FileName = "which",
                    Arguments = "wl-copy",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                });
                wlCopyCheck?.WaitForExit(1000);
                
                return dotoolCheck?.ExitCode == 0 && wlCopyCheck?.ExitCode == 0;
            }
            catch
            {
                return false;
            }
        }
    }

    /// <inheritdoc/>
    public async Task TypeTextAsync(string text, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            _logger.LogWarning("Attempted to type empty or whitespace text");
            return;
        }

        if (!IsAvailable)
        {
            _logger.LogError("dotool or wl-copy is not available on this system");
            throw new InvalidOperationException("dotool or wl-copy is not available");
        }

        try
        {
            // Convert to lowercase and add space
            var textToType = text.ToLower() + " ";

            // Step 1: Save current clipboard content
            var originalClipboard = await _clipboardManager.GetClipboardAsync(cancellationToken);

            // Step 2: Copy our text to clipboard
            await _clipboardManager.SetClipboardAsync(textToType, cancellationToken);

            // Small delay to ensure clipboard is ready
            await Task.Delay(50, cancellationToken);

            // Step 3: Detect if active window is a terminal and use appropriate paste shortcut
            var pasteShortcut = await GetPasteShortcutAsync(cancellationToken);
            _logger.LogInformation("Using paste shortcut: {Shortcut}", pasteShortcut);
            
            var dotoolProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "/bin/bash",
                    Arguments = $"-c \"echo 'key {pasteShortcut}' | dotool\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            dotoolProcess.Start();
            await dotoolProcess.WaitForExitAsync(cancellationToken);

            if (dotoolProcess.ExitCode != 0)
            {
                var error = await dotoolProcess.StandardError.ReadToEndAsync(cancellationToken);
                _logger.LogError("dotool failed with exit code {ExitCode}: {Error}", dotoolProcess.ExitCode, error);
                throw new InvalidOperationException($"dotool failed: {error}");
            }

            // Small delay to ensure paste completed
            await Task.Delay(100, cancellationToken);

            // Step 4: Restore original clipboard content
            if (!string.IsNullOrEmpty(originalClipboard))
            {
                try
                {
                    await _clipboardManager.SetClipboardAsync(originalClipboard, cancellationToken);
                    _logger.LogDebug("Restored original clipboard content");
                }
                catch (InvalidOperationException ex)
                {
                    _logger.LogWarning("Could not restore clipboard: {Message}", ex.Message);
                }
            }

            _logger.LogDebug("Successfully typed {CharCount} characters", textToType.Length);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Text typing was cancelled");
            throw;
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Process error while typing text: {Text}", text);
            throw;
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            _logger.LogError(ex, "Failed to start process while typing text: {Text}", text);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task SendKeyAsync(string key, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            _logger.LogWarning("Attempted to send empty key");
            return;
        }

        if (!IsAvailable)
        {
            _logger.LogError("dotool is not available on this system");
            throw new InvalidOperationException("dotool is not available");
        }

        try
        {
            _logger.LogInformation("Sending key: {Key}", key);

            var dotoolProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "/bin/bash",
                    Arguments = $"-c \"echo 'key {key}' | dotool\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            dotoolProcess.Start();
            await dotoolProcess.WaitForExitAsync(cancellationToken);

            if (dotoolProcess.ExitCode != 0)
            {
                var error = await dotoolProcess.StandardError.ReadToEndAsync(cancellationToken);
                _logger.LogError("dotool failed with exit code {ExitCode}: {Error}", dotoolProcess.ExitCode, error);
                throw new InvalidOperationException($"dotool failed: {error}");
            }

            _logger.LogDebug("Successfully sent key: {Key}", key);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Key send was cancelled");
            throw;
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Process error while sending key: {Key}", key);
            throw;
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            _logger.LogError(ex, "Failed to start process while sending key: {Key}", key);
            throw;
        }
    }

    /// <summary>
    /// Gets the appropriate paste shortcut based on the active window type.
    /// Terminals use Ctrl+Shift+V, other applications use Ctrl+V.
    /// </summary>
    private async Task<string> GetPasteShortcutAsync(CancellationToken cancellationToken)
    {
        try
        {
            var windowClass = await GetActiveWindowClassAsync(cancellationToken);
            
            if (!string.IsNullOrEmpty(windowClass) && TerminalClasses.Contains(windowClass))
            {
                _logger.LogInformation("Detected terminal window: {WindowClass}, using Ctrl+Shift+V", windowClass);
                return "ctrl+shift+v";
            }
            
            _logger.LogInformation("Non-terminal window detected: {WindowClass}, using Ctrl+V", windowClass ?? "(unknown)");
            return "ctrl+v";
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Process error detecting window class, defaulting to Ctrl+V");
            return "ctrl+v";
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            _logger.LogWarning(ex, "Failed to start process for window class detection, defaulting to Ctrl+V");
            return "ctrl+v";
        }
    }

    /// <summary>
    /// Gets the WM_CLASS of the currently active window using window-calls GNOME Shell extension.
    /// Uses the List method and finds the window with focus=true.
    /// </summary>
    private async Task<string?> GetActiveWindowClassAsync(CancellationToken cancellationToken)
    {
        try
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "gdbus",
                    Arguments = "call --session --dest org.gnome.Shell " +
                               "--object-path /org/gnome/Shell/Extensions/Windows " +
                               "--method org.gnome.Shell.Extensions.Windows.List",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            var output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);

            if (process.ExitCode == 0 && !string.IsNullOrWhiteSpace(output))
            {
                _logger.LogDebug("D-Bus window-calls returned: {Output}", output.Trim());

                // Output format is: ('[{"wm_class":"kitty",...,"focus":true},...]',)
                // Extract the JSON array from the gdbus output
                // output is guaranteed non-null by the if condition above
                var jsonStart = output.IndexOf('[');
                var jsonEnd = output.LastIndexOf(']');
                
                if (jsonStart >= 0 && jsonEnd > jsonStart)
                {
                    var jsonArray = output.Substring(jsonStart, jsonEnd - jsonStart + 1);
                    
                    // Parse JSON and find focused window
                    var windows = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(jsonArray);
                    
                    foreach (var window in windows.EnumerateArray())
                    {
                        if (window.TryGetProperty("focus", out var focusProp) && focusProp.GetBoolean())
                        {
                            if (window.TryGetProperty("wm_class", out var wmClassProp))
                            {
                                var windowClass = wmClassProp.GetString();
                                _logger.LogDebug("Focused window class: {WindowClass}", windowClass);
                                return windowClass;
                            }
                        }
                    }
                    
                    _logger.LogWarning("No focused window found in window list");
                }
                else
                {
                    _logger.LogWarning("Could not find JSON array in output: {Output}", output?.Trim());
                }
            }
            else
            {
                var error = await process.StandardError.ReadToEndAsync(cancellationToken);
                _logger.LogWarning("D-Bus call failed: ExitCode={ExitCode}, Error={Error}", 
                    process.ExitCode, error?.Trim());
            }

            return null;
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogDebug(ex, "Process error getting active window class via D-Bus");
            return null;
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            _logger.LogDebug(ex, "Failed to start gdbus process for active window class");
            return null;
        }
        catch (System.Text.Json.JsonException ex)
        {
            _logger.LogDebug(ex, "Failed to parse D-Bus JSON response for window class");
            return null;
        }
    }
}

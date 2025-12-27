namespace Olbrasoft.PushToTalk.Clipboard;

/// <summary>
/// Interface for clipboard management operations.
/// Provides save and restore functionality for clipboard content.
/// </summary>
public interface IClipboardManager
{
    /// <summary>
    /// Gets the current clipboard content.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for async operation.</param>
    /// <returns>The clipboard content as string, or null if clipboard is empty or inaccessible.</returns>
    Task<string?> GetClipboardAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets the clipboard content to the specified text.
    /// </summary>
    /// <param name="content">The text to copy to clipboard.</param>
    /// <param name="cancellationToken">Cancellation token for async operation.</param>
    /// <exception cref="InvalidOperationException">Thrown when clipboard operation fails.</exception>
    Task SetClipboardAsync(string content, CancellationToken cancellationToken = default);
}

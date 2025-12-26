namespace Olbrasoft.PushToTalk.Core.Interfaces;

/// <summary>
/// Client for sending notifications to VirtualAssistant service.
/// </summary>
public interface IVirtualAssistantClient
{
    /// <summary>
    /// Sends a notification to VirtualAssistant.
    /// VirtualAssistant will read the notification via TTS.
    /// </summary>
    /// <param name="text">Notification text in Czech</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task SendNotificationAsync(string text, CancellationToken cancellationToken = default);
}

using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Olbrasoft.PushToTalk.Core.Configuration;
using Olbrasoft.PushToTalk.Core.Interfaces;

namespace Olbrasoft.PushToTalk.Service.Services;

/// <summary>
/// Client for sending notifications to VirtualAssistant service.
/// VirtualAssistant will read notifications via TTS.
/// </summary>
public class NotificationClient : INotificationClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<NotificationClient> _logger;

    private const int PushToTalkAgentId = 10; // Registered in virtual_assistant.agents table

    public NotificationClient(
        HttpClient httpClient,
        IOptions<ServiceEndpoints> serviceEndpoints,
        ILogger<NotificationClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;

        _httpClient.BaseAddress = new Uri(serviceEndpoints.Value.VirtualAssistant);
        _httpClient.Timeout = TimeSpan.FromSeconds(5);
    }

    public async Task SendNotificationAsync(string text, CancellationToken cancellationToken = default)
    {
        try
        {
            var notification = new
            {
                agent_id = PushToTalkAgentId,
                text = text
            };

            _logger.LogInformation("Sending notification to VirtualAssistant: {Text}", text);

            var response = await _httpClient.PostAsJsonAsync("/api/notifications", notification, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Notification sent to VirtualAssistant successfully");
            }
            else
            {
                _logger.LogWarning("Failed to send notification to VirtualAssistant. Status: {StatusCode}, Reason: {Reason}",
                    response.StatusCode, response.ReasonPhrase);
            }
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error sending notification to VirtualAssistant: {Message}", ex.Message);
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogError(ex, "Timeout sending notification to VirtualAssistant");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error sending notification to VirtualAssistant: {Message}", ex.Message);
        }
    }
}

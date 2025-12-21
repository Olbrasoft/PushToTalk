using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Olbrasoft.PushToTalk.App.Services;

namespace Olbrasoft.PushToTalk.App.Api;

/// <summary>
/// Extension methods for mapping dictation API endpoints.
/// Follows Minimal API pattern for clean separation of concerns.
/// </summary>
public static class DictationApiEndpoints
{
    /// <summary>
    /// Maps all dictation-related API endpoints to the application.
    /// </summary>
    /// <param name="app">The web application builder</param>
    /// <returns>The web application builder for method chaining</returns>
    public static IEndpointRouteBuilder MapDictationEndpoints(this IEndpointRouteBuilder app)
    {
        // Status endpoint - GET /api/status
        app.MapGet("/api/status", (DictationService dictationService) => new
        {
            IsRecording = dictationService.State == DictationState.Recording,
            IsTranscribing = dictationService.State == DictationState.Transcribing,
            State = dictationService.State.ToString()
        })
        .WithName("GetDictationStatus")
        .WithTags("Dictation");

        // Start recording - POST /api/recording/start
        app.MapPost("/api/recording/start", async (DictationService dictationService) =>
        {
            if (dictationService.State == DictationState.Idle)
            {
                await dictationService.StartDictationAsync();
                return Results.Ok(new { Success = true });
            }
            return Results.BadRequest(new { Success = false, Error = "Already recording or transcribing" });
        })
        .WithName("StartRecording")
        .WithTags("Dictation");

        // Stop recording - POST /api/recording/stop
        app.MapPost("/api/recording/stop", async (DictationService dictationService) =>
        {
            if (dictationService.State == DictationState.Recording)
            {
                await dictationService.StopDictationAsync();
                return Results.Ok(new { Success = true });
            }
            return Results.BadRequest(new { Success = false, Error = "Not recording" });
        })
        .WithName("StopRecording")
        .WithTags("Dictation");

        // Toggle recording - POST /api/recording/toggle
        app.MapPost("/api/recording/toggle", async (DictationService dictationService) =>
        {
            if (dictationService.State == DictationState.Idle)
            {
                await dictationService.StartDictationAsync();
                return Results.Ok(new { Success = true, IsRecording = true });
            }
            else if (dictationService.State == DictationState.Recording)
            {
                await dictationService.StopDictationAsync();
                return Results.Ok(new { Success = true, IsRecording = false });
            }
            return Results.BadRequest(new { Success = false, Error = "Transcription in progress" });
        })
        .WithName("ToggleRecording")
        .WithTags("Dictation");

        // Cancel transcription - POST /api/recording/cancel
        app.MapPost("/api/recording/cancel", (DictationService dictationService) =>
        {
            if (dictationService.State == DictationState.Transcribing)
            {
                dictationService.CancelTranscription();
                return Results.Ok(new { Success = true });
            }
            return Results.BadRequest(new { Success = false, Error = "Not transcribing" });
        })
        .WithName("CancelTranscription")
        .WithTags("Dictation");

        return app;
    }
}

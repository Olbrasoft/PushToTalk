using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Olbrasoft.PushToTalk.Audio;
using Olbrasoft.PushToTalk.Core.Interfaces;
using Olbrasoft.PushToTalk.Linux.Speech;
using Olbrasoft.PushToTalk.Service.Services;
using Olbrasoft.PushToTalk.TextInput;
using PushToTalk.Data;
using PushToTalk.Data.EntityFrameworkCore;

// Disambiguate types that exist in multiple namespaces
using PttManualMuteService = Olbrasoft.PushToTalk.Service.Services.ManualMuteService;
using PttEvdevKeyboardMonitor = Olbrasoft.PushToTalk.EvdevKeyboardMonitor;

namespace Olbrasoft.PushToTalk.Service;

/// <summary>
/// Extension methods for registering PushToTalk services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds all PushToTalk services to the service collection.
    /// </summary>
    public static IServiceCollection AddPushToTalkServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Get configuration values
        var keyboardDevice = configuration.GetValue<string?>("PushToTalkDictation:KeyboardDevice");
        var whisperLanguage = configuration.GetValue<string>("PushToTalkDictation:WhisperLanguage") ?? "cs";

        // SignalR
        services.AddSignalR();

        // CORS (for web clients)
        services.AddCors(options =>
        {
            options.AddDefaultPolicy(policy =>
            {
                policy.AllowAnyOrigin()
                      .AllowAnyHeader()
                      .AllowAnyMethod();
            });
        });

        // PTT Notifier service
        services.AddSingleton<IPttNotifier, PttNotifier>();

        // Transcription history service (single-level history for repeat functionality)
        services.AddSingleton<ITranscriptionHistory, TranscriptionHistory>();

        // Manual mute service (ScrollLock) - register as concrete type for injection
        // Also register interface for backwards compatibility
        services.AddSingleton<PttManualMuteService>();
        services.AddSingleton<Olbrasoft.PushToTalk.Services.IManualMuteService>(sp =>
            sp.GetRequiredService<PttManualMuteService>());

        // Register services
        services.AddSingleton<IKeyboardMonitor>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<PttEvdevKeyboardMonitor>>();
            return new PttEvdevKeyboardMonitor(logger, keyboardDevice);
        });

        // Key simulator (ISP: separated from keyboard monitoring)
        services.AddSingleton<IKeySimulator, UinputKeySimulator>();

        services.AddSingleton<IAudioRecorder>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<PipeWireAudioRecorder>>();
            return new PipeWireAudioRecorder(logger);
        });

        // Speech transcriber (using gRPC microservice)
        services.AddSingleton<ISpeechTranscriber>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<SpeechToTextGrpcClient>>();
            var serviceUrl = Environment.GetEnvironmentVariable("SPEECHTOTEXT_SERVICE_URL") ?? "http://localhost:5052";
            return new SpeechToTextGrpcClient(logger, serviceUrl, whisperLanguage);
        });

        // Environment provider for display server detection
        services.AddSingleton<IEnvironmentProvider, SystemEnvironmentProvider>();

        // Clipboard manager for save/restore operations
        services.AddSingleton<Olbrasoft.PushToTalk.Clipboard.IClipboardManager>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<Olbrasoft.PushToTalk.Clipboard.WlClipboardManager>>();
            return new Olbrasoft.PushToTalk.Clipboard.WlClipboardManager(logger);
        });

        // Terminal detector for window class detection
        services.AddSingleton<Olbrasoft.PushToTalk.WindowManagement.ITerminalDetector>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<Olbrasoft.PushToTalk.WindowManagement.WaylandTerminalDetector>>();
            return new Olbrasoft.PushToTalk.WindowManagement.WaylandTerminalDetector(logger);
        });

        // Text typer factory (injectable, testable)
        services.AddSingleton<ITextTyperFactory, TextTyperFactory>();

        // Auto-detect display server (X11/Wayland) and use appropriate text typer
        services.AddSingleton<ITextTyper>(sp =>
        {
            var factory = sp.GetRequiredService<ITextTyperFactory>();
            var logger = sp.GetRequiredService<ILogger<Program>>();
            logger.LogInformation("Detected display server: {DisplayServer}", factory.GetDisplayServerName());
            return factory.Create();
        });

        // Typing sound player for transcription feedback
        services.AddSingleton(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<TypingSoundPlayer>>();
            var soundsDirectory = Path.Combine(AppContext.BaseDirectory, "sounds");
            return TypingSoundPlayer.CreateFromDirectory(logger, soundsDirectory);
        });

        // Transcription tray service (not from DI - needs special lifecycle with GTK)
        services.AddSingleton<Tray.TranscriptionTrayService>();

        // Hallucination filter for Whisper transcriptions
        services.Configure<HallucinationFilterOptions>(
            configuration.GetSection(HallucinationFilterOptions.SectionName));
        services.AddSingleton<IHallucinationFilter, WhisperHallucinationFilter>();

        // Speech lock service (file-based lock to prevent TTS during recording)
        services.AddSingleton<ISpeechLockService, SpeechLockService>();

        // TTS control service (HTTP client for TTS and VirtualAssistant APIs)
        services.AddHttpClient<ITtsControlService, TtsControlService>();

        // Composite services (SRP refactoring - combines related dependencies)
        services.AddSingleton<ITranscriptionProcessor, TranscriptionProcessor>();
        services.AddSingleton<ITextOutputService, TextOutputService>();
        services.AddSingleton<IRecordingModeManager, RecordingModeManager>();

        // Recording workflow (extracted from DictationWorker for SRP)
        services.AddSingleton<IRecordingWorkflow, RecordingWorkflow>();

        // HTTP client for DictationWorker
        services.AddHttpClient<DictationWorker>();

        // Register worker as singleton first (so we can resolve it for interfaces)
        services.AddSingleton<DictationWorker>();
        services.AddHostedService<DictationWorker>(sp => sp.GetRequiredService<DictationWorker>());

        // Register interfaces pointing to the same DictationWorker instance
        services.AddSingleton<IRecordingStateProvider>(sp => sp.GetRequiredService<DictationWorker>());
        services.AddSingleton<IRecordingController>(sp => sp.GetRequiredService<DictationWorker>());

        // Database - PostgreSQL with Entity Framework Core
        services.AddDbContext<PushToTalkDbContext>(options =>
        {
            var connectionString = configuration.GetConnectionString("DefaultConnection");
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException(
                    "Database connection string 'DefaultConnection' is not configured. " +
                    "Use 'dotnet user-secrets set \"ConnectionStrings:DefaultConnection\" \"<connection-string>\"' " +
                    "for development or set environment variable for production.");
            }
            options.UseNpgsql(connectionString);
        });

        // Transcription repository for saving Whisper transcriptions to database
        services.AddScoped<ITranscriptionRepository, TranscriptionRepository>();

        // LLM Correction Services
        // Configure Mistral options from database (not from appsettings/secrets)
        services.ConfigureOptions<Configuration.DatabaseMistralOptionsSetup>();

        // Configure ServiceEndpoints for VirtualAssistant URL
        services.Configure<Core.Configuration.ServiceEndpoints>(
            configuration.GetSection(Core.Configuration.ServiceEndpoints.SectionName));

        // Prompt loader for LLM system prompts
        services.AddSingleton<Core.Interfaces.IPromptLoader, Core.Services.EmbeddedPromptLoader>();

        // HTTP client for MistralProvider
        services.AddHttpClient<ILlmProvider, Core.Services.MistralProvider>();

        // HTTP client for NotificationClient (VirtualAssistant)
        services.AddHttpClient<INotificationClient, NotificationClient>();

        // LLM correction orchestration service (scoped - uses DbContext)
        services.AddScoped<ILlmCorrectionService, LlmCorrectionService>();

        // Email notification service (scoped - uses DbContext)
        services.AddScoped<IEmailNotificationService, EmailNotificationService>();

        return services;
    }
}

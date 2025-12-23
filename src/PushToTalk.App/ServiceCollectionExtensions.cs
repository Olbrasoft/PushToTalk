using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Olbrasoft.NotificationAudio.Providers.Linux;
using Olbrasoft.PushToTalk.App.Configuration;
using Olbrasoft.PushToTalk.App.Services;
using Olbrasoft.PushToTalk.App.Tray;
using Olbrasoft.PushToTalk.Audio;
using Olbrasoft.PushToTalk.Linux.Speech;
using Olbrasoft.PushToTalk.TextInput;
using Olbrasoft.SystemTray.Linux;
using PushToTalk.Data;
using PushToTalk.Data.EntityFrameworkCore;

namespace Olbrasoft.PushToTalk.App;

/// <summary>
/// Extension methods for registering dictation services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds core dictation services to the service collection.
    /// </summary>
    public static IServiceCollection AddDictationServices(
        this IServiceCollection services,
        DictationOptions options,
        IConfiguration configuration)
    {
        // Register IConfiguration for NotificationAudio (LinuxAudioSinkSelector needs it)
        services.AddSingleton(configuration);

        // NotificationAudio for playing transcription sound
        services.AddNotificationAudio();

        // VirtualAssistant client for TTS coordination
        services.AddSingleton<HttpClient>();
        services.AddSingleton<IVirtualAssistantClient>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<VirtualAssistantClient>>();
            var httpClient = sp.GetRequiredService<HttpClient>();
            return new VirtualAssistantClient(logger, httpClient, configuration);
        });

        // Keyboard monitor
        services.AddSingleton<IKeyboardMonitor>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<EvdevKeyboardMonitor>>();
            return new EvdevKeyboardMonitor(logger, options.KeyboardDevice);
        });

        // Key simulator for CapsLock LED synchronization
        services.AddSingleton<IKeySimulator>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<UinputKeySimulator>>();
            return new UinputKeySimulator(logger);
        });

        // Audio recorder
        services.AddSingleton<IAudioRecorder>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<PipeWireAudioRecorder>>();
            return new PipeWireAudioRecorder(
                logger,
                deviceName: options.AudioDevice); // Use configured audio device or default
        });

        // Speech transcriber (using gRPC microservice)
        services.AddSingleton<ISpeechTranscriber>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<SpeechToTextGrpcClient>>();
            var serviceUrl = Environment.GetEnvironmentVariable("SPEECHTOTEXT_SERVICE_URL") ?? "http://localhost:5052";
            return new SpeechToTextGrpcClient(logger, serviceUrl, options.WhisperLanguage);
        });

        // Environment provider for display server detection
        services.AddSingleton<IEnvironmentProvider, SystemEnvironmentProvider>();

        // Text typer factory (injectable, testable)
        services.AddSingleton<ITextTyperFactory, TextTyperFactory>();

        // Text typer (auto-detect display server)
        services.AddSingleton<ITextTyper>(sp =>
        {
            var factory = sp.GetRequiredService<ITextTyperFactory>();
            return factory.Create();
        });

        // NOTE: TypingSoundPlayer removed - now using INotificationPlayer from NotificationAudio
        // TranscriptionSoundPath is still in configuration but will be used differently

        // Optional: Text filter
        var filtersPath = options.GetFullTextFiltersPath();
        if (!string.IsNullOrWhiteSpace(filtersPath))
        {
            services.AddSingleton(sp =>
            {
                var logger = sp.GetRequiredService<ILogger<TextFilter>>();
                return new TextFilter(logger, filtersPath);
            });
        }

        // Transcription coordinator (combines speech transcription + sound feedback)
        services.AddSingleton<ITranscriptionCoordinator>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<TranscriptionCoordinator>>();
            var transcriber = sp.GetRequiredService<ISpeechTranscriber>();
            var notificationPlayer = sp.GetRequiredService<Olbrasoft.NotificationAudio.Abstractions.INotificationPlayer>();
            var serviceScopeFactory = sp.GetRequiredService<IServiceScopeFactory>();
            var soundPath = options.GetFullTranscriptionSoundPath();
            return new TranscriptionCoordinator(logger, transcriber, notificationPlayer, serviceScopeFactory, soundPath);
        });

        // Text output handler (combines text filtering + typing)
        services.AddSingleton<ITextOutputHandler>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<TextOutputHandler>>();
            var textTyper = sp.GetRequiredService<ITextTyper>();
            var textFilter = sp.GetService<TextFilter>();
            return new TextOutputHandler(logger, textTyper, textFilter);
        });

        // Dictation service (orchestrates recording, transcription, and output)
        services.AddSingleton(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<DictationService>>();
            var keyboardMonitor = sp.GetRequiredService<IKeyboardMonitor>();
            var keySimulator = sp.GetRequiredService<IKeySimulator>();
            var audioRecorder = sp.GetRequiredService<IAudioRecorder>();
            var transcriptionCoordinator = sp.GetRequiredService<ITranscriptionCoordinator>();
            var textOutputHandler = sp.GetRequiredService<ITextOutputHandler>();
            var vaClient = sp.GetService<IVirtualAssistantClient>();
            var notificationPlayer = sp.GetRequiredService<Olbrasoft.NotificationAudio.Abstractions.INotificationPlayer>();
            var recordingStartSoundPath = options.GetFullRecordingStartSoundPath();

            return new DictationService(
                logger,
                keyboardMonitor,
                keySimulator,
                audioRecorder,
                transcriptionCoordinator,
                textOutputHandler,
                vaClient,
                notificationPlayer,
                recordingStartSoundPath,
                options.GetTriggerKeyCode(),
                options.GetCancelKeyCode());
        });

        // Database - PostgreSQL with Entity Framework Core
        services.AddDbContext<PushToTalkDbContext>(options =>
        {
            var connectionString = configuration.GetConnectionString("DefaultConnection");
            if (!string.IsNullOrWhiteSpace(connectionString))
            {
                options.UseNpgsql(connectionString);
            }
            // If connection string is not configured, DbContext won't be usable
            // but app will still work (just won't save transcriptions)
        });

        // Transcription repository for saving Whisper transcriptions to database
        services.AddScoped<ITranscriptionRepository, TranscriptionRepository>();

        return services;
    }

    /// <summary>
    /// Adds tray icon services to the service collection.
    /// </summary>
    public static IServiceCollection AddTrayServices(
        this IServiceCollection services,
        DictationOptions options,
        string iconsPath,
        TrayIconOptions trayIconOptions)
    {
        // SpeechToText service manager for status checking and control
        services.AddSingleton<SpeechToTextServiceManager>();

        // Icon renderer for SVG rendering
        services.AddSingleton(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<IconRenderer>>();
            return new IconRenderer(logger);
        });

        // Tray icon manager for managing multiple icons (main + animated)
        services.AddSingleton(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<TrayIconManager>>();
            var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
            var iconRenderer = sp.GetRequiredService<IconRenderer>();
            return new TrayIconManager(logger, loggerFactory, iconRenderer);
        });

        // DBus menu handler for tray icon context menu
        services.AddSingleton<ITrayMenuHandler>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<DBusMenuHandler>>();
            return new DBusMenuHandler(logger);
        });

        // PushToTalk tray service (wrapper for main + animated icons)
        services.AddSingleton(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<PushToTalkTrayService>>();
            var manager = sp.GetRequiredService<TrayIconManager>();
            var menuHandler = sp.GetRequiredService<ITrayMenuHandler>();
            return new PushToTalkTrayService(logger, manager, iconsPath, trayIconOptions, menuHandler);
        });

        return services;
    }
}

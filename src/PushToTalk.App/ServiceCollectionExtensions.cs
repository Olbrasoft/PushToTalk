using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Olbrasoft.PushToTalk.App.Services;
using Olbrasoft.PushToTalk.App.Tray;
using Olbrasoft.PushToTalk.Audio;
using Olbrasoft.PushToTalk.Linux.Speech;
using Olbrasoft.PushToTalk.TextInput;
using Olbrasoft.SystemTray.Linux;

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

        // Optional: Typing sound player
        var soundPath = options.GetFullTranscriptionSoundPath();
        if (!string.IsNullOrWhiteSpace(soundPath))
        {
            services.AddSingleton(sp =>
            {
                var logger = sp.GetRequiredService<ILogger<TypingSoundPlayer>>();
                return new TypingSoundPlayer(logger, soundPath);
            });
        }

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
            var soundPlayer = sp.GetService<TypingSoundPlayer>();
            return new TranscriptionCoordinator(logger, transcriber, soundPlayer);
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
            var soundPlayer = sp.GetService<TypingSoundPlayer>();
            var recordingStartSoundPath = options.GetFullRecordingStartSoundPath();

            return new DictationService(
                logger,
                keyboardMonitor,
                keySimulator,
                audioRecorder,
                transcriptionCoordinator,
                textOutputHandler,
                vaClient,
                soundPlayer,
                recordingStartSoundPath,
                options.GetTriggerKeyCode(),
                options.GetCancelKeyCode());
        });

        return services;
    }

    /// <summary>
    /// Adds tray icon services to the service collection.
    /// </summary>
    public static IServiceCollection AddTrayServices(
        this IServiceCollection services,
        DictationOptions options,
        string iconsPath)
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
            return new PushToTalkTrayService(logger, manager, iconsPath, menuHandler);
        });

        return services;
    }
}

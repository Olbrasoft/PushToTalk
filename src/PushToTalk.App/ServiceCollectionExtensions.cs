using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Olbrasoft.NotificationAudio.Providers.Linux;
using Olbrasoft.PushToTalk.App.Configuration;
using Olbrasoft.PushToTalk.App.Services;
using Olbrasoft.PushToTalk.App.Tray;
using Olbrasoft.PushToTalk.Audio;
using Olbrasoft.PushToTalk.Core.Interfaces;
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
            return new SpeechToTextGrpcClient(
                logger,
                serviceUrl,
                options.WhisperLanguage,
                "ggml-large-v3-turbo.bin");
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

        // Text typer (auto-detect display server)
        services.AddSingleton<ITextTyper>(sp =>
        {
            var factory = sp.GetRequiredService<ITextTyperFactory>();
            return factory.Create();
        });

        // NOTE: TypingSoundPlayer removed - now using INotificationPlayer from NotificationAudio
        // TranscriptionSoundPath is still in configuration but will be used differently

        // Text filter strategies (Strategy pattern)
        services.AddSingleton<Filters.ITextFilterStrategy>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<Filters.DatabaseCorrectionFilterStrategy>>();
            var serviceScopeFactory = sp.GetRequiredService<IServiceScopeFactory>();
            return new Filters.DatabaseCorrectionFilterStrategy(logger, serviceScopeFactory);
        });

        services.AddSingleton<Filters.ITextFilterStrategy>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<Filters.FileReplacementFilterStrategy>>();
            var filtersPath = options.GetFullTextFiltersPath();
            return new Filters.FileReplacementFilterStrategy(logger, filtersPath);
        });

        services.AddSingleton<Filters.ITextFilterStrategy>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<Filters.RemovePatternsFilterStrategy>>();
            var filtersPath = options.GetFullTextFiltersPath();
            return new Filters.RemovePatternsFilterStrategy(logger, filtersPath);
        });

        services.AddSingleton<Filters.ITextFilterStrategy>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<Filters.WhitespaceFilterStrategy>>();
            return new Filters.WhitespaceFilterStrategy(logger);
        });

        // Composite text filter (applies all strategies in order)
        services.AddSingleton<ITextFilter, Filters.CompositeTextFilter>();

        // Transcription coordinator (combines speech transcription + sound feedback)
        services.AddSingleton<ITranscriptionCoordinator>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<TranscriptionCoordinator>>();
            var transcriber = sp.GetRequiredService<ISpeechTranscriber>();
            var notificationPlayer = sp.GetRequiredService<Olbrasoft.NotificationAudio.Abstractions.INotificationPlayer>();
            var serviceScopeFactory = sp.GetRequiredService<IServiceScopeFactory>();
            var textFilter = sp.GetService<ITextFilter>();
            var soundPath = options.GetFullTranscriptionSoundPath();
            return new TranscriptionCoordinator(logger, transcriber, notificationPlayer, serviceScopeFactory, textFilter, soundPath);
        });

        // Text output handler (text typing only - filtering now in TranscriptionCoordinator)
        services.AddSingleton<ITextOutputHandler>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<TextOutputHandler>>();
            var textTyper = sp.GetRequiredService<ITextTyper>();
            return new TextOutputHandler(logger, textTyper, textFilter: null);
        });

        // State machine for dictation state management (State pattern)
        services.AddSingleton<Olbrasoft.PushToTalk.App.StateMachine.IDictationStateMachine, Olbrasoft.PushToTalk.App.StateMachine.DictationStateMachine>();

        // CapsLock LED synchronizer (for web remote control)
        services.AddSingleton<Olbrasoft.PushToTalk.App.Keyboard.ICapsLockSynchronizer, Olbrasoft.PushToTalk.App.Keyboard.CapsLockSynchronizer>();

        // Dictation service (orchestrates recording, transcription, and output)
        services.AddSingleton(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<DictationService>>();
            var stateMachine = sp.GetRequiredService<Olbrasoft.PushToTalk.App.StateMachine.IDictationStateMachine>();
            var capsLockSynchronizer = sp.GetRequiredService<Olbrasoft.PushToTalk.App.Keyboard.ICapsLockSynchronizer>();
            var keyboardMonitor = sp.GetRequiredService<IKeyboardMonitor>();
            var audioRecorder = sp.GetRequiredService<IAudioRecorder>();
            var transcriptionCoordinator = sp.GetRequiredService<ITranscriptionCoordinator>();
            var textOutputHandler = sp.GetRequiredService<ITextOutputHandler>();
            var vaClient = sp.GetService<IVirtualAssistantClient>();
            var notificationPlayer = sp.GetRequiredService<Olbrasoft.NotificationAudio.Abstractions.INotificationPlayer>();
            var recordingStartSoundPath = options.GetFullRecordingStartSoundPath();

            return new DictationService(
                logger,
                stateMachine,
                capsLockSynchronizer,
                keyboardMonitor,
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

        // Transcription corrections repository (for ASR post-processing)
        services.AddScoped<ITranscriptionCorrectionRepository, TranscriptionCorrectionRepository>();

        // LLM Correction Services
        // Configure Mistral options from database (not from appsettings/secrets)
        services.ConfigureOptions<Service.Configuration.DatabaseMistralOptionsSetup>();

        // Configure ServiceEndpoints for VirtualAssistant URL
        services.Configure<Core.Configuration.ServiceEndpoints>(
            configuration.GetSection(Core.Configuration.ServiceEndpoints.SectionName));

        // Embedded prompt loader (fallback)
        services.AddSingleton<Core.Services.EmbeddedPromptLoader>();

        // Hybrid prompt loader (file-first, embedded fallback)
        services.AddSingleton<Core.Interfaces.IPromptLoader>(sp =>
        {
            var fileBasePath = "/opt/olbrasoft/push-to-talk/prompts";
            var embeddedFallback = sp.GetRequiredService<Core.Services.EmbeddedPromptLoader>();
            var logger = sp.GetRequiredService<ILogger<Core.Services.HybridPromptLoader>>();
            return new Core.Services.HybridPromptLoader(fileBasePath, embeddedFallback, logger);
        });

        // Reloadable prompt cache
        services.AddSingleton<Core.Interfaces.IPromptCache, Core.Services.ReloadablePromptCache>();

        // Register MistralProvider as singleton with HttpClient
        services.AddSingleton<Core.Services.MistralProvider>(sp =>
        {
            var httpClient = new HttpClient();
            var options = sp.GetRequiredService<IOptions<Core.Configuration.MistralOptions>>();
            var promptCache = sp.GetRequiredService<Core.Interfaces.IPromptCache>();
            var logger = sp.GetRequiredService<ILogger<Core.Services.MistralProvider>>();
            return new Core.Services.MistralProvider(httpClient, options, promptCache, logger);
        });

        // Register ILlmProvider as alias to the same singleton instance
        services.AddSingleton<ILlmProvider>(sp => sp.GetRequiredService<Core.Services.MistralProvider>());

        // HTTP client for NotificationClient (VirtualAssistant)
        services.AddHttpClient<INotificationClient, Service.Services.NotificationClient>();

        // LLM correction orchestration service (scoped - uses DbContext)
        services.AddScoped<ILlmCorrectionService, Service.Services.LlmCorrectionService>();

        // Email notification service (scoped - uses DbContext)
        services.AddScoped<IEmailNotificationService, Service.Services.EmailNotificationService>();

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

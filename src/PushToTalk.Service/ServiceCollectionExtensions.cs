using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Olbrasoft.PushToTalk.Audio;
using Olbrasoft.PushToTalk.Core.Interfaces;
using Olbrasoft.PushToTalk.Service.Services;
using Olbrasoft.PushToTalk.Speech;
using Olbrasoft.PushToTalk.TextInput;

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
        var ggmlModelPath = configuration.GetValue<string>("PushToTalkDictation:GgmlModelPath")
            ?? Path.Combine(AppContext.BaseDirectory, "models", "ggml-medium.bin");
        var whisperLanguage = configuration.GetValue<string>("PushToTalkDictation:WhisperLanguage") ?? "cs";
        var bluetoothTripleClickCommand = configuration.GetValue<string?>("BluetoothMouse:TripleClickCommand");

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
            var logger = sp.GetRequiredService<ILogger<AlsaAudioRecorder>>();
            return new AlsaAudioRecorder(logger);
        });

        services.AddSingleton<ISpeechTranscriber>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<WhisperNetTranscriber>>();
            return new WhisperNetTranscriber(logger, ggmlModelPath, whisperLanguage);
        });

        // Environment provider for display server detection
        services.AddSingleton<IEnvironmentProvider, SystemEnvironmentProvider>();

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

        // HTTP client for DictationWorker
        services.AddHttpClient<DictationWorker>();

        // Bluetooth mouse monitor (remote push-to-talk trigger)
        services.AddSingleton(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<BluetoothMouseMonitor>>();
            var keyboardMonitor = sp.GetRequiredService<IKeyboardMonitor>();
            var keySimulator = sp.GetRequiredService<IKeySimulator>();
            return new BluetoothMouseMonitor(logger, keyboardMonitor, keySimulator, "BluetoothMouse3600", bluetoothTripleClickCommand);
        });

        // USB Optical Mouse monitor (secondary push-to-talk trigger)
        services.AddSingleton(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<UsbMouseMonitor>>();
            var keyboardMonitor = sp.GetRequiredService<IKeyboardMonitor>();
            var keySimulator = sp.GetRequiredService<IKeySimulator>();
            return new UsbMouseMonitor(logger, keyboardMonitor, keySimulator);
        });

        // Register worker as singleton first (so we can resolve it for interfaces)
        services.AddSingleton<DictationWorker>();
        services.AddHostedService<DictationWorker>(sp => sp.GetRequiredService<DictationWorker>());

        // Register interfaces pointing to the same DictationWorker instance
        services.AddSingleton<IRecordingStateProvider>(sp => sp.GetRequiredService<DictationWorker>());
        services.AddSingleton<IRecordingController>(sp => sp.GetRequiredService<DictationWorker>());

        return services;
    }
}
